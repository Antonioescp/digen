using Microsoft.CodeAnalysis;

namespace digen_2.PlantUml;

/// <summary>Assigns stable PlantUML participant aliases to types, qualifying
/// the display label only when two distinct types share a simple name.</summary>
internal sealed class ParticipantRegistry
{
    private readonly Dictionary<INamedTypeSymbol, string> _aliases = new(SymbolEqualityComparer.Default);
    private readonly List<INamedTypeSymbol> _order = [];

    public IReadOnlyList<INamedTypeSymbol> Types => _order;

    public void Register(INamedTypeSymbol type)
    {
        if (_aliases.ContainsKey(type)) return;
        _aliases[type] = $"P{_aliases.Count}";
        _order.Add(type);
    }

    public string Alias(INamedTypeSymbol type)
    {
        Register(type);
        return _aliases[type];
    }

    public string Label(INamedTypeSymbol type)
    {
        var collision = _order.Any(t => !SymbolEqualityComparer.Default.Equals(t, type) && t.Name == type.Name);
        var text = collision ? type.ToDisplayString() : type.Name;
        return PlantUmlText.Sanitize(text);
    }
}
