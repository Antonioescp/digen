using Microsoft.CodeAnalysis;

namespace digen_2.Analysis.CSharp;

/// <summary>Finds a named type anywhere in a solution by its "Namespace.Type" display name.</summary>
internal static class SolutionTypeLookup
{
    public static async Task<INamedTypeSymbol?> FindTypeAsync(Solution solution, string typeFullName)
    {
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation is null) continue;

            foreach (var type in EnumerateNamedTypes(compilation.GlobalNamespace))
            {
                if (type.ToDisplayString() == typeFullName) return type;
            }
        }

        return null;
    }

    public static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in EnumerateNestedTypes(type))
                yield return nested;
        }

        foreach (var child in ns.GetNamespaceMembers())
        foreach (var type in EnumerateNamedTypes(child))
            yield return type;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var grandchild in EnumerateNestedTypes(nested))
                yield return grandchild;
        }
    }
}
