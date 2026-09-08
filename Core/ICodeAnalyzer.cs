namespace digen_2.Core;

/// <summary>
/// A source-language analyzer: loads a project/solution, resolves a target
/// method, and builds its call graph in one direction. Implement this to add
/// support for another language (e.g. Java) - the rest of the tool (CLI,
/// diagram exporters) only ever deals with the language-agnostic
/// <see cref="CallGraphNode"/> tree this returns.
/// </summary>
public interface ICodeAnalyzer
{
    /// <summary>Short identifier used to select this analyzer via --language (e.g. "csharp").</summary>
    string Id { get; }

    /// <summary>True if this analyzer can load the given project/solution file, based on its extension.</summary>
    bool CanAnalyze(string solutionPath);

    Task<CallGraphResult> AnalyzeAsync(CallGraphRequest request);
}

public sealed record CallGraphRequest(
    string SolutionPath,
    string TargetMethod,
    string[]? ParamTypes,
    CallDirection Direction,
    int MaxDepth,
    bool IncludeExternalCalls);

public sealed class CallGraphResult
{
    public bool Success { get; private init; }
    public CallGraphNode? Root { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static CallGraphResult Ok(CallGraphNode root) => new() { Success = true, Root = root };
    public static CallGraphResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
