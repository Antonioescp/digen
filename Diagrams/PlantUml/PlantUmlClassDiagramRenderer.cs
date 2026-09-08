using System.Text;
using digen_2.Core;

namespace digen_2.Diagrams.PlantUml;

/// <summary>Renders a ClassDiagramModel as a PlantUML class diagram.</summary>
internal static class PlantUmlClassDiagramRenderer
{
    public static string Render(ClassDiagramModel model)
    {
        var registry = new TypeAliasRegistry();
        foreach (var type in model.Types) registry.Register(type.FullName, type.Name);

        var sb = new StringBuilder();
        sb.AppendLine("@startuml");
        sb.AppendLine("skinparam classAttributeIconSize 0");
        sb.AppendLine();

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
            sb.AppendLine($"{fromAlias} {arrow} {toAlias}");
        }

        sb.AppendLine("@enduml");
        return sb.ToString();
    }

    private static void WriteType(ClassDiagramType type, TypeAliasRegistry registry, StringBuilder sb)
    {
        var alias = registry.Alias(type.FullName, type.Name);
        var label = registry.Label(type.FullName, type.Name);
        var keyword = type.Shape switch
        {
            TypeShape.Interface => "interface",
            TypeShape.Enum => "enum",
            _ => "class",
        };

        sb.AppendLine($"{keyword} \"{label}\" as {alias} {{");

        foreach (var field in type.Fields)
        {
            sb.AppendLine($"  {PlantUmlText.Sanitize(field.Signature)}");
        }

        if (type.Fields.Count > 0 && type.Methods.Count > 0)
        {
            sb.AppendLine("  --");
        }

        foreach (var method in type.Methods)
        {
            sb.AppendLine($"  {PlantUmlText.Sanitize(method.Signature)}");
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }
}
