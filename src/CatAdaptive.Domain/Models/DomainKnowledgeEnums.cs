namespace CatAdaptive.Domain.Models;

/// <summary>
/// Types of nodes in the Domain Knowledge Graph.
/// </summary>
public enum DomainNodeType
{
    Concept,
    Skill,
    Objective
}

/// <summary>
/// Types of relationships between domain nodes.
/// </summary>
public enum DomainEdgeType
{
    PrerequisiteOf,
    PartOf,
    RelatedTo,
    ContrastsWith
}

/// <summary>
/// Bloom's taxonomy levels for domain nodes.
/// </summary>
public enum BloomsLevel
{
    Remember,
    Understand,
    Apply,
    Analyze,
    Evaluate,
    Create
}

/// <summary>
/// Content modalities in the Content Graph.
/// </summary>
public enum ContentModality
{
    Text,
    Visual,
    Interactive,
    Audio,
    Video
}

