namespace digen_2.Core;

/// <summary>
/// Language-agnostic description of a method, carrying just enough
/// information for a diagram exporter to render it. Produced by an
/// <see cref="ICodeAnalyzer"/>; never tied to a specific language's
/// symbol model (Roslyn, a Java parser, etc).
/// </summary>
public sealed record CallableMethod(
    string TypeName,
    string TypeFullName,
    string MethodName,
    IReadOnlyList<string> ParameterTypes,
    string? ReturnType,
    bool IsConstructor = false);
