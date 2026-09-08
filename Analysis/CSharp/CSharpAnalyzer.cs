using digen_2.Core;
using Microsoft.CodeAnalysis.MSBuild;

namespace digen_2.Analysis.CSharp;

/// <summary>Roslyn-based ICodeAnalyzer implementation for C# solutions.</summary>
public sealed class CSharpAnalyzer : ICodeAnalyzer
{
    public string Id => "csharp";

    public bool CanAnalyze(string solutionPath) =>
        solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
        solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);

    public async Task<CallGraphResult> AnalyzeAsync(CallGraphRequest request)
    {
        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == Microsoft.CodeAnalysis.WorkspaceDiagnosticKind.Failure)
                Console.Error.WriteLine($"warning: {e.Diagnostic.Message}");
        });

        Console.Error.WriteLine($"Loading solution: {request.SolutionPath}");
        var solution = await workspace.OpenSolutionAsync(request.SolutionPath);

        Console.Error.WriteLine($"Resolving target method: {request.TargetMethod}");
        var resolver = new MethodTargetResolver(solution);
        var resolution = await resolver.ResolveAsync(request.TargetMethod, request.ParamTypes);
        if (!resolution.Success)
        {
            return CallGraphResult.Fail(resolution.ErrorMessage!);
        }

        if (request.Direction == CallDirection.Incoming)
        {
            Console.Error.WriteLine($"Building incoming call hierarchy (max depth {request.MaxDepth})...");
            var builder = new IncomingCallGraphBuilder(solution, request.MaxDepth);
            var root = await builder.BuildAsync(resolution.Method!);
            return CallGraphResult.Ok(root);
        }
        else
        {
            Console.Error.WriteLine($"Building outgoing call graph (max depth {request.MaxDepth})...");
            var builder = new CallGraphBuilder(solution, request.MaxDepth, request.IncludeExternalCalls);
            var root = await builder.BuildAsync(resolution.Method!);
            return CallGraphResult.Ok(root);
        }
    }
}
