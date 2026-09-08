using digen_2.Analysis;
using digen_2.Cli;
using digen_2.PlantUml;
using Microsoft.Build.Locator;

MSBuildLocator.RegisterDefaults();
return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    CliOptions options;
    try
    {
        options = CliOptions.Parse(args);
    }
    catch (CliArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        Console.Error.WriteLine();
        Console.Error.WriteLine(CliOptions.Usage);
        return 1;
    }

    if (options.ShowHelp)
    {
        Console.WriteLine(CliOptions.Usage);
        return 0;
    }

    return await GenerateAsync(options);
}

static async Task<int> GenerateAsync(CliOptions options)
{
    using var workspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();
    workspace.RegisterWorkspaceFailedHandler(e =>
    {
        if (e.Diagnostic.Kind == Microsoft.CodeAnalysis.WorkspaceDiagnosticKind.Failure)
            Console.Error.WriteLine($"warning: {e.Diagnostic.Message}");
    });

    Console.Error.WriteLine($"Loading solution: {options.SolutionPath}");
    var solution = await workspace.OpenSolutionAsync(options.SolutionPath);

    Console.Error.WriteLine($"Resolving target method: {options.TargetMethod}");
    var resolver = new MethodTargetResolver(solution);
    var resolution = await resolver.ResolveAsync(options.TargetMethod, options.ParamTypes);

    if (!resolution.Success)
    {
        Console.Error.WriteLine(resolution.ErrorMessage);
        return 1;
    }

    List<string> diagrams;
    if (options.Direction == CallDirection.Incoming)
    {
        diagrams = await BuildIncomingDiagramsAsync(solution, resolution.Method!, options);
    }
    else
    {
        diagrams = [await BuildOutgoingDiagramAsync(solution, resolution.Method!, options)];
    }

    WriteDiagrams(diagrams, options.OutputPath);
    return 0;
}

static async Task<string> BuildOutgoingDiagramAsync(
    Microsoft.CodeAnalysis.Solution solution, Microsoft.CodeAnalysis.IMethodSymbol method, CliOptions options)
{
    Console.Error.WriteLine($"Building outgoing call graph (max depth {options.MaxDepth})...");
    var builder = new CallGraphBuilder(solution, options.MaxDepth, options.IncludeExternalCalls);
    var root = await builder.BuildAsync(method);
    return new PlantUmlSequenceWriter().Write(root);
}

static async Task<List<string>> BuildIncomingDiagramsAsync(
    Microsoft.CodeAnalysis.Solution solution, Microsoft.CodeAnalysis.IMethodSymbol method, CliOptions options)
{
    Console.Error.WriteLine($"Building incoming call hierarchy (max depth {options.MaxDepth})...");
    var builder = new IncomingCallGraphBuilder(solution, options.MaxDepth);
    var root = await builder.BuildAsync(method);

    var paths = root.EnumeratePaths();
    Console.Error.WriteLine($"Found {paths.Count} call chain(s) reaching the target method.");

    var writer = new PlantUmlIncomingPathWriter();
    var diagrams = new List<string>();
    for (var i = 0; i < paths.Count; i++)
    {
        var chain = paths[i];
        chain.Reverse(); // target-first -> chronological (entry point first)
        diagrams.Add(writer.Write(chain, i + 1, paths.Count));
    }

    return diagrams;
}

static void WriteDiagrams(IReadOnlyList<string> diagrams, string? outputPath)
{
    if (outputPath is null)
    {
        for (var i = 0; i < diagrams.Count; i++)
        {
            if (diagrams.Count > 1) Console.WriteLine($"' ---- path {i + 1} of {diagrams.Count} ----");
            Console.WriteLine(diagrams[i]);
        }

        return;
    }

    if (diagrams.Count == 1)
    {
        File.WriteAllText(outputPath, diagrams[0]);
        Console.Error.WriteLine($"Wrote {outputPath}");
        return;
    }

    var dir = Path.GetDirectoryName(outputPath);
    var stem = Path.GetFileNameWithoutExtension(outputPath);
    var ext = Path.GetExtension(outputPath);

    for (var i = 0; i < diagrams.Count; i++)
    {
        var path = Path.Combine(dir ?? "", $"{stem}.{i + 1}{ext}");
        File.WriteAllText(path, diagrams[i]);
        Console.Error.WriteLine($"Wrote {path}");
    }
}
