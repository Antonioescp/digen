namespace digen_2.Core;

/// <summary>Capability interface: an ICodeAnalyzer that can build a class diagram rooted at a target type.</summary>
public interface IClassDiagramAnalyzer : ICodeAnalyzer
{
    Task<ClassDiagramResult> AnalyzeAsync(ClassDiagramRequest request);
}

public sealed record ClassDiagramRequest(string SolutionPath, string TargetType, int MaxDepth);

public sealed class ClassDiagramResult
{
    public bool Success { get; private init; }
    public ClassDiagramModel? Model { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static ClassDiagramResult Ok(ClassDiagramModel model) => new() { Success = true, Model = model };
    public static ClassDiagramResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
