namespace digen_2.Core;

/// <summary>
/// Renders a <see cref="CallGraphNode"/> tree as a diagram in some text
/// format (PlantUML, Mermaid, ...). Implement this to add another output
/// format - it never sees language-specific symbols, only the generic
/// call graph model.
/// </summary>
public interface IDiagramExporter
{
    /// <summary>Short identifier used to select this exporter via --format (e.g. "plantuml").</summary>
    string Id { get; }

    /// <summary>Renders a full outgoing call tree (root = target method, children = what it calls) as one diagram.</summary>
    string RenderOutgoing(CallGraphNode root);

    /// <summary>Renders one chronological call chain from an incoming call hierarchy - entry point first, target last - as one diagram.</summary>
    string RenderIncomingPath(IReadOnlyList<CallGraphNode> chronologicalChain, int pathIndex, int pathCount);
}
