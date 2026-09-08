using System.Text;
using digen_2.Core;
using digen_2.Diagrams;

namespace digen_2.Diagrams.Mermaid;

/// <summary>
/// Renders a CallGraphNode tree as a Mermaid sequence diagram. Mermaid has
/// no equivalent of PlantUML's "message from outside" arrow (`[->`), so the
/// diagram's entry point is represented as a self-message on the root
/// participant instead.
/// </summary>
internal static class MermaidSequenceRenderer
{
    public static string RenderOutgoing(CallGraphNode root)
    {
        var registry = new TypeAliasRegistry();
        CollectParticipants(root, registry);

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: \"Sequence Diagram: {MermaidText.Sanitize(root.Method.TypeName)}.{MermaidText.Sanitize(root.Method.MethodName)}\"");
        sb.AppendLine("---");
        sb.AppendLine("sequenceDiagram");

        WriteParticipants(registry, sb);

        var entryAlias = registry.Alias(root.Method);
        sb.AppendLine($"    {entryAlias}->>{entryAlias}: {MermaidText.Signature(root.Method)}");
        sb.AppendLine($"    activate {entryAlias}");

        foreach (var child in root.Children)
        {
            WriteNode(root.Method, child, registry, sb);
        }

        var rootReturn = MermaidText.ReturnLabel(root.Method);
        if (rootReturn is not null)
        {
            sb.AppendLine($"    {entryAlias}-->>{entryAlias}: {rootReturn}");
        }

        sb.AppendLine($"    deactivate {entryAlias}");

        return sb.ToString();
    }

    public static string RenderIncomingPath(IReadOnlyList<CallGraphNode> chain, int pathIndex, int pathCount)
    {
        var registry = new TypeAliasRegistry();
        foreach (var node in chain) registry.Register(node.Method);

        var target = chain[^1].Method;
        var title = $"Incoming Calls: {target.TypeName}.{target.MethodName}";
        if (pathCount > 1) title += $" (path {pathIndex} of {pathCount})";

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: \"{MermaidText.Sanitize(title)}\"");
        sb.AppendLine("---");
        sb.AppendLine("sequenceDiagram");

        WriteParticipants(registry, sb);

        var entryAlias = registry.Alias(chain[0].Method);
        sb.AppendLine($"    {entryAlias}->>{entryAlias}: {MermaidText.Signature(chain[0].Method)}");
        sb.AppendLine($"    activate {entryAlias}");
        if (chain[0].Note is not null)
        {
            sb.AppendLine($"    Note right of {entryAlias}: {MermaidText.Sanitize(chain[0].Note!)}");
        }

        for (var i = 1; i < chain.Count; i++)
        {
            var fromAlias = registry.Alias(chain[i - 1].Method);
            var toAlias = registry.Alias(chain[i].Method);
            sb.AppendLine($"    {fromAlias}->>{toAlias}: {MermaidText.Signature(chain[i].Method)}");
            sb.AppendLine($"    activate {toAlias}");
        }

        for (var i = chain.Count - 1; i >= 1; i--)
        {
            var fromAlias = registry.Alias(chain[i - 1].Method);
            var toAlias = registry.Alias(chain[i].Method);
            var returnLabel = MermaidText.ReturnLabel(chain[i].Method);
            if (returnLabel is not null)
            {
                sb.AppendLine($"    {toAlias}-->>{fromAlias}: {returnLabel}");
            }

            sb.AppendLine($"    deactivate {toAlias}");
        }

        var entryReturn = MermaidText.ReturnLabel(chain[0].Method);
        if (entryReturn is not null)
        {
            sb.AppendLine($"    {entryAlias}-->>{entryAlias}: {entryReturn}");
        }

        sb.AppendLine($"    deactivate {entryAlias}");

        return sb.ToString();
    }

    private static void WriteNode(CallableMethod callerMethod, CallGraphNode node, TypeAliasRegistry registry, StringBuilder sb)
    {
        var callerAlias = registry.Alias(callerMethod);
        var calleeAlias = registry.Alias(node.Method);

        sb.AppendLine($"    {callerAlias}->>{calleeAlias}: {MermaidText.Signature(node.Method)}");
        sb.AppendLine($"    activate {calleeAlias}");

        if (node.Note is not null)
        {
            sb.AppendLine($"    Note right of {calleeAlias}: {MermaidText.Sanitize(node.Note)}");
        }

        foreach (var child in node.Children)
        {
            WriteNode(node.Method, child, registry, sb);
        }

        var returnLabel = MermaidText.ReturnLabel(node.Method);
        if (returnLabel is not null && node.Note is null)
        {
            sb.AppendLine($"    {calleeAlias}-->>{callerAlias}: {returnLabel}");
        }

        sb.AppendLine($"    deactivate {calleeAlias}");
    }

    private static void CollectParticipants(CallGraphNode node, TypeAliasRegistry registry)
    {
        registry.Register(node.Method);
        foreach (var child in node.Children)
        {
            CollectParticipants(child, registry);
        }
    }

    private static void WriteParticipants(TypeAliasRegistry registry, StringBuilder sb)
    {
        foreach (var (fullName, name) in registry.Types)
        {
            sb.AppendLine($"    participant {registry.Alias(fullName, name)} as {MermaidText.Sanitize(registry.Label(fullName, name))}");
        }
    }
}
