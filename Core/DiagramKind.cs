namespace digen_2.Core;

public enum DiagramKind
{
    /// <summary>A call graph, walked outward (what a method calls) or inward (who calls a method).</summary>
    Sequence,

    /// <summary>Types, their members, and the relationships (inheritance, implementation, association) between them.</summary>
    Class,
}
