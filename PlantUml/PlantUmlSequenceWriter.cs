using System.Text;
using digen_2.Analysis;

namespace digen_2.PlantUml;

/// <summary>Renders an outgoing call tree (InvocationNode) - "what does this method call?" - as one PlantUML sequence diagram.</summary>
public sealed class PlantUmlSequenceWriter
{
    public string Write(InvocationNode root)
    {
        var registry = new ParticipantRegistry();
        CollectParticipants(root, registry);

        var sb = new StringBuilder();
        sb.AppendLine("@startuml");
        sb.AppendLine($"title Sequence Diagram: {PlantUmlText.Sanitize(root.Callee.ContainingType.Name)}.{PlantUmlText.Sanitize(root.Callee.Name)}");
        sb.AppendLine("autoactivate off");
        sb.AppendLine();

        foreach (var type in registry.Types)
        {
            sb.AppendLine($"participant \"{registry.Label(type)}\" as {registry.Alias(type)}");
        }

        sb.AppendLine();

        var entryAlias = registry.Alias(root.Callee.ContainingType);
        sb.AppendLine($"[-> {entryAlias} : {PlantUmlText.Signature(root.Callee)}");
        sb.AppendLine($"activate {entryAlias}");

        foreach (var child in root.Children)
        {
            WriteNode(child, registry, sb);
        }

        var rootReturn = PlantUmlText.ReturnLabel(root.Callee);
        sb.AppendLine(rootReturn is not null
            ? $"{entryAlias} -->] : {PlantUmlText.Sanitize(rootReturn)}"
            : $"{entryAlias} -->]");

        sb.AppendLine($"deactivate {entryAlias}");
        sb.AppendLine("@enduml");

        return sb.ToString();
    }

    private static void WriteNode(InvocationNode node, ParticipantRegistry registry, StringBuilder sb)
    {
        var callerAlias = registry.Alias(node.Caller!.ContainingType);
        var calleeAlias = registry.Alias(node.Callee.ContainingType);

        sb.AppendLine($"{callerAlias} -> {calleeAlias} : {PlantUmlText.Signature(node.Callee)}");
        sb.AppendLine($"activate {calleeAlias}");

        if (node.Note is not null)
        {
            sb.AppendLine($"note right of {calleeAlias} : {PlantUmlText.Sanitize(node.Note)}");
        }

        foreach (var child in node.Children)
        {
            WriteNode(child, registry, sb);
        }

        var returnLabel = PlantUmlText.ReturnLabel(node.Callee);
        if (returnLabel is not null && node.Note is null)
        {
            sb.AppendLine($"{calleeAlias} --> {callerAlias} : {PlantUmlText.Sanitize(returnLabel)}");
        }

        sb.AppendLine($"deactivate {calleeAlias}");
    }

    private static void CollectParticipants(InvocationNode node, ParticipantRegistry registry)
    {
        registry.Register(node.Callee.ContainingType);
        foreach (var child in node.Children)
        {
            registry.Register(child.Caller!.ContainingType);
            CollectParticipants(child, registry);
        }
    }
}
