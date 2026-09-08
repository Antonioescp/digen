using digen_2.Core;

namespace digen_2.Diagrams.Mermaid;

/// <summary>Text formatting shared across the Mermaid exporter.</summary>
internal static class MermaidText
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
