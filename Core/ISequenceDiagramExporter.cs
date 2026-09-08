namespace digen_2.Core;

/// <summary>Capability interface: an IDiagramExporter that can render a call graph.</summary>
public interface ISequenceDiagramExporter : IDiagramExporter
{
    /// <summary>Renders a full outgoing call tree (root = target method, children = what it calls) as one diagram.</summary>
    string RenderOutgoing(CallGraphNode root);

    /// <summary>Renders one chronological call chain from an incoming call hierarchy - entry point first, target last - as one diagram.</summary>
    string RenderIncomingPath(IReadOnlyList<CallGraphNode> chronologicalChain, int pathIndex, int pathCount);
}
