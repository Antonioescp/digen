using System.Text;
using digen_2.Core;

namespace digen_2.Diagrams.PlantUml;

/// <summary>Renders a CallGraphNode tree as a PlantUML sequence diagram.</summary>
public sealed class PlantUmlExporter : IDiagramExporter
{
    public string Id => "plantuml";

    public string RenderOutgoing(CallGraphNode root)
    {
        var registry = new ParticipantRegistry();
        CollectParticipants(root, registry);

        var sb = new StringBuilder();
        sb.AppendLine("@startuml");
        sb.AppendLine($"title Sequence Diagram: {PlantUmlText.Sanitize(root.Method.TypeName)}.{PlantUmlText.Sanitize(root.Method.MethodName)}");
        sb.AppendLine("autoactivate off");
        sb.AppendLine();

        WriteParticipants(registry, sb);
        sb.AppendLine();

        var entryAlias = registry.Alias(root.Method);
        sb.AppendLine($"[-> {entryAlias} : {PlantUmlText.Signature(root.Method)}");
        sb.AppendLine($"activate {entryAlias}");

        foreach (var child in root.Children)
        {
            WriteNode(root.Method, child, registry, sb);
        }

        var rootReturn = PlantUmlText.ReturnLabel(root.Method);
        sb.AppendLine(rootReturn is not null
            ? $"{entryAlias} -->] : {rootReturn}"
            : $"{entryAlias} -->]");

        sb.AppendLine($"deactivate {entryAlias}");
        sb.AppendLine("@enduml");

        return sb.ToString();
    }

    public string RenderIncomingPath(IReadOnlyList<CallGraphNode> chronologicalChain, int pathIndex, int pathCount)
    {
        var chain = chronologicalChain;
        var registry = new ParticipantRegistry();
        foreach (var node in chain) registry.Register(node.Method);

        var target = chain[^1].Method;
        var sb = new StringBuilder();
        sb.AppendLine("@startuml");

        var title = $"Incoming Calls: {target.TypeName}.{target.MethodName}";
        if (pathCount > 1) title += $" (path {pathIndex} of {pathCount})";
        sb.AppendLine($"title {PlantUmlText.Sanitize(title)}");
        sb.AppendLine("autoactivate off");
        sb.AppendLine();

        WriteParticipants(registry, sb);
        sb.AppendLine();

        var entryAlias = registry.Alias(chain[0].Method);
        sb.AppendLine($"[-> {entryAlias} : {PlantUmlText.Signature(chain[0].Method)}");
        sb.AppendLine($"activate {entryAlias}");
        if (chain[0].Note is not null)
        {
            sb.AppendLine($"note right of {entryAlias} : {PlantUmlText.Sanitize(chain[0].Note!)}");
        }

        for (var i = 1; i < chain.Count; i++)
        {
            var fromAlias = registry.Alias(chain[i - 1].Method);
            var toAlias = registry.Alias(chain[i].Method);
            sb.AppendLine($"{fromAlias} -> {toAlias} : {PlantUmlText.Signature(chain[i].Method)}");
            sb.AppendLine($"activate {toAlias}");
        }

        for (var i = chain.Count - 1; i >= 1; i--)
        {
            var fromAlias = registry.Alias(chain[i - 1].Method);
            var toAlias = registry.Alias(chain[i].Method);
            var returnLabel = PlantUmlText.ReturnLabel(chain[i].Method);
            if (returnLabel is not null)
            {
                sb.AppendLine($"{toAlias} --> {fromAlias} : {returnLabel}");
            }

            sb.AppendLine($"deactivate {toAlias}");
        }

        var entryReturn = PlantUmlText.ReturnLabel(chain[0].Method);
        sb.AppendLine(entryReturn is not null
            ? $"{entryAlias} -->] : {entryReturn}"
            : $"{entryAlias} -->]");

        sb.AppendLine($"deactivate {entryAlias}");
        sb.AppendLine("@enduml");

        return sb.ToString();
    }

    private static void WriteNode(CallableMethod callerMethod, CallGraphNode node, ParticipantRegistry registry, StringBuilder sb)
    {
        var callerAlias = registry.Alias(callerMethod);
        var calleeAlias = registry.Alias(node.Method);

        sb.AppendLine($"{callerAlias} -> {calleeAlias} : {PlantUmlText.Signature(node.Method)}");
        sb.AppendLine($"activate {calleeAlias}");

        if (node.Note is not null)
        {
            sb.AppendLine($"note right of {calleeAlias} : {PlantUmlText.Sanitize(node.Note)}");
        }

        foreach (var child in node.Children)
        {
            WriteNode(node.Method, child, registry, sb);
        }

        var returnLabel = PlantUmlText.ReturnLabel(node.Method);
        if (returnLabel is not null && node.Note is null)
        {
            sb.AppendLine($"{calleeAlias} --> {callerAlias} : {returnLabel}");
        }

        sb.AppendLine($"deactivate {calleeAlias}");
    }

    private static void CollectParticipants(CallGraphNode node, ParticipantRegistry registry)
    {
        registry.Register(node.Method);
        foreach (var child in node.Children)
        {
            CollectParticipants(child, registry);
        }
    }

    private static void WriteParticipants(ParticipantRegistry registry, StringBuilder sb)
    {
        foreach (var (fullName, name) in registry.Types)
        {
            sb.AppendLine($"participant \"{registry.Label(fullName, name)}\" as {registry.Alias(fullName, name)}");
        }
    }
}
