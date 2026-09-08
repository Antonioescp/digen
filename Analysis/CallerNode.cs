using Microsoft.CodeAnalysis;

namespace digen_2.Analysis;

/// <summary>
/// One node in an incoming call hierarchy: Method is called by each entry in
/// Callers. The root node wraps the diagram's target method; leaves (empty
/// Callers) are either true entry points (nothing in the solution calls them)
/// or a point where expansion was stopped early - see Note.
/// </summary>
public sealed class CallerNode(IMethodSymbol method)
{
    public IMethodSymbol Method { get; } = method;
    public List<CallerNode> Callers { get; } = [];

    /// <summary>Set when this node is a leaf because expansion stopped early (recursion, depth limit, no callers found), rather than a genuine entry point.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// Every root-to-leaf chain, root (target method) first. Reverse a chain
    /// to get chronological calling order (entry point first, target last).
    /// </summary>
    public List<List<CallerNode>> EnumeratePaths()
    {
        var results = new List<List<CallerNode>>();
        Walk(this, [], results);
        return results;
    }

    private static void Walk(CallerNode node, List<CallerNode> current, List<List<CallerNode>> results)
    {
        current.Add(node);
        if (node.Callers.Count == 0)
        {
            results.Add([..current]);
        }
        else
        {
            foreach (var caller in node.Callers)
                Walk(caller, current, results);
        }

        current.RemoveAt(current.Count - 1);
    }
}
