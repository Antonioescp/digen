namespace digen_2.Core;

public enum TypeShape
{
    Class,
    Interface,
    Struct,
    Enum,
    Record,
}

public sealed record TypeMember(string Name, string Signature, bool IsMethod);

public enum TypeRelationKind
{
    Inheritance,
    Implementation,
    Association,
}

public sealed record TypeRelation(string FromTypeFullName, string ToTypeFullName, TypeRelationKind Kind);

public sealed record ClassDiagramType(
    string Name,
    string FullName,
    TypeShape Shape,
    IReadOnlyList<TypeMember> Fields,
    IReadOnlyList<TypeMember> Methods);

/// <summary>Language-agnostic description of a set of types and how they relate, for a class diagram.</summary>
public sealed class ClassDiagramModel
{
    public required IReadOnlyList<ClassDiagramType> Types { get; init; }
    public required IReadOnlyList<TypeRelation> Relations { get; init; }
}
