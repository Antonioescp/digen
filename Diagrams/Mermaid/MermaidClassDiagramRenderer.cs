using System.Text;
using digen_2.Core;
using digen_2.Diagrams;

namespace digen_2.Diagrams.Mermaid;

/// <summary>Renders a ClassDiagramModel as a Mermaid class diagram.</summary>
internal static class MermaidClassDiagramRenderer
{
    public static string Render(ClassDiagramModel model)
    {
        var registry = new TypeAliasRegistry();
        foreach (var type in model.Types) registry.Register(type.FullName, type.Name);

        var sb = new StringBuilder();
        sb.AppendLine("classDiagram");

        foreach (var type in model.Types)
        {
            WriteType(type, registry, sb);
        }

        foreach (var relation in model.Relations)
        {
            var fromAlias = registry.TryGetAlias(relation.FromTypeFullName);
            var toAlias = registry.TryGetAlias(relation.ToTypeFullName);
            if (fromAlias is null || toAlias is null) continue;

            var arrow = relation.Kind switch
            {
                TypeRelationKind.Inheritance => "--|>",
                TypeRelationKind.Implementation => "..|>",
                _ => "-->",
            };
            sb.AppendLine($"    {fromAlias} {arrow} {toAlias}");
        }

        return sb.ToString();
    }

    private static void WriteType(ClassDiagramType type, TypeAliasRegistry registry, StringBuilder sb)
    {
        var alias = registry.Alias(type.FullName, type.Name);
        var label = MermaidText.Sanitize(registry.Label(type.FullName, type.Name));
        var stereotype = type.Shape switch
        {
            TypeShape.Interface => "interface",
            TypeShape.Enum => "enumeration",
            TypeShape.Record => "record",
            TypeShape.Struct => "struct",
            _ => null,
        };

        sb.AppendLine($"    class {alias}[\"{label}\"] {{");

        if (stereotype is not null)
        {
            sb.AppendLine($"        <<{stereotype}>>");
        }

        foreach (var field in type.Fields)
        {
            sb.AppendLine($"        {MermaidText.Sanitize(field.Signature)}");
        }

        foreach (var method in type.Methods)
        {
            sb.AppendLine($"        {MermaidText.Sanitize(method.Signature)}");
        }

        sb.AppendLine("    }");
    }
}
