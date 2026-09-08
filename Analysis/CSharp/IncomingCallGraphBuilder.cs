using digen_2.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace digen_2.Analysis.CSharp;

/// <summary>
/// Builds the incoming call hierarchy for a method - "who calls this?" -
/// recursively up to entry points, mirroring an IDE's Call Hierarchy /
/// Incoming Calls view.
/// </summary>
public sealed class IncomingCallGraphBuilder(Solution solution, int maxDepth)
{
    private const int MaxTotalNodes = 4000;
    private int _nodeCount;

    public async Task<CallGraphNode> BuildAsync(IMethodSymbol target)
    {
        var targetDef = target.OriginalDefinition;
        var root = new CallGraphNode(CallableMethodMapper.Map(targetDef));
        _nodeCount = 1;

        var pathStack = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default) { targetDef };
        await ExpandAsync(root, targetDef, pathStack, depth: 0);

        if (root.Children.Count == 0 && root.Note is null)
        {
            root.Note = "no callers found in solution";
        }

        return root;
    }

    private async Task ExpandAsync(CallGraphNode node, IMethodSymbol method, HashSet<IMethodSymbol> pathStack, int depth)
    {
        if (depth >= maxDepth)
        {
            node.Note = "max depth reached";
            return;
        }

        var callerInfos = await SymbolFinder.FindCallersAsync(method, solution);

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

            var child = new CallGraphNode(CallableMethodMapper.Map(caller));
            node.Children.Add(child);
            _nodeCount++;

            if (pathStack.Contains(caller))
            {
                child.Note = "recursive call, truncated";
                continue;
            }

            pathStack.Add(caller);
            await ExpandAsync(child, caller, pathStack, depth + 1);
            pathStack.Remove(caller);
        }
    }
}
