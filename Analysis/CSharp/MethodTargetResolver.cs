using Microsoft.CodeAnalysis;

namespace digen_2.Analysis.CSharp;

public sealed class MethodResolutionResult
{
    public bool Success { get; private init; }
    public IMethodSymbol? Method { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static MethodResolutionResult Ok(IMethodSymbol method) => new() { Success = true, Method = method };
    public static MethodResolutionResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

/// <summary>
/// Resolves a "Namespace.Type.Method" spec (as typed by a user) to a concrete
/// IMethodSymbol somewhere in the solution.
/// </summary>
public sealed class MethodTargetResolver(Solution solution)
{
    public async Task<MethodResolutionResult> ResolveAsync(string typeAndMethod, string[]? paramTypes)
    {
        var lastDot = typeAndMethod.LastIndexOf('.');
        if (lastDot <= 0 || lastDot == typeAndMethod.Length - 1)
        {
            return MethodResolutionResult.Fail(
                $"Target method must be in the form 'Namespace.Type.Method', got '{typeAndMethod}'.");
        }

        var typeName = typeAndMethod[..lastDot];
        var methodName = typeAndMethod[(lastDot + 1)..];

        var matchedType = await SolutionTypeLookup.FindTypeAsync(solution, typeName);

        if (matchedType is null)
        {
            return MethodResolutionResult.Fail($"Could not find type '{typeName}' in the solution.");
        }

        var candidates = matchedType.GetMembers(methodName).OfType<IMethodSymbol>().ToList();
        if (candidates.Count == 0)
        {
            return MethodResolutionResult.Fail($"Type '{typeName}' has no method named '{methodName}'.");
        }

        if (candidates.Count == 1)
        {
            return MethodResolutionResult.Ok(candidates[0]);
        }

        if (paramTypes is { Length: > 0 })
        {
            var filtered = candidates
                .Where(m => ParametersMatch(m, paramTypes))
                .ToList();

            if (filtered.Count == 1)
            {
                return MethodResolutionResult.Ok(filtered[0]);
            }

            if (filtered.Count == 0)
            {
                return MethodResolutionResult.Fail(
                    $"No overload of '{typeName}.{methodName}' matches parameter types ({string.Join(", ", paramTypes)}).\n" +
                    DescribeCandidates(candidates));
            }
        }

        return MethodResolutionResult.Fail(
            $"'{typeName}.{methodName}' is overloaded ({candidates.Count} candidates). " +
            "Disambiguate with --params <types> or a '(types)' suffix on the target.\n" +
            DescribeCandidates(candidates));
    }

    private static bool ParametersMatch(IMethodSymbol method, string[] paramTypes)
    {
        if (method.Parameters.Length != paramTypes.Length) return false;
        for (var i = 0; i < paramTypes.Length; i++)
        {
            var actual = method.Parameters[i].Type;
            var wanted = paramTypes[i];
            if (!string.Equals(actual.Name, wanted, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(actual.ToDisplayString(), wanted, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string DescribeCandidates(IEnumerable<IMethodSymbol> candidates) =>
        "Candidates:\n" + string.Join('\n', candidates.Select(c => "  " + c.ToDisplayString()));
}
