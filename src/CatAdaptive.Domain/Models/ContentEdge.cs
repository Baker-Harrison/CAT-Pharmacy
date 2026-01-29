namespace CatAdaptive.Domain.Models;

/// <summary>
/// A directed edge between two content nodes in the Content Graph.
/// </summary>
public sealed record ContentEdge
{
    public Guid Id { get; init; }
    
    /// <summary>Source node ID (the "from" side of the relationship).</summary>
    public Guid FromNodeId { get; init; }
    
    /// <summary>Target node ID (the "to" side of the relationship).</summary>
    public Guid ToNodeId { get; init; }
    
    /// <summary>Type of relationship between the nodes.</summary>
    public ContentEdgeType Type { get; init; }

    /// <summary>
    /// Creates a new edge between two content nodes.
    /// </summary>
    public static ContentEdge Create(Guid fromNodeId, Guid toNodeId, ContentEdgeType type)
    {
        return new ContentEdge
        {
            Id = Guid.NewGuid(),
            FromNodeId = fromNodeId,
            ToNodeId = toNodeId,
            Type = type
        };
    }

    /// <summary>
    /// Creates an "Explains" edge (explanation → concept).
    /// </summary>
    public static ContentEdge Explains(Guid explanationNodeId, Guid conceptNodeId)
        => Create(explanationNodeId, conceptNodeId, ContentEdgeType.Explains);

    /// <summary>
    /// Creates a "DependsOn" edge (concept → prerequisite).
    /// </summary>
    public static ContentEdge DependsOn(Guid conceptNodeId, Guid prerequisiteNodeId)
        => Create(conceptNodeId, prerequisiteNodeId, ContentEdgeType.DependsOn);

    /// <summary>
    /// Creates an "ExampleOf" edge (example → concept).
    /// </summary>
    public static ContentEdge ExampleOf(Guid exampleNodeId, Guid conceptNodeId)
        => Create(exampleNodeId, conceptNodeId, ContentEdgeType.ExampleOf);

    /// <summary>
    /// Creates an "IntroducedIn" edge (concept → slide reference).
    /// </summary>
    public static ContentEdge IntroducedIn(Guid conceptNodeId, Guid slideRefNodeId)
        => Create(conceptNodeId, slideRefNodeId, ContentEdgeType.IntroducedIn);
}
