using digen_2.Core;

namespace digen_2.Diagrams;

/// <summary>Assigns stable, format-neutral entity aliases (P0, P1, ...) to
/// types, qualifying the display label only when two distinct types share a
/// simple name. Shared by every diagram exporter/renderer - the label
/// returned is raw text; each renderer applies its own format-specific
/// escaping before emitting it.</summary>
internal sealed class TypeAliasRegistry
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

    /// <summary>The alias for a type already registered, or null if it never was.</summary>
    public string? TryGetAlias(string fullName) => _aliases.GetValueOrDefault(fullName);

    /// <summary>The raw (un-escaped) display label for a type: its simple name, or its full name if another registered type shares that simple name.</summary>
    public string Label(string fullName, string name)
    {
        var collision = _order.Any(t => t.FullName != fullName && t.Name == name);
        return collision ? fullName : name;
    }
}
