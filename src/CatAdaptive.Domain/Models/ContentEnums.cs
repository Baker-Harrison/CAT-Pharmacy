namespace CatAdaptive.Domain.Models;

/// <summary>
/// Types of content nodes in the Content Graph.
/// </summary>
public enum ContentNodeType
{
    /// <summary>A core concept or topic that learners must master.</summary>
    Concept,
    
    /// <summary>A formal definition of a term or concept.</summary>
    Definition,
    
    /// <summary>An explanation of how or why something works.</summary>
    Explanation,
    
    /// <summary>A concrete example illustrating a concept.</summary>
    Example,
    
    /// <summary>A step-by-step worked problem demonstrating application.</summary>
    WorkedProblem,
    
    /// <summary>A learning objective from course materials.</summary>
    LearningObjective,
    
    /// <summary>A reference to a source slide or chunk.</summary>
    SlideReference
}

/// <summary>
/// Origin of content (internal course materials vs external sources).
/// </summary>
public enum ContentOrigin
{
    /// <summary>Content from instructor-provided materials (e.g., PPTX slides).</summary>
    Internal,
    
    /// <summary>Content acquired from external sources (e.g., PubMed, DailyMed).</summary>
    External
}

/// <summary>
/// Types of directed edges between content nodes.
/// </summary>
public enum ContentEdgeType
{
    /// <summary>Source node explains the target concept.</summary>
    Explains,
    
    /// <summary>Source concept depends on target concept (prerequisite).</summary>
    DependsOn,
    
    /// <summary>Source concept is a prerequisite for target concept.</summary>
    PrerequisiteFor,
    
    /// <summary>Source node is an example of target concept.</summary>
    ExampleOf,
    
    /// <summary>Source concept was introduced in target slide/chunk.</summary>
    IntroducedIn
}
