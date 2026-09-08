using digen_2.Core;

namespace digen_2.Cli;

public sealed class CliArgumentException(string message) : Exception(message);

public sealed class CliOptions
{
    public required string SolutionPath { get; init; }

    /// <summary>For --kind sequence: "Namespace.Type.Method". For --kind class: "Namespace.Type".</summary>
    public required string Target { get; init; }

    public string[]? ParamTypes { get; init; }
    public int MaxDepth { get; init; } = 8;
    public string? OutputPath { get; init; }
    public bool IncludeExternalCalls { get; init; } = true;
    public CallDirection Direction { get; init; } = CallDirection.Outgoing;
    public DiagramKind Kind { get; init; } = DiagramKind.Sequence;

    /// <summary>--language value, or null to auto-detect from the solution path's extension.</summary>
    public string? Language { get; init; }

    public string Format { get; init; } = "plantuml";
    public bool ShowHelp { get; init; }

    public const string Usage =
        """
        digen-2 - Generate call-graph and class diagrams from a codebase

        Usage:
          digen-2 <solution.sln> <target> [options]

        Target (shape depends on --kind):
          sequence (default): the full type name and method name, dot-separated:
            MyApp.Services.OrderService.PlaceOrder
          Optionally specify parameter types to disambiguate overloads:
            MyApp.Services.OrderService.PlaceOrder(int,string)
          class: just the full type name:
            MyApp.Services.OrderService

        Options:
          -k, --kind <kind>        sequence (default): a call graph, rooted at a
                                    target method.
                                    class: types, members, and the relationships
                                    (inheritance, implementation, association)
                                    between them, rooted at a target type.
          -r, --direction <dir>    Sequence only. outgoing (default): what does the
                                    target method call, walked recursively.
                                    incoming: who calls the target method, walked
                                    up to entry points (like an IDE's "Incoming
                                    Calls" hierarchy). Since a sequence diagram is
                                    one flow through time, each distinct call chain
                                    reaching the target is emitted as its own
                                    diagram.
          -l, --language <id>      Analyzer to use (default: auto-detected from the
                                    solution file's extension), e.g. 'csharp'.
          -f, --format <id>        Diagram format to export (default: plantuml).
          -p, --params <types>     Sequence only. Comma-separated parameter type
                                    names used to disambiguate an overloaded method
                                    (alternative to the (types) suffix above).
          -d, --depth <n>          Maximum graph depth to follow (default: 8).
          -o, --output <path>      Write the diagram to a file instead of stdout.
                                    With --direction incoming and more than one
                                    call chain, each diagram is written next to
                                    this path with an inserted index, e.g.
                                    out.puml -> out.1.puml, out.2.puml, ...
          --no-external            Sequence, outgoing only: omit leaf calls into
                                    code with no source in the solution (BCL/NuGet
                                    calls). Included by default.
          -h, --help                Show this help text.
        """;

    public static CliOptions Parse(string[] args)
    {
        if (args.Length == 0 || args.Any(a => a is "-h" or "--help"))
        {
            return new CliOptions { SolutionPath = "", Target = "", ShowHelp = true };
        }

        var positional = new List<string>();
        string[]? paramTypes = null;
        var maxDepth = 8;
        string? output = null;
        var includeExternal = true;
        var direction = CallDirection.Outgoing;
        var kind = DiagramKind.Sequence;
        string? language = null;
        var format = "plantuml";

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-p":
                case "--params":
                    paramTypes = RequireValue(args, ref i, "--params")
                        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    break;
                case "-d":
                case "--depth":
                    var raw = RequireValue(args, ref i, "--depth");
                    if (!int.TryParse(raw, out maxDepth) || maxDepth < 1)
                        throw new CliArgumentException($"Invalid value for --depth: '{raw}'. Expected a positive integer.");
                    break;
                case "-o":
                case "--output":
                    output = RequireValue(args, ref i, "--output");
                    break;
                case "--no-external":
                    includeExternal = false;
                    break;
                case "-r":
                case "--direction":
                    var dir = RequireValue(args, ref i, "--direction");
                    direction = dir.ToLowerInvariant() switch
                    {
                        "outgoing" or "out" => CallDirection.Outgoing,
                        "incoming" or "in" => CallDirection.Incoming,
                        _ => throw new CliArgumentException(
                            $"Invalid value for --direction: '{dir}'. Expected 'outgoing' or 'incoming'."),
                    };
                    break;
                case "-k":
                case "--kind":
                    var kindRaw = RequireValue(args, ref i, "--kind");
                    kind = kindRaw.ToLowerInvariant() switch
                    {
                        "sequence" or "seq" => DiagramKind.Sequence,
                        "class" => DiagramKind.Class,
                        _ => throw new CliArgumentException(
                            $"Invalid value for --kind: '{kindRaw}'. Expected 'sequence' or 'class'."),
                    };
                    break;
                case "-l":
                case "--language":
                    language = RequireValue(args, ref i, "--language");
                    break;
                case "-f":
                case "--format":
                    format = RequireValue(args, ref i, "--format");
                    break;
                default:
                    positional.Add(args[i]);
                    break;
            }
        }

        if (positional.Count < 2)
            throw new CliArgumentException("Expected a solution path and a target.");
        if (positional.Count > 2)
            throw new CliArgumentException($"Unexpected argument: '{positional[2]}'.");

        var solutionPath = positional[0];
        var target = positional[1];

        if (kind == DiagramKind.Sequence)
        {
            // Allow "Type.Method(int,string)" as an alternative to --params.
            var parenIndex = target.IndexOf('(');
            if (parenIndex >= 0)
            {
                if (!target.EndsWith(')'))
                    throw new CliArgumentException($"Malformed target method signature: '{target}'.");
                var inner = target[(parenIndex + 1)..^1];
                paramTypes = inner.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                target = target[..parenIndex];
            }
        }

        if (!File.Exists(solutionPath))
            throw new CliArgumentException($"Solution file not found: '{solutionPath}'.");

        return new CliOptions
        {
            SolutionPath = solutionPath,
            Target = target,
            ParamTypes = paramTypes,
            MaxDepth = maxDepth,
            OutputPath = output,
            IncludeExternalCalls = includeExternal,
            Direction = direction,
            Kind = kind,
            Language = language,
            Format = format,
        };
    }

    private static string RequireValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
            throw new CliArgumentException($"Missing value for {flag}.");
        return args[++i];
    }
}
