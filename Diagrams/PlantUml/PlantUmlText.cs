using digen_2.Core;

namespace digen_2.Diagrams.PlantUml;

/// <summary>Text formatting shared across the PlantUML exporter.</summary>
internal static class PlantUmlText
{
    public static string? ReturnLabel(CallableMethod method) =>
        method.ReturnType is null ? null : Sanitize(method.ReturnType);

    public static string Signature(CallableMethod method)
    {
        var name = method.IsConstructor ? "new " + method.TypeName : method.MethodName;
        var parameters = string.Join(", ", method.ParameterTypes);
        return Sanitize($"{name}({parameters})");
    }

    public static string Sanitize(string text) =>
        text.Replace('\n', ' ').Replace('\r', ' ')
            .Replace('<', '‹').Replace('>', '›')
            .Replace('"', '\'');
}
