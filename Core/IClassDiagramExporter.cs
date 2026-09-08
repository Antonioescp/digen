namespace digen_2.Core;

/// <summary>Capability interface: an IDiagramExporter that can render a class diagram.</summary>
public interface IClassDiagramExporter : IDiagramExporter
{
    string Render(ClassDiagramModel model);
}
