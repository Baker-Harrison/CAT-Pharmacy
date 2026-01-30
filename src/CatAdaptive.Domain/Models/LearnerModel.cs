using System.Collections.ObjectModel;

namespace CatAdaptive.Domain.Models;

/// <summary>
/// Represents a learner's mastery state for a specific domain node.
/// </summary>
public sealed record LearnerMastery(
    Guid NodeId,
    MasteryState State,
    double MasteryProbability,
    DateTimeOffset? LastRetrievalTimestamp,
    IReadOnlyList<RetrievalEvent> RetrievalHistory,
    IReadOnlyList<ErrorType> ErrorTypes,
    double ConfidenceRating);

/// <summary>
/// Represents a single retrieval event for a node.
/// </summary>
public sealed record RetrievalEvent(
    DateTimeOffset Timestamp,
    bool WasSuccessful,
    ErrorType ErrorType,
    double Confidence,
    int LatencyMs,
    PromptFormat PromptFormat);

/// <summary>
/// Learner Model - stores the learner's evolving mastery state mapped onto Domain Knowledge Graph nodes.
/// </summary>
public sealed class LearnerModel
{
    private readonly Dictionary<Guid, LearnerMastery> _masteryByNodeId = new();

    /// <summary>
    /// The learner this model belongs to.
    /// </summary>
    public Guid LearnerId { get; }

    /// <summary>
    /// All mastery states in this model.
    /// </summary>
    public IReadOnlyDictionary<Guid, LearnerMastery> Masteries 
        => new ReadOnlyDictionary<Guid, LearnerMastery>(_masteryByNodeId);

    public LearnerModel(Guid learnerId)
    {
        LearnerId = learnerId;
    }

    /// <summary>
    /// Gets the mastery state for a node. Returns Unknown if never seen.
    /// </summary>
    public LearnerMastery GetMastery(Guid nodeId)
    {
        return _masteryByNodeId.TryGetValue(nodeId, out var mastery)
            ? mastery
            : new LearnerMastery(
                nodeId,
                MasteryState.Unknown,
                0.0,
                null,
                Array.Empty<RetrievalEvent>(),
                Array.Empty<ErrorType>(),
                0.0);
    }

    /// <summary>
    /// Updates mastery for a node based on a retrieval event.
    /// </summary>
    public void UpdateFromRetrieval(Guid nodeId, RetrievalEvent retrievalEvent)
    {
        var current = GetMastery(nodeId);
        var newHistory = new List<RetrievalEvent>(current.RetrievalHistory) { retrievalEvent };
        
        // Calculate new mastery probability based on success rate
        var successfulRetrievals = newHistory.Count(r => r.WasSuccessful);
        var newProbability = newHistory.Count > 0 
            ? (double)successfulRetrievals / newHistory.Count
            : 0.0;

        // Determine new mastery state
        var newState = DetermineMasteryState(newProbability, newHistory, current.State);

        // Update error types
        var errorTypes = newHistory
            .Where(r => !r.WasSuccessful && r.ErrorType != ErrorType.None)
            .Select(r => r.ErrorType)
            .Distinct()
            .ToList();

        // Update confidence rating (exponential moving average)
        var newConfidence = current.ConfidenceRating * 0.7 + retrievalEvent.Confidence * 0.3;

        _masteryByNodeId[nodeId] = current with
        {
            State = newState,
            MasteryProbability = newProbability,
            LastRetrievalTimestamp = retrievalEvent.Timestamp,
            RetrievalHistory = newHistory,
            ErrorTypes = errorTypes,
            ConfidenceRating = newConfidence
        };
    }

    /// <summary>
    /// Gets nodes that need remediation (mastery probability < 0.5).
    /// </summary>
    public IReadOnlyList<LearnerMastery> GetRemediationTargets()
        => _masteryByNodeId.Values
            .Where(m => m.MasteryProbability < 0.5 && m.State != MasteryState.Unknown)
            .OrderBy(m => m.MasteryProbability)
            .ToList();

    /// <summary>
    /// Gets nodes that need reinforcement (mastery probability 0.5-0.8).
    /// </summary>
    public IReadOnlyList<LearnerMastery> GetReinforcementTargets()
        => _masteryByNodeId.Values
            .Where(m => m.MasteryProbability >= 0.5 && m.MasteryProbability < 0.8)
            .OrderByDescending(m => m.MasteryProbability)
            .ToList();

    /// <summary>
    /// Gets nodes ready for spaced reactivation (mastery probability >= 0.8).
    /// </summary>
    public IReadOnlyList<LearnerMastery> GetSpacedReactivationTargets()
        => _masteryByNodeId.Values
            .Where(m => m.MasteryProbability >= 0.8 && 
                       m.LastRetrievalTimestamp.HasValue &&
                       DateTimeOffset.UtcNow - m.LastRetrievalTimestamp.Value > TimeSpan.FromDays(7))
            .OrderBy(m => m.LastRetrievalTimestamp)
            .ToList();

    /// <summary>
    /// Gets nodes that have never been exposed.
    /// </summary>
    public IReadOnlyList<LearnerMastery> GetUnexposedNodes()
        => _masteryByNodeId.Values
            .Where(m => m.State == MasteryState.Unknown)
            .ToList();

    /// <summary>
    /// Initializes mastery for a set of nodes as Unknown.
    /// </summary>
    public void InitializeNodes(IEnumerable<Guid> nodeIds, IEnumerable<Guid> prerequisites)
    {
        foreach (var prereq in prerequisites)
        {
            var prereqMastery = GetMastery(prereq);
            // Require at least Fragile state for prerequisites
            if (prereqMastery.State == MasteryState.Unknown)
                return;
        }

        foreach (var nodeId in nodeIds)
        {
            if (!_masteryByNodeId.ContainsKey(nodeId))
            {
                _masteryByNodeId[nodeId] = new LearnerMastery(
                    nodeId,
                    MasteryState.Unknown,
                    0.0,
                    null,
                    Array.Empty<RetrievalEvent>(),
                    Array.Empty<ErrorType>(),
                    0.0);
            }
        }
    }

    /// <summary>
    /// Determines mastery state based on probability and history.
    /// </summary>
    private static MasteryState DetermineMasteryState(
        double probability,
        IReadOnlyList<RetrievalEvent> history,
        MasteryState currentState)
    {
        // Can't regress from Unknown
        if (currentState == MasteryState.Unknown && history.Count == 0)
            return MasteryState.Unknown;

        // Check for recent failures that might indicate regression
        var recentEvents = history.TakeLast(5).ToList();
        var recentFailureRate = recentEvents.Count > 0 
            ? (double)recentEvents.Count(e => !e.WasSuccessful) / recentEvents.Count
            : 0.0;

        // Regression rule: high recent failure rate
        if (currentState > MasteryState.Fragile && recentFailureRate >= 0.6)
            return MasteryState.Fragile;

        // Progression rules based on probability and consistency
        return probability switch
        {
            < 0.2 => MasteryState.Unknown,
            < 0.4 => MasteryState.Fragile,
            < 0.6 => MasteryState.Functional,
            < 0.8 => MasteryState.Robust,
            _ => MasteryState.TransferReady
        };
    }
}
