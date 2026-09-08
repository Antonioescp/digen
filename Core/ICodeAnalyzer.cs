namespace digen_2.Core;

/// <summary>
/// A source-language analyzer: loads a project/solution and produces
/// diagram data for it. Implement this (plus one capability interface per
/// diagram kind it supports, e.g. <see cref="ISequenceDiagramAnalyzer"/>) to
/// add support for another language (e.g. Java). The rest of the tool (CLI,
/// diagram exporters) only ever deals with the language-agnostic domain
/// model each capability interface returns.
/// </summary>
public interface ICodeAnalyzer
{
    /// <summary>Short identifier used to select this analyzer via --language (e.g. "csharp").</summary>
    string Id { get; }

    /// <summary>True if this analyzer can load the given project/solution file, based on its extension.</summary>
    bool CanAnalyze(string solutionPath);

    /// <summary>Which diagram kinds this analyzer can produce data for. Each one listed here must be backed by the matching capability interface (e.g. Sequence -> ISequenceDiagramAnalyzer).</summary>
    IReadOnlyCollection<DiagramKind> SupportedKinds { get; }
}
