using digen_2.Core;

namespace digen_2.Diagrams.Mermaid;

/// <summary>Mermaid IDiagramExporter. Supports both sequence and class diagrams.</summary>
public sealed class MermaidExporter : ISequenceDiagramExporter, IClassDiagramExporter
{
    public string Id => "mermaid";

    public IReadOnlyCollection<DiagramKind> SupportedKinds { get; } = [DiagramKind.Sequence, DiagramKind.Class];

    public string FormatComment(string text) => $"%% {text}";

    public string RenderOutgoing(CallGraphNode root) => MermaidSequenceRenderer.RenderOutgoing(root);

    public string RenderIncomingPath(IReadOnlyList<CallGraphNode> chronologicalChain, int pathIndex, int pathCount) =>
        MermaidSequenceRenderer.RenderIncomingPath(chronologicalChain, pathIndex, pathCount);

    public string Render(ClassDiagramModel model) => MermaidClassDiagramRenderer.Render(model);
}
