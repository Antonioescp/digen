using digen_2.Core;
using Microsoft.CodeAnalysis;

namespace digen_2.Analysis.CSharp;

/// <summary>Maps a Roslyn IMethodSymbol to the language-agnostic CallableMethod used by diagram exporters.</summary>
internal static class CallableMethodMapper
{
    public static CallableMethod Map(IMethodSymbol method) => new(
        TypeName: method.ContainingType.Name,
        TypeFullName: method.ContainingType.ToDisplayString(),
        MethodName: method.Name,
        ParameterTypes: method.Parameters
            .Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
            .ToList(),
        ReturnType: ReturnLabel(method),
        IsConstructor: method.MethodKind == MethodKind.Constructor);

    private static string? ReturnLabel(IMethodSymbol method)
    {
        if (method.MethodKind == MethodKind.Constructor) return null;
        var rt = method.ReturnType;
        if (rt.SpecialType == SpecialType.System_Void) return null;

        var name = rt.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        return name is "Task" or "ValueTask" ? null : name;
    }
}
