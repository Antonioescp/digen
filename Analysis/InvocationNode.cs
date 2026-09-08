using Microsoft.CodeAnalysis;

namespace digen_2.Analysis;

/// <summary>
/// One call edge in the call graph: Caller invokes Callee. The root node
/// (representing the initial call into the diagram's target method) has a
/// null Caller.
/// </summary>
public sealed class InvocationNode(IMethodSymbol callee, IMethodSymbol? caller)
{
    public IMethodSymbol Callee { get; } = callee;
    public IMethodSymbol? Caller { get; } = caller;
    public List<InvocationNode> Children { get; } = [];

    /// <summary>True if this call resolved to source code we could have walked further into.</summary>
    public bool HasSource { get; set; }

    /// <summary>Set when expansion stopped early (recursion, depth limit, ambiguous override, etc.).</summary>
    public string? Note { get; set; }
}
