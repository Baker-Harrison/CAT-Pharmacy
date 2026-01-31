using System.Collections.ObjectModel;

namespace CatAdaptive.Domain.Models;

/// <summary>
/// Represents a retrieval event for tracking mastery.
/// </summary>
public sealed record RetrievalEvent(
    DateTimeOffset Timestamp,
    bool WasSuccessful,
    GapType? ErrorType,
    double Confidence,
    int LatencyMs,
    string? PromptFormat);

/// <summary>
/// Represents a mastery event that updates student state.
/// </summary>
public sealed record MasteryEvent(
    Guid NodeId,
    MasteryEventType Type,
    double Strength,
    string Evidence,
    AssessmentContext Context,
    DateTimeOffset Timestamp,
    double Confidence);

/// <summary>
/// Context for an assessment event.
/// </summary>
public sealed record AssessmentContext(
    double Difficulty,
    BloomsLevel BloomsLevel,
    string ContentType,
    int AttemptNumber);

/// <summary>
/// Evidence vector for multi-dimensional mastery tracking.
/// </summary>
public sealed record EvidenceVector(
    double ConceptualUnderstanding,
    double ProceduralSkill,
    double ApplicationAbility,
    double TransferCapability,
    double MisconceptionIndex,
    double ResponseLatency,
    double ErrorPattern,
    double ImprovementRate)
{
    public static EvidenceVector Empty => new(0, 0, 0, 0, 0, 0, 0, 0);
    
    public double OverallScore => 
        (ConceptualUnderstanding + ProceduralSkill + ApplicationAbility + TransferCapability) / 4.0 
        - (MisconceptionIndex * 0.2);
}

/// <summary>
/// Represents a knowledge gap identified in student understanding.
/// </summary>
public sealed record KnowledgeGap(
    Guid GapNodeId,
    GapType Type,
    double Severity,
    string Description,
    IReadOnlyList<Guid> PrerequisitesNeeded,
    IReadOnlyList<Guid> RelatedConceptsAffected);

/// <summary>
/// Evidence item supporting mastery assessment.
/// </summary>
public sealed record EvidenceItem(
    EvidenceType Type,
    double Strength,
    string Description,
    DateTimeOffset Timestamp,
    Guid? RelatedNodeId);

/// <summary>
/// Tracks mastery for a specific domain node.
/// </summary>
public sealed record KnowledgeMastery(
    Guid DomainNodeId,
    MasteryLevel Level,
    double Confidence,
    int PracticeAttempts,
    DateTimeOffset LastAssessed,
    IReadOnlyList<RetrievalEvent> History,
    IReadOnlyList<KnowledgeGap> IdentifiedGaps,
    double RetentionProbability,
    EvidenceVector Evidence)
{
    public static KnowledgeMastery CreateNew(Guid nodeId) => new(
        nodeId,
        MasteryLevel.Unknown,
        0.0,
        0,
        DateTimeOffset.MinValue,
        Array.Empty<RetrievalEvent>(),
        Array.Empty<KnowledgeGap>(),
        0.0,
        EvidenceVector.Empty);
}

/// <summary>
/// Tracks mastery for a specific learning objective.
/// </summary>
public sealed record LearningObjectiveMastery(
    Guid LearningObjectiveId,
    Guid SourceSlideId,
    MasteryLevel Level,
    double EvidenceScore,
    IReadOnlyList<EvidenceItem> SupportingEvidence,
    DateTimeOffset LastDemonstrated);

/// <summary>
/// Learning preferences for personalization.
/// </summary>
public sealed record LearningPreferences(
    ContentModality PreferredModality,
    DifficultyPreference Difficulty,
    IReadOnlyList<string> InterestAreas,
    double EngagementWithVisuals,
    double EngagementWithText,
    double EngagementWithExamples,
    double EngagementWithCases)
{
    public static LearningPreferences Default => new(
        ContentModality.Text,
        DifficultyPreference.Appropriate,
        Array.Empty<string>(),
        0.5, 0.5, 0.5, 0.5);
}

/// <summary>
/// Engagement metrics for tracking student activity.
/// </summary>
public sealed record EngagementMetrics(
    int TotalSessions,
    TimeSpan TotalTimeSpent,
    double AverageSessionLength,
    double AverageConfidence,
    int ConsecutiveDays,
    DateTimeOffset LastActiveAt);

/// <summary>
/// Analysis of student's current knowledge state.
/// </summary>
public sealed record KnowledgeAnalysis(
    IReadOnlyList<KnowledgeGap> CriticalGaps,
    IReadOnlyList<Guid> StrengthAreas,
    IReadOnlyList<Guid> PrerequisiteGaps,
    double OverallMasteryScore,
    IReadOnlyList<Guid> RecommendedNextNodes);

/// <summary>
/// Comprehensive student state model that tracks everything about a learner.
/// </summary>
public sealed class StudentStateModel
{
    private readonly Dictionary<Guid, KnowledgeMastery> _knowledgeMastery = new();
    private readonly Dictionary<Guid, LearningObjectiveMastery> _objectiveMastery = new();

    public Guid StudentId { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset LastUpdated { get; private set; }

    public LearningPreferences Preferences { get; private set; }
    public EngagementMetrics Engagement { get; private set; }
    public KnowledgeAnalysis CurrentAnalysis { get; set; }

    public IReadOnlyDictionary<Guid, KnowledgeMastery> KnowledgeMasteries
        => new ReadOnlyDictionary<Guid, KnowledgeMastery>(_knowledgeMastery);

    public IReadOnlyDictionary<Guid, LearningObjectiveMastery> ObjectiveMasteries
        => new ReadOnlyDictionary<Guid, LearningObjectiveMastery>(_objectiveMastery);

    public StudentStateModel(Guid studentId)
    {
        StudentId = studentId;
        CreatedAt = DateTimeOffset.UtcNow;
        LastUpdated = DateTimeOffset.UtcNow;
        Preferences = LearningPreferences.Default;
        Engagement = new EngagementMetrics(0, TimeSpan.Zero, 0, 0, 0, DateTimeOffset.UtcNow);
        CurrentAnalysis = new KnowledgeAnalysis(
            Array.Empty<KnowledgeGap>(),
            Array.Empty<Guid>(),
            Array.Empty<Guid>(),
            0.0,
            Array.Empty<Guid>());
    }

    /// <summary>
    /// Gets knowledge mastery for a specific node.
    /// </summary>
    public KnowledgeMastery GetKnowledgeMastery(Guid nodeId)
    {
        return _knowledgeMastery.TryGetValue(nodeId, out var mastery)
            ? mastery
            : KnowledgeMastery.CreateNew(nodeId);
    }

    /// <summary>
    /// Updates knowledge mastery for a node.
    /// </summary>
    public void UpdateKnowledgeMastery(KnowledgeMastery mastery)
    {
        _knowledgeMastery[mastery.DomainNodeId] = mastery;
        LastUpdated = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets objective mastery for a specific learning objective.
    /// </summary>
    public LearningObjectiveMastery? GetObjectiveMastery(Guid objectiveId)
    {
        return _objectiveMastery.GetValueOrDefault(objectiveId);
    }

    /// <summary>
    /// Updates learning objective mastery.
    /// </summary>
    public void UpdateObjectiveMastery(LearningObjectiveMastery mastery)
    {
        _objectiveMastery[mastery.LearningObjectiveId] = mastery;
        LastUpdated = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Updates learning preferences.
    /// </summary>
    public void UpdatePreferences(LearningPreferences preferences)
    {
        Preferences = preferences;
        LastUpdated = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Updates engagement metrics.
    /// </summary>
    public void UpdateEngagement(EngagementMetrics engagement)
    {
        Engagement = engagement;
        LastUpdated = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets recent mastery events across all nodes.
    /// </summary>
    public IEnumerable<RetrievalEvent> GetRecentEvents(int count = 10)
    {
        return _knowledgeMastery.Values
            .SelectMany(m => m.History)
            .OrderByDescending(e => e.Timestamp)
            .Take(count);
    }

    /// <summary>
    /// Initializes mastery for a set of domain nodes.
    /// </summary>
    public void InitializeMasteryForNodes(IEnumerable<Guid> nodeIds)
    {
        foreach (var nodeId in nodeIds)
        {
            if (!_knowledgeMastery.ContainsKey(nodeId))
            {
                _knowledgeMastery[nodeId] = KnowledgeMastery.CreateNew(nodeId);
            }
        }
        LastUpdated = DateTimeOffset.UtcNow;
    }
}
