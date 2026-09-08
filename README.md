# digen-2

Generates [PlantUML](https://plantuml.com/sequence-diagram) sequence diagrams from a real C# call graph, computed with Roslyn against a solution's actual semantics (not a regex/text scan). Point it at a `.sln`/`.slnx` and a target method, and it will either:

- walk **outward** from the method to show what it calls, recursively, or
- walk **inward** to show how the method is reached from its callers, up to entry points — like an IDE's "Call Hierarchy: Incoming Calls" view.

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

## How it resolves calls

- **DI-style interface/abstraction calls** (`outgoing` direction): when a call targets an interface method, the tool looks for concrete implementations in the solution. A single implementation is followed automatically (noted as `via <Type>` in the diagram); multiple implementations are left unexpanded with a note, since the runtime type can't be determined statically.
- **Calls inside lambdas** (e.g. `items.ForEach(x => _service.DoWork(x))`) are picked up in both directions — the outgoing walker descends into lambda bodies, and the incoming walker (via Roslyn's `FindCallersAsync`) attributes the call site back to its enclosing method. A lambda that's *stored* (a field, an event subscription, a `Func<>`/`Action<>` variable) and invoked from a separate method is not tracked across that indirection — the diagram will show the code that writes the call, not the code that eventually triggers the stored delegate.
- **Recursion and cycles** are detected and truncated with a note rather than looping forever.
- Diagrams are capped by `--depth` and by an internal safety limit on total nodes, to keep large or highly-connected codebases from producing unusable output.
