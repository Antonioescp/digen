namespace digen_2.Core;

/// <summary>
/// Renders diagram data in some text format (PlantUML, Mermaid, ...).
/// Implement this (plus one capability interface per diagram kind it
/// supports, e.g. <see cref="ISequenceDiagramExporter"/>) to add another
/// output format. Exporters never see language-specific symbols, only the
/// generic domain model each capability interface takes.
/// </summary>
public interface IDiagramExporter
{
    /// <summary>Short identifier used to select this exporter via --format (e.g. "plantuml").</summary>
    string Id { get; }

    /// <summary>Which diagram kinds this exporter can render. Each one listed here must be backed by the matching capability interface (e.g. Sequence -> ISequenceDiagramExporter).</summary>
    IReadOnlyCollection<DiagramKind> SupportedKinds { get; }

    /// <summary>Formats a line of free text as a comment in this format - used to separate multiple diagrams printed to stdout.</summary>
    string FormatComment(string text) => $"' {text}";
}
