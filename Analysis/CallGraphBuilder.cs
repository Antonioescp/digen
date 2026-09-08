using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace digen_2.Analysis;

/// <summary>
/// Walks method bodies via the Roslyn semantic model, starting from a root
/// method, building a tree of InvocationNode call edges.
/// </summary>
public sealed class CallGraphBuilder(Solution solution, int maxDepth, bool includeExternalCalls)
{
    private const int MaxTotalNodes = 4000;

    private readonly Dictionary<SyntaxTree, SemanticModel> _semanticModels = new();
    private int _nodeCount;

    public async Task<InvocationNode> BuildAsync(IMethodSymbol root)
    {
        var rootNode = new InvocationNode(root, null) { HasSource = root.DeclaringSyntaxReferences.Length > 0 };
        _nodeCount = 1;

        var ancestors = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default) { root };
        await ExpandAsync(rootNode, ancestors, depth: 0);
        return rootNode;
    }

    private async Task ExpandAsync(InvocationNode node, HashSet<IMethodSymbol> ancestors, int depth)
    {
        if (depth >= maxDepth)
        {
            node.Note = "max depth reached";
            return;
        }

        var method = node.Callee;
        var declSyntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
        if (declSyntaxRef is null)
        {
            // No source available (BCL, NuGet package, abstract member with no body, etc.)
            return;
        }

        node.HasSource = true;

        var declSyntax = await declSyntaxRef.GetSyntaxAsync();
        var semanticModel = await GetSemanticModelAsync(declSyntax.SyntaxTree);
        if (semanticModel is null) return;

        foreach (var (invocationSyntax, isCreation) in FindCalls(declSyntax))
        {
            if (_nodeCount >= MaxTotalNodes)
            {
                node.Note = "diagram truncated: too many calls";
                return;
            }

            var symbolInfo = semanticModel.GetSymbolInfo(invocationSyntax);
            var calleeSymbol = symbolInfo.Symbol as IMethodSymbol
                                ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
            if (calleeSymbol is null) continue;
            calleeSymbol = calleeSymbol.OriginalDefinition;

            if (!isCreation && ShouldSkip(calleeSymbol)) continue;

            var resolved = await ResolvePolymorphicTargetAsync(calleeSymbol);

            if (resolved.Target is null)
            {
                // No source and caller asked to omit such leaves entirely.
                if (!includeExternalCalls) continue;
                var leaf = new InvocationNode(calleeSymbol, method) { HasSource = false, Note = resolved.Note };
                node.Children.Add(leaf);
                _nodeCount++;
                continue;
            }

            var target = resolved.Target;
            var child = new InvocationNode(target, method) { Note = resolved.Note };
            node.Children.Add(child);
            _nodeCount++;

            if (ancestors.Contains(target))
            {
                child.Note = string.IsNullOrEmpty(child.Note) ? "recursive call" : child.Note + "; recursive call";
                child.HasSource = target.DeclaringSyntaxReferences.Length > 0;
                continue;
            }

            ancestors.Add(target);
            await ExpandAsync(child, ancestors, depth + 1);
            ancestors.Remove(target);
        }
    }

    /// <summary>
    /// Skip calls into the language/runtime plumbing that would otherwise
    /// clutter every diagram (property accessors on simple fields, etc. are
    /// still fine - this only filters truly noisy operator/equality helpers).
    /// </summary>
    private static bool ShouldSkip(IMethodSymbol symbol) =>
        symbol.ContainingType?.SpecialType == SpecialType.System_Object &&
        symbol.Name is "GetType" or "ToString" or "Equals" or "GetHashCode";

    private readonly struct ResolvedTarget(IMethodSymbol? target, string? note)
    {
        public IMethodSymbol? Target { get; } = target;
        public string? Note { get; } = note;
    }

    /// <summary>
    /// If the call targets an interface method (typical DI-style call),
    /// try to follow it to its single concrete implementation in the
    /// solution. Ambiguous (multiple implementations) or virtual/abstract
    /// class members are left unresolved - we can't know the runtime type
    /// statically, so we report it rather than guess.
    /// </summary>
    private async Task<ResolvedTarget> ResolvePolymorphicTargetAsync(IMethodSymbol symbol)
    {
        var hasSource = symbol.DeclaringSyntaxReferences.Length > 0;

        if (symbol.ContainingType?.TypeKind == TypeKind.Interface)
        {
            var implementations = await SymbolFinder.FindImplementationsAsync(symbol, solution);
            var concrete = implementations
                .OfType<IMethodSymbol>()
                .Where(m => m.DeclaringSyntaxReferences.Length > 0)
                .ToList();

            if (concrete.Count == 1)
            {
                return new ResolvedTarget(concrete[0], $"via {concrete[0].ContainingType.Name}");
            }

            if (concrete.Count > 1)
            {
                return new ResolvedTarget(hasSource ? symbol : null,
                    $"{concrete.Count} possible implementations, not expanded");
            }

            return new ResolvedTarget(hasSource ? symbol : null, null);
        }

        return new ResolvedTarget(hasSource ? symbol : null, null);
    }

    private async Task<SemanticModel?> GetSemanticModelAsync(SyntaxTree tree)
    {
        if (_semanticModels.TryGetValue(tree, out var cached)) return cached;

        var document = solution.GetDocument(tree);
        Compilation? compilation = document is not null
            ? await document.Project.GetCompilationAsync()
            : null;

        if (compilation is null) return null;

        var model = compilation.GetSemanticModel(tree);
        _semanticModels[tree] = model;
        return model;
    }

    private static IEnumerable<(SyntaxNode Node, bool IsCreation)> FindCalls(SyntaxNode declaration)
    {
        var body = GetBodyNode(declaration);
        var nodes = new List<(SyntaxNode, bool)>();

        if (body is not null)
        {
            nodes.AddRange(body.DescendantNodesAndSelf()
                .Where(n => n is InvocationExpressionSyntax)
                .Select(n => (n, false)));
            nodes.AddRange(body.DescendantNodesAndSelf()
                .Where(n => n is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
                .Select(n => (n, true)));
        }

        if (declaration is ConstructorDeclarationSyntax { Initializer: { } initializer })
        {
            nodes.Add((initializer, false));
        }

        return nodes.OrderBy(n => n.Item1.SpanStart);
    }

    private static SyntaxNode? GetBodyNode(SyntaxNode decl) => decl switch
    {
        BaseMethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody?.Expression,
        AccessorDeclarationSyntax a => (SyntaxNode?)a.Body ?? a.ExpressionBody?.Expression,
        LocalFunctionStatementSyntax l => (SyntaxNode?)l.Body ?? l.ExpressionBody?.Expression,
        PropertyDeclarationSyntax { ExpressionBody: { } arrow } => arrow.Expression,
        ArrowExpressionClauseSyntax arrow => arrow.Expression,
        _ => null,
    };
}
