using System.Text;
using digen_2.Analysis;

namespace digen_2.PlantUml;

/// <summary>
/// Renders one root-to-target chain from an incoming call hierarchy as a
/// single linear PlantUML sequence diagram: entry point calls the next
/// method, which calls the next, ... down to the target method.
/// </summary>
public sealed class PlantUmlIncomingPathWriter
{
    public string Write(IReadOnlyList<CallerNode> chain, int pathIndex, int pathCount)
    {
        var registry = new ParticipantRegistry();
        foreach (var node in chain) registry.Register(node.Method.ContainingType);

        var target = chain[^1].Method;
        var sb = new StringBuilder();
        sb.AppendLine("@startuml");

        var title = $"Incoming Calls: {target.ContainingType.Name}.{target.Name}";
        if (pathCount > 1) title += $" (path {pathIndex} of {pathCount})";
        sb.AppendLine($"title {PlantUmlText.Sanitize(title)}");
        sb.AppendLine("autoactivate off");
        sb.AppendLine();

        foreach (var type in registry.Types)
        {
            sb.AppendLine($"participant \"{registry.Label(type)}\" as {registry.Alias(type)}");
        }

        sb.AppendLine();

        var entryAlias = registry.Alias(chain[0].Method.ContainingType);
        sb.AppendLine($"[-> {entryAlias} : {PlantUmlText.Signature(chain[0].Method)}");
        sb.AppendLine($"activate {entryAlias}");
        if (chain[0].Note is not null)
        {
            sb.AppendLine($"note right of {entryAlias} : {PlantUmlText.Sanitize(chain[0].Note!)}");
        }

        for (var i = 1; i < chain.Count; i++)
        {
            var fromAlias = registry.Alias(chain[i - 1].Method.ContainingType);
            var toAlias = registry.Alias(chain[i].Method.ContainingType);
            sb.AppendLine($"{fromAlias} -> {toAlias} : {PlantUmlText.Signature(chain[i].Method)}");
            sb.AppendLine($"activate {toAlias}");
        }

        for (var i = chain.Count - 1; i >= 1; i--)
        {
            var fromAlias = registry.Alias(chain[i - 1].Method.ContainingType);
            var toAlias = registry.Alias(chain[i].Method.ContainingType);
            var returnLabel = PlantUmlText.ReturnLabel(chain[i].Method);
            if (returnLabel is not null)
            {
                sb.AppendLine($"{toAlias} --> {fromAlias} : {PlantUmlText.Sanitize(returnLabel)}");
            }

            sb.AppendLine($"deactivate {toAlias}");
        }

        var entryReturn = PlantUmlText.ReturnLabel(chain[0].Method);
        sb.AppendLine(entryReturn is not null
            ? $"{entryAlias} -->] : {PlantUmlText.Sanitize(entryReturn)}"
            : $"{entryAlias} -->]");

        sb.AppendLine($"deactivate {entryAlias}");
        sb.AppendLine("@enduml");

        return sb.ToString();
    }
}
