using digen_2.Core;
using Microsoft.CodeAnalysis;

namespace digen_2.Analysis.CSharp;

/// <summary>
/// Builds a class diagram model by breadth-first walking type relationships
/// (base type, interfaces, and types referenced by fields/properties/method
/// signatures) outward from a root type, bounded by depth and a node cap so
/// a heavily-coupled codebase doesn't produce an unusable diagram.
/// </summary>
public sealed class ClassDiagramBuilder(int maxDepth)
{
    private const int MaxTypes = 40;

    public ClassDiagramModel Build(INamedTypeSymbol root)
    {
        var included = new Dictionary<string, INamedTypeSymbol>();
        var order = new List<string>();
        var relations = new List<TypeRelation>();
        var queue = new Queue<(INamedTypeSymbol Type, int Depth)>();

        Enqueue(root, 0);

        while (queue.Count > 0)
        {
            var (type, depth) = queue.Dequeue();
            var typeFullName = type.ToDisplayString();

            if (type.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
            {
                foreach (var related in RelevantSolutionTypes(baseType))
                {
                    relations.Add(new TypeRelation(typeFullName, related.ToDisplayString(), TypeRelationKind.Inheritance));
                    Enqueue(related, depth + 1);
                }
            }

            foreach (var iface in type.Interfaces)
            foreach (var related in RelevantSolutionTypes(iface))
            {
                relations.Add(new TypeRelation(typeFullName, related.ToDisplayString(), TypeRelationKind.Implementation));
                Enqueue(related, depth + 1);
            }

            foreach (var memberType in MemberTypes(type))
            foreach (var related in RelevantSolutionTypes(memberType))
            {
                if (SymbolEqualityComparer.Default.Equals(related, type)) continue;
                relations.Add(new TypeRelation(typeFullName, related.ToDisplayString(), TypeRelationKind.Association));
                Enqueue(related, depth + 1);
            }
        }

        var types = order.Select(fullName => MapType(included[fullName])).ToList();
        var dedupedRelations = relations.Distinct().Where(r => included.ContainsKey(r.ToTypeFullName)).ToList();

        return new ClassDiagramModel { Types = types, Relations = dedupedRelations };

        void Enqueue(INamedTypeSymbol type, int depth)
        {
            var fullName = type.ToDisplayString();
            if (included.ContainsKey(fullName)) return;
            if (depth > maxDepth) return;
            if (included.Count >= MaxTypes) return;

            included[fullName] = type;
            order.Add(fullName);
            queue.Enqueue((type, depth));
        }
    }

    private static IEnumerable<ITypeSymbol> MemberTypes(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            switch (member)
            {
                case IFieldSymbol { IsImplicitlyDeclared: false } field:
                    yield return field.Type;
                    break;
                case IPropertySymbol property:
                    yield return property.Type;
                    break;
                case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                    foreach (var p in method.Parameters) yield return p.Type;
                    if (!method.ReturnsVoid) yield return method.ReturnType;
                    break;
            }
        }
    }

    /// <summary>
    /// The types worth drawing a relation to: the type itself if it has
    /// source in the solution, or (recursively) its generic type arguments /
    /// array element type otherwise - so `List&lt;OrderItem&gt;` still links
    /// to OrderItem even though List&lt;T&gt; itself is a BCL type.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> RelevantSolutionTypes(ITypeSymbol? type)
    {
        switch (type)
        {
            case INamedTypeSymbol { DeclaringSyntaxReferences.Length: > 0 } named:
                yield return named;
                break;
            case INamedTypeSymbol { IsGenericType: true } generic:
                foreach (var arg in generic.TypeArguments)
                foreach (var t in RelevantSolutionTypes(arg))
                    yield return t;
                break;
            case IArrayTypeSymbol array:
                foreach (var t in RelevantSolutionTypes(array.ElementType))
                    yield return t;
                break;
        }
    }

    private static ClassDiagramType MapType(INamedTypeSymbol type)
    {
        var shape = type.TypeKind switch
        {
            TypeKind.Interface => TypeShape.Interface,
            TypeKind.Struct => TypeShape.Struct,
            TypeKind.Enum => TypeShape.Enum,
            _ => type.IsRecord ? TypeShape.Record : TypeShape.Class,
        };

        var fields = type.GetMembers().OfType<IPropertySymbol>()
            .Select(p => new TypeMember(p.Name, $"{Visibility(p.DeclaredAccessibility)}{p.Name}: {DisplayType(p.Type)}", IsMethod: false))
            .Concat(type.GetMembers().OfType<IFieldSymbol>()
                .Where(f => !f.IsImplicitlyDeclared)
                .Select(f => new TypeMember(f.Name, $"{Visibility(f.DeclaredAccessibility)}{f.Name}: {DisplayType(f.Type)}", IsMethod: false)))
            .ToList();

        var methods = type.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind is MethodKind.Ordinary or MethodKind.Constructor && !m.IsImplicitlyDeclared)
            .Select(m => new TypeMember(m.Name, MethodSignature(m), IsMethod: true))
            .ToList();

        return new ClassDiagramType(type.Name, type.ToDisplayString(), shape, fields, methods);
    }

    private static string MethodSignature(IMethodSymbol method)
    {
        var name = method.MethodKind == MethodKind.Constructor ? method.ContainingType.Name : method.Name;
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Name}: {DisplayType(p.Type)}"));
        var returnPart = method.MethodKind == MethodKind.Constructor || method.ReturnsVoid
            ? ""
            : $": {DisplayType(method.ReturnType)}";
        return $"{Visibility(method.DeclaredAccessibility)}{name}({parameters}){returnPart}";
    }

    private static string DisplayType(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    private static string Visibility(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "+",
        Accessibility.Private => "-",
        Accessibility.Protected => "#",
        Accessibility.ProtectedOrInternal => "#",
        _ => "~",
    };
}
