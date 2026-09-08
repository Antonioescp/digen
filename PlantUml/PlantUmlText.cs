using Microsoft.CodeAnalysis;

namespace digen_2.PlantUml;

/// <summary>Text formatting shared by every PlantUML sequence diagram writer.</summary>
internal static class PlantUmlText
{
    public static string? ReturnLabel(IMethodSymbol method)
    {
        if (method.MethodKind == MethodKind.Constructor) return null;
        var rt = method.ReturnType;
        if (rt.SpecialType == SpecialType.System_Void) return null;

        var name = rt.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        return name is "Task" or "ValueTask" ? null : name;
    }

    public static string Signature(IMethodSymbol method)
    {
        var name = method.MethodKind == MethodKind.Constructor ? "new " + method.ContainingType.Name : method.Name;
        var parameters = string.Join(", ",
            method.Parameters.Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        return Sanitize($"{name}({parameters})");
    }

    public static string Sanitize(string text) =>
        text.Replace('\n', ' ').Replace('\r', ' ')
            .Replace('<', '‹').Replace('>', '›')
            .Replace('"', '\'');
}
