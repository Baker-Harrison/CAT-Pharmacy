using System.Collections.ObjectModel;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Domain.Aggregates;

/// <summary>
/// The Knowledge Graph (Demand) - represents what the learner knows/can do per concept.
/// Updated ONLY via evidence, never directly. Implements deterministic state transitions.
/// </summary>
public sealed class KnowledgeGraph
{
    private readonly Dictionary<Guid, ConceptMastery> _masteryByConceptId = new();
    private readonly List<int> _latencyHistory = new();
    private readonly Dictionary<Guid, List<ErrorType>> _errorHistory = new();

    /// <summary>Learner this knowledge graph belongs to.</summary>
    public Guid LearnerId { get; }

    /// <summary>All concept masteries in this graph.</summary>
    public IReadOnlyDictionary<Guid, ConceptMastery> Masteries 
        => new ReadOnlyDictionary<Guid, ConceptMastery>(_masteryByConceptId);

    public KnowledgeGraph(Guid learnerId)
    {
        LearnerId = learnerId;
    }

    /// <summary>
    /// Gets the mastery state for a concept. Returns Unknown if never seen.
    /// </summary>
    public ConceptMastery GetMastery(Guid conceptId)
    {
        return _masteryByConceptId.TryGetValue(conceptId, out var mastery)
            ? mastery
            : ConceptMastery.CreateUnknown(conceptId);
    }

    /// <summary>
    /// Gets the weakest concepts (lowest mastery states).
    /// </summary>
    public IReadOnlyList<ConceptMastery> GetWeakestConcepts(int count)
    {
        return _masteryByConceptId.Values
            .OrderBy(m => (int)m.State)
            .ThenByDescending(m => m.DecayRisk)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Gets all concepts in a specific mastery state.
    /// </summary>
    public IReadOnlyList<ConceptMastery> GetConceptsByState(MasteryState state)
    {
        return _masteryByConceptId.Values
            .Where(m => m.State == state)
            .ToList();
    }

    /// <summary>
    /// Gets concepts at risk of decay (high decay risk value).
    /// </summary>
    public IReadOnlyList<ConceptMastery> GetAtRiskConcepts(double riskThreshold = 0.5)
    {
        return _masteryByConceptId.Values
            .Where(m => m.DecayRisk >= riskThreshold && m.State >= MasteryState.Fragile)
            .OrderByDescending(m => m.DecayRisk)
            .ToList();
    }

    /// <summary>
    /// Updates the knowledge graph from new evidence. Applies deterministic state transitions.
    /// </summary>
    public void UpdateFromEvidence(EvidenceRecord evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        foreach (var conceptId in evidence.ConceptIds)
        {
            var current = GetMastery(conceptId);
            var updated = ApplyEvidence(current, evidence);
            _masteryByConceptId[conceptId] = updated;
        }

        // Track latency for profile
        _latencyHistory.Add(evidence.LatencyMs);
    }

    /// <summary>
    /// Applies decay to all concepts based on time since last attempt.
    /// Call this periodically (e.g., at session start).
    /// </summary>
    public void ApplyDecay(TimeSpan decayWindow, DateTimeOffset now)
    {
        var updates = new List<(Guid, ConceptMastery)>();

        foreach (var (conceptId, mastery) in _masteryByConceptId)
        {
            if (mastery.LastAttemptAt.HasValue && mastery.State > MasteryState.Unknown)
            {
                var timeSince = now - mastery.LastAttemptAt.Value;
                var decayFactor = Math.Min(1.0, timeSince.TotalDays / decayWindow.TotalDays);
                
                // Decay risk increases with time
                var newDecayRisk = Math.Clamp(mastery.DecayRisk + decayFactor * 0.1, 0.0, 1.0);
                
                // If decay risk is high and state is Functional or above, may regress
                MasteryState newState = mastery.State;
                if (newDecayRisk >= 0.8 && mastery.State >= MasteryState.Functional)
                {
                    newState = (MasteryState)Math.Max((int)MasteryState.Fragile, (int)mastery.State - 1);
                }

                updates.Add((conceptId, mastery with
                {
                    DecayRisk = newDecayRisk,
                    State = newState
                }));
            }
        }

        foreach (var (conceptId, updated) in updates)
        {
            _masteryByConceptId[conceptId] = updated;
        }
    }

    /// <summary>
    /// Initializes mastery for a set of concepts (from Content Graph).
    /// </summary>
    public void InitializeConcepts(IEnumerable<Guid> conceptIds)
    {
        foreach (var conceptId in conceptIds)
        {
            if (!_masteryByConceptId.ContainsKey(conceptId))
            {
                _masteryByConceptId[conceptId] = ConceptMastery.CreateUnknown(conceptId);
            }
        }
    }

    /// <summary>
    /// Applies evidence to update a single concept's mastery using deterministic transition rules.
    /// </summary>
    private ConceptMastery ApplyEvidence(ConceptMastery current, EvidenceRecord evidence)
    {
        // Update error history
        if (!_errorHistory.ContainsKey(current.ConceptId))
            _errorHistory[current.ConceptId] = new List<ErrorType>();
        
        if (!evidence.IsCorrect && evidence.ErrorType != ErrorType.None)
            _errorHistory[current.ConceptId].Add(evidence.ErrorType);

        // Calculate new counts
        var newCorrectCount = current.CorrectCount + (evidence.IsCorrect ? 1 : 0);
        var newIncorrectCount = current.IncorrectCount + (evidence.IsCorrect ? 0 : 1);
        var newExplainWhyCount = current.ExplainWhySuccessCount + (evidence.IsExplainWhySuccess ? 1 : 0);
        var newApplicationCount = current.ApplicationSuccessCount + (evidence.IsApplicationSuccess ? 1 : 0);
        var newIntegrationCount = current.IntegrationSuccessCount + (evidence.IsIntegrationSuccess ? 1 : 0);

        // Determine new state via deterministic rules
        var newState = DetermineNextState(
            current.State,
            newCorrectCount,
            newIncorrectCount,
            newExplainWhyCount,
            newApplicationCount,
            newIntegrationCount,
            evidence.IsCorrect);

        // Calculate evidence strength (0-1)
        var totalAttempts = newCorrectCount + newIncorrectCount;
        var evidenceStrength = totalAttempts > 0 
            ? Math.Min(1.0, (double)newCorrectCount / totalAttempts * Math.Log(totalAttempts + 1) / 2)
            : 0.0;

        // Reset decay risk on new attempt
        var newDecayRisk = Math.Max(0, current.DecayRisk - 0.2);

        // Update common errors
        var commonErrors = _errorHistory.TryGetValue(current.ConceptId, out var errors)
            ? errors.GroupBy(e => e).OrderByDescending(g => g.Count()).Select(g => g.Key).Take(3).ToList()
            : new List<ErrorType>();

        return current with
        {
            State = newState,
            EvidenceStrength = evidenceStrength,
            LastAttemptAt = evidence.Timestamp,
            DecayRisk = newDecayRisk,
            CommonErrors = commonErrors,
            MedianLatency = CalculateMedianLatency(),
            CorrectCount = newCorrectCount,
            IncorrectCount = newIncorrectCount,
            ExplainWhySuccessCount = newExplainWhyCount,
            ApplicationSuccessCount = newApplicationCount,
            IntegrationSuccessCount = newIntegrationCount
        };
    }

    /// <summary>
    /// Deterministic state transition rules per product spec.
    /// </summary>
    private static MasteryState DetermineNextState(
        MasteryState currentState,
        int correctCount,
        int incorrectCount,
        int explainWhyCount,
        int applicationCount,
        int integrationCount,
        bool lastAttemptCorrect)
    {
        // Regression: repeated errors or high-latency collapse → Fragile
        if (!lastAttemptCorrect && incorrectCount >= 2 && currentState > MasteryState.Fragile)
        {
            // Check for consecutive errors (simplified: 2+ total errors regresses)
            return MasteryState.Fragile;
        }

        // Progression rules
        switch (currentState)
        {
            case MasteryState.Unknown:
                // Unknown → Fragile: any partial evidence
                if (correctCount > 0 || incorrectCount > 0)
                    return MasteryState.Fragile;
                break;

            case MasteryState.Fragile:
                // Fragile → Functional: 2+ correct with time/format separation
                // (Simplified: 2+ correct answers)
                if (correctCount >= 2)
                    return MasteryState.Functional;
                break;

            case MasteryState.Functional:
                // Functional → Robust: correct application + correct explain-why
                if (applicationCount >= 1 && explainWhyCount >= 1)
                    return MasteryState.Robust;
                break;

            case MasteryState.Robust:
                // Robust → TransferReady: novel integration success
                if (integrationCount >= 1)
                    return MasteryState.TransferReady;
                break;

            case MasteryState.TransferReady:
                // Already at max - maintain
                break;
        }

        return currentState;
    }

    private TimeSpan? CalculateMedianLatency()
    {
        if (_latencyHistory.Count == 0)
            return null;

        var sorted = _latencyHistory.OrderBy(l => l).ToList();
        var mid = sorted.Count / 2;
        var medianMs = sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2
            : sorted[mid];

        return TimeSpan.FromMilliseconds(medianMs);
    }
}
