# digen-2

Generates call-graph diagrams (PlantUML sequence diagrams today) from a real call graph, computed with Roslyn against a C# solution's actual semantics (not a regex/text scan). Point it at a `.sln`/`.slnx` and a target method, and it will either:

- walk **outward** from the method to show what it calls, recursively, or
- walk **inward** to show how the method is reached from its callers, up to entry points — like an IDE's "Call Hierarchy: Incoming Calls" view.

The source-language analysis and the diagram output format are both pluggable behind interfaces (see [Architecture](#architecture)) — C#/Roslyn and PlantUML are the first implementations, not the only ones the tool is built for.

## Requirements

- .NET SDK matching the project's `TargetFramework` (`net10.0`)
- The target solution must build (or at least restore) locally, since the tool loads it through MSBuild

## Build

```
dotnet build
```

## Usage

```
digen-2 <solution.sln> <Namespace.Type.Method> [options]
```

or, without a prior build:

```
dotnet run --project digen-2.csproj -- <solution.sln> <Namespace.Type.Method> [options]
```

### Target method

The full type name and method name, dot-separated:

```
MyApp.Services.OrderService.PlaceOrder
```

If the method is overloaded, disambiguate with a `(types)` suffix or `--params`:

```
MyApp.Services.OrderService.PlaceOrder(int,string)
```

### Options

| Flag | Description |
|---|---|
| `-r, --direction <dir>` | `outgoing` (default): what does the target method call, walked recursively. `incoming`: who calls the target method, walked up to entry points. Because a sequence diagram is one flow through time, each distinct call chain reaching the target is emitted as its own diagram. |
| `-l, --language <id>` | Analyzer to use (default: auto-detected from the solution file's extension). Currently just `csharp`. |
| `-f, --format <id>` | Diagram format to export (default: `plantuml`). |
| `-p, --params <types>` | Comma-separated parameter type names to disambiguate an overloaded method (alternative to the `(types)` suffix). |
| `-d, --depth <n>` | Maximum call-graph depth to follow (default: `8`). |
| `-o, --output <path>` | Write the diagram to a file instead of stdout. With `--direction incoming` and more than one call chain, each diagram is written next to this path with an inserted index, e.g. `out.puml` → `out.1.puml`, `out.2.puml`, ... |
| `--no-external` | Outgoing only: omit leaf calls into code with no source in the solution (BCL/NuGet calls). Included by default. |
| `-h, --help` | Show help text. |

## Examples

Outgoing — what does `PlaceOrder` call?

```
dotnet run -- MySolution.sln MyApp.Services.OrderService.PlaceOrder
```

Incoming — how is `Charge` reached, and from where?

```
dotnet run -- MySolution.sln MyApp.Payments.StripeGateway.Charge --direction incoming
```

Write to a file, capped at 4 hops:

```
dotnet run -- MySolution.sln MyApp.Services.OrderService.PlaceOrder -d 4 -o diagram.puml
```

Disambiguate an overload and drop framework/library noise:

```
dotnet run -- MySolution.sln MyApp.Services.OrderService.PlaceOrder(int,decimal) --no-external
```

Render the resulting `.puml` file with any PlantUML renderer (the [PlantUML VS Code extension](https://marketplace.visualstudio.com/items?itemName=jebbs.plantuml), the [online server](https://www.plantuml.com/plantuml/uml/), or `plantuml.jar`).

## Architecture

The tool is split into three layers so a new language or a new diagram format is additive, not a rewrite:

```
Core/                      language- and format-agnostic contracts and domain model
  CallableMethod.cs          a method's shape: type, name, parameters, return type
  CallGraphNode.cs           a tree of CallableMethod - the whole call graph
  ICodeAnalyzer.cs           loads a project, resolves a target, builds a CallGraphNode
  IDiagramExporter.cs        renders a CallGraphNode as diagram text

Analysis/CSharp/           ICodeAnalyzer implementation, built on Roslyn
  CSharpAnalyzer.cs           entry point: owns the MSBuildWorkspace lifecycle
  MethodTargetResolver.cs     "Namespace.Type.Method" -> IMethodSymbol
  CallGraphBuilder.cs         outgoing walk (what does it call)
  IncomingCallGraphBuilder.cs incoming walk (who calls it), via SymbolFinder.FindCallersAsync
  CallableMethodMapper.cs     IMethodSymbol -> CallableMethod

Diagrams/PlantUml/         IDiagramExporter implementation
  PlantUmlExporter.cs         renders a CallGraphNode as a PlantUML sequence diagram

Program.cs                 composition root: lists the available analyzers/exporters,
                            picks one of each by --language/--format (or by the solution
                            file's extension), and drives the CLI
```

`Program.cs` and the CLI only ever see `ICodeAnalyzer`, `IDiagramExporter`, and the `CallableMethod`/`CallGraphNode` domain model — nothing in that path knows about Roslyn or PlantUML syntax specifically.

### Adding a language (e.g. Java)

Implement `ICodeAnalyzer` in a new `Analysis/Java/JavaAnalyzer.cs`:

```csharp
public sealed class JavaAnalyzer : ICodeAnalyzer
{
    public string Id => "java";
    public bool CanAnalyze(string solutionPath) => solutionPath.EndsWith(".pom.xml") || /* ... */;
    public async Task<CallGraphResult> AnalyzeAsync(CallGraphRequest request) { /* ... */ }
}
```

`AnalyzeAsync` resolves the target method with whatever parser/model fits the language, walks its calls (or callers, per `request.Direction`), and returns a `CallGraphNode` tree built from `CallableMethod` values — the same generic shape the C# analyzer produces. Then register it in `Program.cs`'s `Analyzers()` list. Every existing diagram exporter works with it immediately, since exporters never see language-specific symbols.

### Adding a diagram format (e.g. Mermaid)

Implement `IDiagramExporter` in a new `Diagrams/Mermaid/MermaidExporter.cs`:

```csharp
public sealed class MermaidExporter : IDiagramExporter
{
    public string Id => "mermaid";
    public string RenderOutgoing(CallGraphNode root) { /* ... */ }
    public string RenderIncomingPath(IReadOnlyList<CallGraphNode> chronologicalChain, int pathIndex, int pathCount) { /* ... */ }
}
```

Register it in `Program.cs`'s `Exporters()` list, then select it with `--format mermaid`. Every existing analyzer produces output it can render.

## How the C# analyzer resolves calls

- **DI-style interface/abstraction calls** (`outgoing` direction): when a call targets an interface method, the tool looks for concrete implementations in the solution. A single implementation is followed automatically (noted as `via <Type>` in the diagram); multiple implementations are left unexpanded with a note, since the runtime type can't be determined statically.
- **Calls inside lambdas** (e.g. `items.ForEach(x => _service.DoWork(x))`) are picked up in both directions — the outgoing walker descends into lambda bodies, and the incoming walker (via Roslyn's `FindCallersAsync`) attributes the call site back to its enclosing method. A lambda that's *stored* (a field, an event subscription, a `Func<>`/`Action<>` variable) and invoked from a separate method is not tracked across that indirection — the diagram will show the code that writes the call, not the code that eventually triggers the stored delegate.
- **Recursion and cycles** are detected and truncated with a note rather than looping forever.
- Diagrams are capped by `--depth` and by an internal safety limit on total nodes, to keep large or highly-connected codebases from producing unusable output.
