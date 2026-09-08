namespace digen_2.Core;

/// <summary>
/// One node in a call graph tree, produced by an <see cref="ICodeAnalyzer"/>.
/// The same shape serves both directions:
///  - outgoing: root is the target method, children are what it calls.
///  - incoming: root is the target method, children are what calls it (up
///    towards entry points).
/// </summary>
public sealed class CallGraphNode(CallableMethod method)
{
    public CallableMethod Method { get; } = method;
    public List<CallGraphNode> Children { get; } = [];

    /// <summary>Set when this node is a leaf because expansion stopped early (recursion, depth limit, ambiguity, too many nodes), rather than there genuinely being nothing further.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// Every root-to-leaf chain in this tree, root first. For an incoming
    /// hierarchy, reverse a chain to get chronological calling order (entry
    /// point first, target last).
    /// </summary>
    public List<List<CallGraphNode>> EnumeratePaths()
    {
        var results = new List<List<CallGraphNode>>();
        Walk(this, [], results);
        return results;
    }

    private static void Walk(CallGraphNode node, List<CallGraphNode> current, List<List<CallGraphNode>> results)
    {
        current.Add(node);
        if (node.Children.Count == 0)
        {
            results.Add([..current]);
        }
        else
        {
            foreach (var child in node.Children)
                Walk(child, current, results);
        }

        current.RemoveAt(current.Count - 1);
    }
}
