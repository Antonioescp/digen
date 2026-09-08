namespace digen_2.Cli;

public sealed class CliArgumentException(string message) : Exception(message);

public enum CallDirection
{
    /// <summary>What does the target method call? (default)</summary>
    Outgoing,

    /// <summary>Who calls the target method, up to entry points? One diagram per call chain.</summary>
    Incoming,
}

public sealed class CliOptions
{
    public required string SolutionPath { get; init; }
    public required string TargetMethod { get; init; }
    public string[]? ParamTypes { get; init; }
    public int MaxDepth { get; init; } = 8;
    public string? OutputPath { get; init; }
    public bool IncludeExternalCalls { get; init; } = true;
    public CallDirection Direction { get; init; } = CallDirection.Outgoing;
    public bool ShowHelp { get; init; }

    public const string Usage =
        """
        digen-2 - Generate PlantUML sequence diagrams from a C# call graph

        Usage:
          digen-2 <solution.sln> <Namespace.Type.Method> [options]

        Target method:
          The full type name and method name, dot-separated, e.g.
            MyApp.Services.OrderService.PlaceOrder
          Optionally specify parameter types to disambiguate overloads:
            MyApp.Services.OrderService.PlaceOrder(int,string)

        Options:
          -r, --direction <dir>    outgoing (default): what does the target method
                                    call, walked recursively.
                                    incoming: who calls the target method, walked
                                    up to entry points (like an IDE's "Incoming
                                    Calls" hierarchy). Since a sequence diagram is
                                    one flow through time, each distinct call chain
                                    reaching the target is emitted as its own
                                    diagram.
          -p, --params <types>     Comma-separated parameter type names used to
                                    disambiguate an overloaded method (alternative
                                    to the (types) suffix above).
          -d, --depth <n>          Maximum call-graph depth to follow (default: 8).
          -o, --output <path>      Write the diagram to a file instead of stdout.
                                    With --direction incoming and more than one
                                    call chain, each diagram is written next to
                                    this path with an inserted index, e.g.
                                    out.puml -> out.1.puml, out.2.puml, ...
          --no-external            Outgoing only: omit leaf calls into code with
                                    no source in the solution (BCL/NuGet calls).
                                    Included by default.
          -h, --help                Show this help text.
        """;

    public static CliOptions Parse(string[] args)
    {
        if (args.Length == 0 || args.Any(a => a is "-h" or "--help"))
        {
            return new CliOptions { SolutionPath = "", TargetMethod = "", ShowHelp = true };
        }

        var positional = new List<string>();
        string[]? paramTypes = null;
        var maxDepth = 8;
        string? output = null;
        var includeExternal = true;
        var direction = CallDirection.Outgoing;

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
                default:
                    positional.Add(args[i]);
                    break;
            }
        }

        if (positional.Count < 2)
            throw new CliArgumentException("Expected a solution path and a target method.");
        if (positional.Count > 2)
            throw new CliArgumentException($"Unexpected argument: '{positional[2]}'.");

        var solutionPath = positional[0];
        var targetMethod = positional[1];

        // Allow "Type.Method(int,string)" as an alternative to --params.
        var parenIndex = targetMethod.IndexOf('(');
        if (parenIndex >= 0)
        {
            if (!targetMethod.EndsWith(')'))
                throw new CliArgumentException($"Malformed target method signature: '{targetMethod}'.");
            var inner = targetMethod[(parenIndex + 1)..^1];
            paramTypes = inner.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            targetMethod = targetMethod[..parenIndex];
        }

        if (!File.Exists(solutionPath))
            throw new CliArgumentException($"Solution file not found: '{solutionPath}'.");

        return new CliOptions
        {
            SolutionPath = solutionPath,
            TargetMethod = targetMethod,
            ParamTypes = paramTypes,
            MaxDepth = maxDepth,
            OutputPath = output,
            IncludeExternalCalls = includeExternal,
            Direction = direction,
        };
    }

    private static string RequireValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
            throw new CliArgumentException($"Missing value for {flag}.");
        return args[++i];
    }
}
