namespace digen_2.Core;

public enum CallDirection
{
    /// <summary>What does the target method call? Walked recursively into a single tree.</summary>
    Outgoing,

    /// <summary>Who calls the target method, walked up to entry points? Branches into one chain per caller path.</summary>
    Incoming,
}
