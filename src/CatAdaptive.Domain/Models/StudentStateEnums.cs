namespace CatAdaptive.Domain.Models;

/// <summary>
/// Represents the student's mastery level for a concept.
/// </summary>
public enum MasteryLevel
{
    Unknown = 0,
    Novice = 1,
    Developing = 2,
    Proficient = 3,
    Advanced = 4
}

/// <summary>
/// Types of mastery events that can update student state.
/// </summary>
public enum MasteryEventType
{
    DiagnosticAssessment,
    FormativeCheck,
    PracticeAttempt,
    TeachingIntervention,
    PeerExplanation,
    RealWorldApplication,
    SpacedRetrieval,
    ErrorCorrection,
    MetacognitiveReflection
}

/// <summary>
/// Types of knowledge gaps identified in student understanding.
/// </summary>
public enum GapType
{
    Conceptual,
    Procedural,
    Factual,
    Metacognitive,
    Transfer
}

/// <summary>
/// Types of evidence that support mastery assessment.
/// </summary>
public enum EvidenceType
{
    DirectAssessment,
    IndirectObservation,
    SelfReport,
    PeerFeedback,
    ApplicationDemo,
    HistoricalPattern
}

/// <summary>
/// Content modality types for multi-modal learning.
/// </summary>
public enum ContentModality
{
    Text,
    Visual,
    Audio,
    Video,
    Interactive,
    Simulation,
    AR_VR
}

/// <summary>
/// Types of content nodes in the AI-enhanced content graph.
/// </summary>
public enum ContentNodeType
{
    Explanation,
    Example,
    WorkedExample,
    WorkedProblem,
    ClinicalCase,
    Question,
    Visual,
    Mnemonic,
    Summary,
    Interactive,
    Video,
    Audio,
    Simulation,
    Assessment,
    CrossReference
}

/// <summary>
/// Types of edges in the content graph.
/// </summary>
public enum ContentEdgeType
{
    PrerequisiteOf,
    RelatedTo,
    AlternativeTo,
    SimplifiedVersionOf,
    AdvancedVersionOf,
    SupportsLearningOf,
    AssessesUnderstandingOf
}

/// <summary>
/// Origin of content in the graph.
/// </summary>
public enum ContentOrigin
{
    Slides,
    WebSearch,
    AcademicPaper,
    ClinicalGuideline,
    AIGenerated,
    UserContributed,
    External
}

/// <summary>
/// Bloom's taxonomy levels for learning objectives.
/// </summary>
public enum BloomsLevel
{
    Remember = 1,
    Understand = 2,
    Apply = 3,
    Analyze = 4,
    Evaluate = 5,
    Create = 6
}

/// <summary>
/// Student's difficulty preference for content.
/// </summary>
public enum DifficultyPreference
{
    Easier,
    Appropriate,
    Challenging
}
