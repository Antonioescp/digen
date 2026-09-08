using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace digen_2.Analysis;

/// <summary>
/// Builds the incoming call hierarchy for a method - "who calls this?" -
/// recursively up to entry points, mirroring an IDE's Call Hierarchy /
/// Incoming Calls view.
/// </summary>
public sealed class IncomingCallGraphBuilder(Solution solution, int maxDepth)
{
    private const int MaxTotalNodes = 4000;
    private int _nodeCount;

    public async Task<CallerNode> BuildAsync(IMethodSymbol target)
    {
        var root = new CallerNode(target.OriginalDefinition);
        _nodeCount = 1;

        var pathStack = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default) { root.Method };
        await ExpandAsync(root, pathStack, depth: 0);

        if (root.Callers.Count == 0 && root.Note is null)
        {
            root.Note = "no callers found in solution";
        }

        return root;
    }

    private async Task ExpandAsync(CallerNode node, HashSet<IMethodSymbol> pathStack, int depth)
    {
        if (depth >= maxDepth)
        {
            node.Note = "max depth reached";
            return;
        }

        var callerInfos = await SymbolFinder.FindCallersAsync(node.Method, solution);

        var callers = callerInfos
            .Select(c => c.CallingSymbol)
            .OfType<IMethodSymbol>()
            .Select(m => m.OriginalDefinition)
            .Distinct(SymbolEqualityComparer.Default)
            .Cast<IMethodSymbol>()
            .ToList();

        foreach (var caller in callers)
        {
            if (_nodeCount >= MaxTotalNodes)
            {
                node.Note = "truncated: too many callers";
                return;
            }

            var child = new CallerNode(caller);
            node.Callers.Add(child);
            _nodeCount++;

            if (pathStack.Contains(caller))
            {
                child.Note = "recursive call, truncated";
                continue;
            }

            pathStack.Add(caller);
            await ExpandAsync(child, pathStack, depth + 1);
            pathStack.Remove(caller);
        }
    }
}
