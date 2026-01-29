using System.Collections.ObjectModel;

namespace CatAdaptive.Domain.Models;

/// <summary>
/// Tracks a learner's mastery of a single concept.
/// Updated only via evidence (never directly).
/// </summary>
public sealed record ConceptMastery
{
    public Guid ConceptId { get; init; }
    
    /// <summary>Current mastery state.</summary>
    public MasteryState State { get; init; } = MasteryState.Unknown;
    
    /// <summary>Cumulative evidence strength (0-1 scale).</summary>
    public double EvidenceStrength { get; init; } = 0.0;
    
    /// <summary>When the learner last attempted this concept.</summary>
    public DateTimeOffset? LastAttemptAt { get; init; }
    
    /// <summary>Risk of knowledge decay (0-1, higher = more at risk).</summary>
    public double DecayRisk { get; init; } = 0.0;
    
    /// <summary>Common error types the learner makes on this concept.</summary>
    public IReadOnlyList<ErrorType> CommonErrors { get; init; } = Array.Empty<ErrorType>();
    
    /// <summary>Median response latency (for tracking fluency).</summary>
    public TimeSpan? MedianLatency { get; init; }
    
    /// <summary>Count of correct responses.</summary>
    public int CorrectCount { get; init; } = 0;
    
    /// <summary>Count of incorrect responses.</summary>
    public int IncorrectCount { get; init; } = 0;
    
    /// <summary>Count of explain-why successes (for Robust state transition).</summary>
    public int ExplainWhySuccessCount { get; init; } = 0;
    
    /// <summary>Count of application successes (for Robust state transition).</summary>
    public int ApplicationSuccessCount { get; init; } = 0;
    
    /// <summary>Count of integration successes (for TransferReady state transition).</summary>
    public int IntegrationSuccessCount { get; init; } = 0;

    /// <summary>
    /// Creates an initial unknown mastery state for a concept.
    /// </summary>
    public static ConceptMastery CreateUnknown(Guid conceptId)
        => new() { ConceptId = conceptId };

    /// <summary>
    /// Returns total attempts for this concept.
    /// </summary>
    public int TotalAttempts => CorrectCount + IncorrectCount;

    /// <summary>
    /// Returns accuracy rate (0-1).
    /// </summary>
    public double AccuracyRate => TotalAttempts > 0 ? (double)CorrectCount / TotalAttempts : 0.0;
}
