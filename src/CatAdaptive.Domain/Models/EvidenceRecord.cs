using System.Collections.ObjectModel;

namespace CatAdaptive.Domain.Models;

/// <summary>
/// A single evidence record capturing learner performance on a prompt.
/// This is the ONLY source of truth for updating the Knowledge Graph.
/// </summary>
public sealed record EvidenceRecord
{
    /// <summary>Unique identifier for this evidence record.</summary>
    public Guid Id { get; init; }
    
    /// <summary>The learner this evidence belongs to.</summary>
    public Guid LearnerId { get; init; }
    
    /// <summary>The prompt ID that generated this evidence.</summary>
    public Guid PromptId { get; init; }
    
    /// <summary>Concept IDs assessed by this prompt.</summary>
    public IReadOnlyList<Guid> ConceptIds { get; init; } = Array.Empty<Guid>();
    
    /// <summary>The learner's raw response text.</summary>
    public string ResponseText { get; init; } = string.Empty;
    
    /// <summary>Whether the response was correct.</summary>
    public bool IsCorrect { get; init; }
    
    /// <summary>Quality score for explanation (0-1, rubric-based).</summary>
    public double ExplanationQuality { get; init; }
    
    /// <summary>Response latency in milliseconds.</summary>
    public int LatencyMs { get; init; }
    
    /// <summary>Learner's self-reported confidence (0-1, optional).</summary>
    public double? ConfidenceSelfReport { get; init; }
    
    /// <summary>Classified error type if incorrect.</summary>
    public ErrorType ErrorType { get; init; } = ErrorType.None;
    
    /// <summary>Format of the prompt that generated this evidence.</summary>
    public PromptFormat PromptFormat { get; init; } = PromptFormat.ShortAnswer;
    
    /// <summary>When this evidence was recorded.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Creates a new evidence record.
    /// </summary>
    public static EvidenceRecord Create(
        Guid learnerId,
        Guid promptId,
        IEnumerable<Guid> conceptIds,
        string responseText,
        bool isCorrect,
        double explanationQuality,
        int latencyMs,
        PromptFormat promptFormat,
        ErrorType errorType = ErrorType.None,
        double? confidenceSelfReport = null,
        DateTimeOffset? timestamp = null)
    {
        return new EvidenceRecord
        {
            Id = Guid.NewGuid(),
            LearnerId = learnerId,
            PromptId = promptId,
            ConceptIds = conceptIds?.ToList() ?? new List<Guid>(),
            ResponseText = responseText,
            IsCorrect = isCorrect,
            ExplanationQuality = Math.Clamp(explanationQuality, 0.0, 1.0),
            LatencyMs = Math.Max(0, latencyMs),
            PromptFormat = promptFormat,
            ErrorType = isCorrect ? ErrorType.None : errorType,
            ConfidenceSelfReport = confidenceSelfReport.HasValue 
                ? Math.Clamp(confidenceSelfReport.Value, 0.0, 1.0) 
                : null,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Returns true if this evidence demonstrates explain-why capability.
    /// </summary>
    public bool IsExplainWhySuccess => 
        IsCorrect && 
        PromptFormat == PromptFormat.ExplainWhy && 
        ExplanationQuality >= 0.7;

    /// <summary>
    /// Returns true if this evidence demonstrates application capability.
    /// </summary>
    public bool IsApplicationSuccess => 
        IsCorrect && 
        PromptFormat == PromptFormat.Application;

    /// <summary>
    /// Returns true if this evidence demonstrates integration capability.
    /// </summary>
    public bool IsIntegrationSuccess => 
        IsCorrect && 
        PromptFormat == PromptFormat.Integration;
}
