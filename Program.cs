using digen_2.Analysis.CSharp;
using digen_2.Cli;
using digen_2.Core;
using digen_2.Diagrams.Mermaid;
using digen_2.Diagrams.PlantUml;
using Microsoft.Build.Locator;

MSBuildLocator.RegisterDefaults();
return await RunAsync(args);

// Composition root: register available analyzers/exporters here. Adding a
// new language (e.g. Java) or diagram format means writing an
// ICodeAnalyzer / IDiagramExporter implementation (plus one capability
// interface per DiagramKind it supports) and adding one line below.
static IReadOnlyList<ICodeAnalyzer> Analyzers() => [new CSharpAnalyzer()];
static IReadOnlyList<IDiagramExporter> Exporters() => [new PlantUmlExporter(), new MermaidExporter()];

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

    var analyzers = Analyzers();
    var analyzer = options.Language is not null
        ? analyzers.FirstOrDefault(a => string.Equals(a.Id, options.Language, StringComparison.OrdinalIgnoreCase))
        : analyzers.FirstOrDefault(a => a.CanAnalyze(options.SolutionPath));

    if (analyzer is null)
    {
        Console.Error.WriteLine(options.Language is not null
            ? $"Unknown --language '{options.Language}'. Available: {string.Join(", ", analyzers.Select(a => a.Id))}"
            : $"No analyzer recognizes '{options.SolutionPath}'. Specify one with --language. Available: {string.Join(", ", analyzers.Select(a => a.Id))}");
        return 1;
    }

    var exporters = Exporters();
    var exporter = exporters.FirstOrDefault(e => string.Equals(e.Id, options.Format, StringComparison.OrdinalIgnoreCase));
    if (exporter is null)
    {
        Console.Error.WriteLine(
            $"Unknown --format '{options.Format}'. Available: {string.Join(", ", exporters.Select(e => e.Id))}");
        return 1;
    }

    if (!analyzer.SupportedKinds.Contains(options.Kind))
    {
        Console.Error.WriteLine(
            $"Analyzer '{analyzer.Id}' doesn't support --kind {FormatKind(options.Kind)}. Supported: {string.Join(", ", analyzer.SupportedKinds.Select(FormatKind))}");
        return 1;
    }

    if (!exporter.SupportedKinds.Contains(options.Kind))
    {
        Console.Error.WriteLine(
            $"Format '{exporter.Id}' doesn't support --kind {FormatKind(options.Kind)}. Supported: {string.Join(", ", exporter.SupportedKinds.Select(FormatKind))}");
        return 1;
    }

    return options.Kind switch
    {
        DiagramKind.Class => await GenerateClassDiagramAsync(options, (IClassDiagramAnalyzer)analyzer, (IClassDiagramExporter)exporter),
        _ => await GenerateSequenceDiagramAsync(options, (ISequenceDiagramAnalyzer)analyzer, (ISequenceDiagramExporter)exporter),
    };
}

static string FormatKind(DiagramKind kind) => kind.ToString().ToLowerInvariant();

static async Task<int> GenerateSequenceDiagramAsync(CliOptions options, ISequenceDiagramAnalyzer analyzer, ISequenceDiagramExporter exporter)
{
    var request = new CallGraphRequest(
        options.SolutionPath,
        options.Target,
        options.ParamTypes,
        options.Direction,
        options.MaxDepth,
        options.IncludeExternalCalls);

    var result = await analyzer.AnalyzeAsync(request);
    if (!result.Success)
    {
        Console.Error.WriteLine(result.ErrorMessage);
        return 1;
    }

    List<string> diagrams = options.Direction == CallDirection.Incoming
        ? RenderIncoming(result.Root!, exporter)
        : [exporter.RenderOutgoing(result.Root!)];

    WriteDiagrams(diagrams, exporter, options.OutputPath);
    return 0;
}

static async Task<int> GenerateClassDiagramAsync(CliOptions options, IClassDiagramAnalyzer analyzer, IClassDiagramExporter exporter)
{
    var request = new ClassDiagramRequest(options.SolutionPath, options.Target, options.MaxDepth);

    var result = await analyzer.AnalyzeAsync(request);
    if (!result.Success)
    {
        Console.Error.WriteLine(result.ErrorMessage);
        return 1;
    }

    WriteDiagrams([exporter.Render(result.Model!)], exporter, options.OutputPath);
    return 0;
}

static List<string> RenderIncoming(CallGraphNode root, ISequenceDiagramExporter exporter)
{
    var paths = root.EnumeratePaths();
    Console.Error.WriteLine($"Found {paths.Count} call chain(s) reaching the target method.");

    var diagrams = new List<string>();
    for (var i = 0; i < paths.Count; i++)
    {
        var chain = paths[i];
        chain.Reverse(); // target-first -> chronological (entry point first)
        diagrams.Add(exporter.RenderIncomingPath(chain, i + 1, paths.Count));
    }

    return diagrams;
}

static void WriteDiagrams(IReadOnlyList<string> diagrams, IDiagramExporter exporter, string? outputPath)
{
    if (outputPath is null)
    {
        for (var i = 0; i < diagrams.Count; i++)
        {
            if (diagrams.Count > 1) Console.WriteLine(exporter.FormatComment($"---- path {i + 1} of {diagrams.Count} ----"));
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
