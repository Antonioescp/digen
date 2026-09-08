using digen_2.Core;

namespace digen_2.Diagrams.PlantUml;

/// <summary>PlantUML IDiagramExporter. Supports both sequence and class diagrams.</summary>
public sealed class PlantUmlExporter : ISequenceDiagramExporter, IClassDiagramExporter
{
    public string Id => "plantuml";

    public IReadOnlyCollection<DiagramKind> SupportedKinds { get; } = [DiagramKind.Sequence, DiagramKind.Class];

    public string RenderOutgoing(CallGraphNode root) => PlantUmlSequenceRenderer.RenderOutgoing(root);

    public string RenderIncomingPath(IReadOnlyList<CallGraphNode> chronologicalChain, int pathIndex, int pathCount) =>
        PlantUmlSequenceRenderer.RenderIncomingPath(chronologicalChain, pathIndex, pathCount);

    public string Render(ClassDiagramModel model) => PlantUmlClassDiagramRenderer.Render(model);
}
