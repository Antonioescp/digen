using digen_2.Core;

namespace digen_2.Diagrams.PlantUml;

/// <summary>Assigns stable PlantUML participant aliases to types, qualifying
/// the display label only when two distinct types share a simple name.</summary>
internal sealed class ParticipantRegistry
{
    private readonly Dictionary<string, string> _aliases = new();
    private readonly List<(string FullName, string Name)> _order = [];

    public IReadOnlyList<(string FullName, string Name)> Types => _order;

    public void Register(CallableMethod method) => Register(method.TypeFullName, method.TypeName);

    public void Register(string fullName, string name)
    {
        if (_aliases.ContainsKey(fullName)) return;
        _aliases[fullName] = $"P{_aliases.Count}";
        _order.Add((fullName, name));
    }

    public string Alias(CallableMethod method) => Alias(method.TypeFullName, method.TypeName);

    public string Alias(string fullName, string name)
    {
        Register(fullName, name);
        return _aliases[fullName];
    }

    public string Label(string fullName, string name)
    {
        var collision = _order.Any(t => t.FullName != fullName && t.Name == name);
        return PlantUmlText.Sanitize(collision ? fullName : name);
    }
}
