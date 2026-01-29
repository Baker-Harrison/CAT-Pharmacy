namespace CatAdaptive.Domain.Models;

/// <summary>
/// Rubric for evaluating a response to a prompt.
/// </summary>
public sealed record EvaluationRubric
{
    /// <summary>Key points that must be present for correctness.</summary>
    public IReadOnlyList<string> RequiredPoints { get; init; } = Array.Empty<string>();

    /// <summary>Keywords/concepts that indicate understanding.</summary>
    public IReadOnlyList<string> KeyConcepts { get; init; } = Array.Empty<string>();

    /// <summary>Common misconceptions to detect as errors.</summary>
    public IReadOnlyList<string> CommonMisconceptions { get; init; } = Array.Empty<string>();

    /// <summary>Minimum explanation quality threshold (0-1).</summary>
    public double MinExplanationQuality { get; init; } = 0.7;

    public static EvaluationRubric Create(
        IEnumerable<string>? requiredPoints = null,
        IEnumerable<string>? keyConcepts = null,
        IEnumerable<string>? commonMisconceptions = null,
        double minExplanationQuality = 0.7)
    {
        return new EvaluationRubric
        {
            RequiredPoints = requiredPoints?.ToList() ?? new List<string>(),
            KeyConcepts = keyConcepts?.ToList() ?? new List<string>(),
            CommonMisconceptions = commonMisconceptions?.ToList() ?? new List<string>(),
            MinExplanationQuality = Math.Clamp(minExplanationQuality, 0.0, 1.0)
        };
    }
}
