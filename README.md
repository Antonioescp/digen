# digen-2

Generates diagrams (PlantUML or Mermaid) from a codebase's real semantics, computed with Roslyn against a C# solution (not a regex/text scan). Point it at a `.sln`/`.slnx` and a target, and it will generate one of two kinds of diagram:

- **sequence** (default) — walk **outward** from a target method to show what it calls, recursively, or walk **inward** to show how the method is reached from its callers, up to entry points, like an IDE's "Call Hierarchy: Incoming Calls" view.
- **class** — starting from a target type, show it and the types it's related to (base type, implemented interfaces, and types referenced by its fields/properties/method signatures), with their members and the relationships between them.

The source-language analysis, the diagram kind, and the diagram output format are all pluggable behind interfaces (see [Architecture](#architecture)) — C#/Roslyn, sequence/class, and PlantUML/Mermaid are the current implementations, not the only ones the tool is built for.

## Requirements

- .NET SDK matching the project's `TargetFramework` (`net10.0`)
- The target solution must build (or at least restore) locally, since the tool loads it through MSBuild

## Build

```
dotnet build
```

## Usage

```
digen-2 <solution.sln> <target> [options]
```

or, without a prior build:

```
dotnet run --project digen-2.csproj -- <solution.sln> <target> [options]
```

### Target

The target's shape depends on `--kind`:

- `sequence` (default): the full type name and method name, dot-separated:
  ```
  MyApp.Services.OrderService.PlaceOrder
  ```
  If the method is overloaded, disambiguate with a `(types)` suffix or `--params`:
  ```
  MyApp.Services.OrderService.PlaceOrder(int,string)
  ```
- `class`: just the full type name:
  ```
  MyApp.Services.OrderService
  ```

### Options

| Flag | Description |
|---|---|
| `-k, --kind <kind>` | `sequence` (default): a call graph, rooted at a target method. `class`: types, members, and relationships (inheritance, implementation, association), rooted at a target type. |
| `-r, --direction <dir>` | Sequence only. `outgoing` (default): what does the target method call, walked recursively. `incoming`: who calls the target method, walked up to entry points. Because a sequence diagram is one flow through time, each distinct call chain reaching the target is emitted as its own diagram. |
| `-l, --language <id>` | Analyzer to use (default: auto-detected from the solution file's extension). Currently just `csharp`. |
| `-f, --format <id>` | Diagram format to export (default: `plantuml`). Also `mermaid`. |
| `-p, --params <types>` | Sequence only. Comma-separated parameter type names to disambiguate an overloaded method (alternative to the `(types)` suffix). |
| `-d, --depth <n>` | Maximum graph depth to follow (default: `8`). |
| `-o, --output <path>` | Write the diagram to a file instead of stdout. With `--direction incoming` and more than one call chain, each diagram is written next to this path with an inserted index, e.g. `out.puml` → `out.1.puml`, `out.2.puml`, ... |
| `--no-external` | Sequence, outgoing only: omit leaf calls into code with no source in the solution (BCL/NuGet calls). Included by default. |
| `-h, --help` | Show help text. |

## Examples

Outgoing sequence — what does `PlaceOrder` call?

```
dotnet run -- MySolution.sln MyApp.Services.OrderService.PlaceOrder
```

Incoming sequence — how is `Charge` reached, and from where?

```
dotnet run -- MySolution.sln MyApp.Payments.StripeGateway.Charge --direction incoming
```

Class diagram for a type and its immediate neighborhood:

```
dotnet run -- MySolution.sln MyApp.Services.OrderService --kind class
```

Write to a file, capped at 4 hops:

```
dotnet run -- MySolution.sln MyApp.Services.OrderService.PlaceOrder -d 4 -o diagram.puml
```

Disambiguate an overload and drop framework/library noise:

```
dotnet run -- MySolution.sln MyApp.Services.OrderService.PlaceOrder(int,decimal) --no-external
```

Same class diagram, as Mermaid instead of PlantUML:

```
dotnet run -- MySolution.sln MyApp.Services.OrderService --kind class --format mermaid
```

Render the resulting `.puml` file with any PlantUML renderer (the [PlantUML VS Code extension](https://marketplace.visualstudio.com/items?itemName=jebbs.plantuml), the [online server](https://www.plantuml.com/plantuml/uml/), or `plantuml.jar`). Render a `--format mermaid` result with the [Mermaid Live Editor](https://mermaid.live/), a `` ```mermaid `` fenced block in Markdown (GitHub, GitLab, and most editors render these inline), or the [Mermaid VS Code extension](https://marketplace.visualstudio.com/items?itemName=bierner.markdown-mermaid).

## Architecture

The tool is split into three layers so a new language, diagram kind, or output format is additive, not a rewrite:

```
Core/                        language-, kind-, and format-agnostic contracts and domain model
  DiagramKind.cs                enum: Sequence, Class
  ICodeAnalyzer.cs              base: Id, CanAnalyze, SupportedKinds
  ISequenceDiagramAnalyzer.cs     capability: builds a CallGraphNode from a target method
  IClassDiagramAnalyzer.cs        capability: builds a ClassDiagramModel from a target type
  CallableMethod.cs, CallGraphNode.cs      sequence domain model (a method; a tree of them)
  ClassDiagramModel.cs                     class domain model (types, members, relations)
  IDiagramExporter.cs           base: Id, SupportedKinds
  ISequenceDiagramExporter.cs     capability: renders a CallGraphNode
  IClassDiagramExporter.cs        capability: renders a ClassDiagramModel

Analysis/CSharp/              ICodeAnalyzer implementation, built on Roslyn
  CSharpAnalyzer.cs              entry point: owns the MSBuildWorkspace lifecycle,
                                  implements both analyzer capabilities
  MethodTargetResolver.cs        "Namespace.Type.Method" -> IMethodSymbol
  SolutionTypeLookup.cs          "Namespace.Type" -> INamedTypeSymbol (shared)
  CallGraphBuilder.cs            outgoing walk (what does it call)
  IncomingCallGraphBuilder.cs    incoming walk (who calls it), via SymbolFinder.FindCallersAsync
  CallableMethodMapper.cs        IMethodSymbol -> CallableMethod
  ClassDiagramBuilder.cs         BFS over type relations (base type, interfaces, member types)

Diagrams/TypeAliasRegistry.cs shared by every exporter: stable per-diagram entity aliasing
                               (raw labels only - each renderer applies its own escaping)

Diagrams/PlantUml/            IDiagramExporter implementation, implements both exporter capabilities
  PlantUmlExporter.cs            thin dispatcher to the two renderers below
  PlantUmlSequenceRenderer.cs    CallGraphNode -> PlantUML sequence diagram
  PlantUmlClassDiagramRenderer.cs ClassDiagramModel -> PlantUML class diagram
  PlantUmlText.cs                PlantUML-specific escaping/signature formatting

Diagrams/Mermaid/              IDiagramExporter implementation, implements both exporter capabilities
  MermaidExporter.cs             thin dispatcher to the two renderers below
  MermaidSequenceRenderer.cs     CallGraphNode -> Mermaid sequence diagram
  MermaidClassDiagramRenderer.cs ClassDiagramModel -> Mermaid class diagram
  MermaidText.cs                 Mermaid-specific escaping/signature formatting

Program.cs                    composition root: lists the available analyzers/exporters,
                               picks one of each by --language/--format (or by the solution
                               file's extension), checks both support the requested --kind,
                               and drives the CLI
```

`Program.cs` and the CLI only ever see the `Core` interfaces and domain models — nothing in that path knows about Roslyn, PlantUML, or Mermaid syntax specifically.

### Why "kind" is a capability interface, not a flag on one big interface

A sequence diagram's input (a call graph rooted at a method) and a class diagram's input (a set of types, members, and relations rooted at a type) are structurally unrelated - there's no shared shape to shove into one `Analyze(request) -> result` method. So `ICodeAnalyzer` and `IDiagramExporter` are just markers (`Id`, `CanAnalyze`/`SupportedKinds`); the actual work lives in one capability interface per kind (`ISequenceDiagramAnalyzer`, `IClassDiagramAnalyzer`, and their exporter counterparts) that a concrete class implements only for the kinds it supports. `Program.cs` checks `SupportedKinds` and casts to the matching capability interface. An analyzer or exporter that only supports one kind simply doesn't implement the other interface - `Program.cs` reports it as unsupported rather than throwing.

### Adding a diagram kind (e.g. a package/dependency diagram)

1. In `Core/`, add the domain model (e.g. `PackageDiagramModel.cs`) and the two capability interfaces (`IPackageDiagramAnalyzer` with a `Request`/`Result` pair, `IPackageDiagramExporter`), following the `IClassDiagramAnalyzer`/`IClassDiagramExporter` pattern. Add the new case to `DiagramKind`.
2. Implement the new analyzer interface on `CSharpAnalyzer` (or any analyzer), add `DiagramKind.Package` to its `SupportedKinds`.
3. Implement the new exporter interface on `PlantUmlExporter` (or any exporter), add `DiagramKind.Package` to its `SupportedKinds`.
4. Wire the CLI: add the `--kind` value in `CliOptions`, and a `GeneratePackageDiagramAsync` branch in `Program.cs`'s kind switch.

### Adding a language (e.g. Java)

Implement `ICodeAnalyzer` plus whichever capability interfaces it supports (e.g. `ISequenceDiagramAnalyzer`) in a new `Analysis/Java/JavaAnalyzer.cs`:

```csharp
public sealed class JavaAnalyzer : ISequenceDiagramAnalyzer
{
    public string Id => "java";
    public bool CanAnalyze(string solutionPath) => solutionPath.EndsWith(".pom.xml") || /* ... */;
    public IReadOnlyCollection<DiagramKind> SupportedKinds { get; } = [DiagramKind.Sequence];
    public async Task<CallGraphResult> AnalyzeAsync(CallGraphRequest request) { /* ... */ }
}
```

`AnalyzeAsync` resolves the target method with whatever parser/model fits the language, walks its calls (or callers, per `request.Direction`), and returns a `CallGraphNode` tree built from `CallableMethod` values — the same generic shape the C# analyzer produces. Then register it in `Program.cs`'s `Analyzers()` list. Every exporter that supports `Sequence` works with it immediately, since exporters never see language-specific symbols.

### Adding a diagram format (e.g. GraphViz DOT)

`Diagrams/Mermaid/` is a complete second `IDiagramExporter` and the reference example to copy: implement `IDiagramExporter` plus whichever capability interfaces you support in a new `Diagrams/Dot/DotExporter.cs`:

```csharp
public sealed class DotExporter : ISequenceDiagramExporter, IClassDiagramExporter
{
    public string Id => "dot";
    public IReadOnlyCollection<DiagramKind> SupportedKinds { get; } = [DiagramKind.Sequence, DiagramKind.Class];
    public string RenderOutgoing(CallGraphNode root) { /* ... */ }
    public string RenderIncomingPath(IReadOnlyList<CallGraphNode> chronologicalChain, int pathIndex, int pathCount) { /* ... */ }
    public string Render(ClassDiagramModel model) { /* ... */ }

    // Override only if this format's multi-diagram-to-stdout separator isn't a PlantUML-style "' text" comment.
    public string FormatComment(string text) => $"// {text}";
}
```

Register it in `Program.cs`'s `Exporters()` list, then select it with `--format dot`. Every analyzer that supports the requested kind produces output it can render.

## How the C# analyzer resolves calls (sequence diagrams)

- **DI-style interface/abstraction calls** (`outgoing` direction): when a call targets an interface method, the tool looks for concrete implementations in the solution. A single implementation is followed automatically (noted as `via <Type>` in the diagram); multiple implementations are left unexpanded with a note, since the runtime type can't be determined statically.
- **Calls inside lambdas** (e.g. `items.ForEach(x => _service.DoWork(x))`) are picked up in both directions — the outgoing walker descends into lambda bodies, and the incoming walker (via Roslyn's `FindCallersAsync`) attributes the call site back to its enclosing method. A lambda that's *stored* (a field, an event subscription, a `Func<>`/`Action<>` variable) and invoked from a separate method is not tracked across that indirection — the diagram will show the code that writes the call, not the code that eventually triggers the stored delegate.
- **Recursion and cycles** are detected and truncated with a note rather than looping forever.
- Diagrams are capped by `--depth` and by an internal safety limit on total nodes, to keep large or highly-connected codebases from producing unusable output.

## How the C# analyzer builds class diagrams

Starting at the target type, it breadth-first walks: the base type (if not `object`), implemented interfaces, and the types of fields, properties, method parameters, and return types — unwrapping generics and arrays, so `List<OrderItem> Items` links to `OrderItem` even though `List<T>` itself is a BCL type with no source. Only types with source in the solution are included or expanded further; BCL/NuGet types are invisible to this walk (they'd add noise without adding a diagram-relevant node). The walk is capped by `--depth` and by an internal cap of 40 types.

## Format notes

- **PlantUML** sequence diagrams use a "message from outside" arrow (`[->`) for the diagram's entry point, since PlantUML supports it directly.
- **Mermaid** has no such construct, so the entry point is rendered as a self-message on the root participant instead (`P0->>P0: PlaceOrder(...)`) — visually the same idea (a call originating outside the diagram), expressed the way Mermaid diagrams commonly do it. Mermaid sequence diagrams also carry their title as YAML frontmatter (`---\ntitle: "..."\n---`) rather than PlantUML's `title` keyword.
- Both formats render the same underlying `CallGraphNode`/`ClassDiagramModel`, so a diagram's *content* (which calls, notes, members, relations are shown) is identical between `--format plantuml` and `--format mermaid` — only the syntax differs.
