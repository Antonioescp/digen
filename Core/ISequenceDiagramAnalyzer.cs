namespace digen_2.Core;

/// <summary>Capability interface: an ICodeAnalyzer that can build a call graph rooted at a target method.</summary>
public interface ISequenceDiagramAnalyzer : ICodeAnalyzer
{
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
