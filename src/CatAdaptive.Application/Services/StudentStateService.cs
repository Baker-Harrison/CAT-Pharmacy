using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Application.Services;

/// <summary>
/// Service for managing student state and mastery tracking.
/// </summary>
public sealed class StudentStateService
{
    private const double AtRiskRetentionThreshold = 0.6;
    private const double AtRiskConfidenceThreshold = 0.4;
    private const int RecommendedNodeCount = 5;

    private readonly IStudentStateRepository _repository;
    private readonly IDomainGraphRepository _domainGraphRepository;
    private readonly IGeminiService _gemini;
    private readonly ILogger<StudentStateService> _logger;

    public StudentStateService(
        IStudentStateRepository repository,
        IDomainGraphRepository domainGraphRepository,
        IGeminiService gemini,
        ILogger<StudentStateService> logger)
    {
        _repository = repository;
        _domainGraphRepository = domainGraphRepository;
        _gemini = gemini;
        _logger = logger;
    }

    /// <summary>
    /// Gets student state by ID, creating if not exists.
    /// </summary>
    public async Task<StudentStateModel> GetOrCreateStudentStateAsync(
        Guid studentId,
        CancellationToken ct = default)
    {
        var state = await _repository.GetByStudentAsync(studentId, ct);
        
        if (state == null)
        {
            _logger.LogInformation("Creating new student state for {StudentId}", studentId);
            state = new StudentStateModel(studentId);
            
            // Initialize with domain nodes if available
            var domainGraph = await _domainGraphRepository.GetAsync(ct);
            if (domainGraph != null)
            {
                state.InitializeMasteryForNodes(domainGraph.Nodes.Keys);
            }
            
            await _repository.SaveAsync(state, ct);
        }
        
        return state;
    }

    /// <summary>
    /// Updates mastery based on a learning event.
    /// </summary>
    public async Task<StudentStateModel> UpdateMasteryAsync(
        Guid studentId,
        MasteryEvent masteryEvent,
        CancellationToken ct = default)
    {
        var state = await GetOrCreateStudentStateAsync(studentId, ct);
        var currentMastery = state.GetKnowledgeMastery(masteryEvent.NodeId);
        
        // Calculate updated mastery using Bayesian approach
        var updatedMastery = CalculateUpdatedMastery(currentMastery, masteryEvent);
        
        state.UpdateKnowledgeMastery(updatedMastery);
        
        // Recalculate analysis
        state.CurrentAnalysis = await AnalyzeKnowledgeStateAsync(state, ct);
        
        await _repository.SaveAsync(state, ct);
        
        _logger.LogInformation(
            "Updated mastery for student {StudentId}, node {NodeId}: {Level} ({Confidence:P})",
            studentId, masteryEvent.NodeId, updatedMastery.Level, updatedMastery.Confidence);
        
        return state;
    }

    /// <summary>
    /// Analyzes current knowledge state and identifies gaps.
    /// </summary>
    public async Task<KnowledgeAnalysis> AnalyzeKnowledgeStateAsync(
        StudentStateModel state,
        CancellationToken ct = default)
    {
        var domainGraph = await _domainGraphRepository.GetAsync(ct);
        
        // Identify critical gaps
        var criticalGaps = IdentifyCriticalGaps(state, domainGraph);
        
        // Identify strengths
        var strengths = state.KnowledgeMasteries.Values
            .Where(m => m.Level >= MasteryLevel.Proficient)
            .Select(m => m.DomainNodeId)
            .ToList();
        
        // Identify prerequisite gaps
        var prereqGaps = IdentifyPrerequisiteGaps(state, domainGraph);
        
        // Calculate overall mastery
        var overallMastery = CalculateOverallMastery(state);

        // Identify at-risk concepts for spaced repetition
        var atRiskNodes = IdentifyAtRiskConcepts(state, domainGraph);
        
        // Get AI recommendations for next nodes
        var recommendedNodes = await GetRecommendedNextNodesAsync(state, domainGraph, atRiskNodes, ct);
        
        return new KnowledgeAnalysis(
            CriticalGaps: criticalGaps,
            StrengthAreas: strengths,
            PrerequisiteGaps: prereqGaps,
            OverallMasteryScore: overallMastery,
            RecommendedNextNodes: recommendedNodes);
    }

    /// <summary>
    /// Updates learning preferences based on engagement data.
    /// </summary>
    public async Task UpdatePreferencesAsync(
        Guid studentId,
        LearningPreferences preferences,
        CancellationToken ct = default)
    {
        var state = await GetOrCreateStudentStateAsync(studentId, ct);
        state.UpdatePreferences(preferences);
        await _repository.SaveAsync(state, ct);
    }

    /// <summary>
    /// Records a learning session for engagement tracking.
    /// </summary>
    public async Task RecordSessionAsync(
        Guid studentId,
        TimeSpan sessionDuration,
        double averageConfidence,
        CancellationToken ct = default)
    {
        var state = await GetOrCreateStudentStateAsync(studentId, ct);
        
        var currentEngagement = state.Engagement;
        var newEngagement = new EngagementMetrics(
            TotalSessions: currentEngagement.TotalSessions + 1,
            TotalTimeSpent: currentEngagement.TotalTimeSpent + sessionDuration,
            AverageSessionLength: (currentEngagement.AverageSessionLength * currentEngagement.TotalSessions + sessionDuration.TotalMinutes) 
                                 / (currentEngagement.TotalSessions + 1),
            AverageConfidence: (currentEngagement.AverageConfidence * currentEngagement.TotalSessions + averageConfidence) 
                              / (currentEngagement.TotalSessions + 1),
            ConsecutiveDays: CalculateConsecutiveDays(currentEngagement.LastActiveAt),
            LastActiveAt: DateTimeOffset.UtcNow);
        
        state.UpdateEngagement(newEngagement);
        await _repository.SaveAsync(state, ct);
    }

    private KnowledgeMastery CalculateUpdatedMastery(
        KnowledgeMastery current,
        MasteryEvent masteryEvent)
    {
        // Bayesian update of mastery probability
        var prior = current.Confidence;
        var likelihood = CalculateLikelihood(masteryEvent);
        var posterior = CalculatePosterior(prior, likelihood);
        
        // Update evidence vector
        var newEvidence = UpdateEvidenceVector(current.Evidence, masteryEvent);
        
        // Calculate retention probability (forgetting curve)
        var retention = CalculateRetention(current.LastAssessed);
        
        // Determine new mastery level
        var newLevel = DetermineMasteryLevel(posterior, newEvidence);
        
        // Create new retrieval event
        var retrievalEvent = new RetrievalEvent(
            Timestamp: masteryEvent.Timestamp,
            WasSuccessful: masteryEvent.Strength > 0.5,
            ErrorType: masteryEvent.Strength < 0.5 ? GapType.Conceptual : null,
            Confidence: masteryEvent.Confidence,
            LatencyMs: 0,
            PromptFormat: masteryEvent.Type.ToString());
        
        var newHistory = current.History.Append(retrievalEvent).TakeLast(20).ToList();
        
        return new KnowledgeMastery(
            DomainNodeId: current.DomainNodeId,
            Level: newLevel,
            Confidence: posterior,
            PracticeAttempts: current.PracticeAttempts + 1,
            LastAssessed: masteryEvent.Timestamp,
            History: newHistory,
            IdentifiedGaps: current.IdentifiedGaps,
            RetentionProbability: retention,
            Evidence: newEvidence);
    }

    private double CalculateLikelihood(MasteryEvent masteryEvent)
    {
        var typeWeight = masteryEvent.Type switch
        {
            MasteryEventType.DiagnosticAssessment => 1.0,
            MasteryEventType.FormativeCheck => 0.8,
            MasteryEventType.PracticeAttempt => 0.6,
            MasteryEventType.SpacedRetrieval => 0.9,
            _ => 0.5
        };
        
        return masteryEvent.Strength * typeWeight * masteryEvent.Confidence;
    }

    private double CalculatePosterior(double prior, double likelihood)
    {
        // Simple Bayesian update
        var evidenceWeight = 0.3; // How much new evidence affects belief
        return prior * (1 - evidenceWeight) + likelihood * evidenceWeight;
    }

    private EvidenceVector UpdateEvidenceVector(EvidenceVector current, MasteryEvent masteryEvent)
    {
        var updateFactor = 0.2;
        var strength = masteryEvent.Strength;
        
        return new EvidenceVector(
            ConceptualUnderstanding: current.ConceptualUnderstanding * (1 - updateFactor) + strength * updateFactor,
            ProceduralSkill: current.ProceduralSkill * (1 - updateFactor) + strength * updateFactor,
            ApplicationAbility: masteryEvent.Context.BloomsLevel >= BloomsLevel.Apply 
                ? current.ApplicationAbility * (1 - updateFactor) + strength * updateFactor 
                : current.ApplicationAbility,
            TransferCapability: masteryEvent.Context.BloomsLevel >= BloomsLevel.Analyze
                ? current.TransferCapability * (1 - updateFactor) + strength * updateFactor
                : current.TransferCapability,
            MisconceptionIndex: strength < 0.5 
                ? Math.Min(1.0, current.MisconceptionIndex + 0.1) 
                : Math.Max(0.0, current.MisconceptionIndex - 0.05),
            ResponseLatency: current.ResponseLatency,
            ErrorPattern: strength < 0.5 
                ? Math.Min(1.0, current.ErrorPattern + 0.1) 
                : current.ErrorPattern * 0.9,
            ImprovementRate: CalculateImprovementRate(current, strength));
    }

    private double CalculateImprovementRate(EvidenceVector current, double newStrength)
    {
        var diff = newStrength - current.OverallScore;
        return current.ImprovementRate * 0.8 + diff * 0.2;
    }

    private double CalculateRetention(DateTimeOffset lastAssessed)
    {
        if (lastAssessed == DateTimeOffset.MinValue)
            return 0.0;
        
        var daysSince = (DateTimeOffset.UtcNow - lastAssessed).TotalDays;
        
        // Ebbinghaus forgetting curve approximation
        return Math.Exp(-0.1 * daysSince);
    }

    private MasteryLevel DetermineMasteryLevel(double confidence, EvidenceVector evidence)
    {
        var overall = (confidence + evidence.OverallScore) / 2.0;
        
        return overall switch
        {
            >= 0.75 => MasteryLevel.Advanced,
            >= 0.50 => MasteryLevel.Proficient,
            >= 0.25 => MasteryLevel.Developing,
            > 0.0 => MasteryLevel.Novice,
            _ => MasteryLevel.Unknown
        };
    }

    private IReadOnlyList<KnowledgeGap> IdentifyCriticalGaps(
        StudentStateModel state,
        DomainKnowledgeGraph? domainGraph)
    {
        var gaps = new List<KnowledgeGap>();
        
        foreach (var mastery in state.KnowledgeMasteries.Values)
        {
            if (mastery.Level < MasteryLevel.Developing && mastery.PracticeAttempts > 0)
            {
                var decayAdjustedConfidence = GetDecayAdjustedConfidence(mastery);
                var node = domainGraph?.GetNode(mastery.DomainNodeId);
                var prereqs = domainGraph?.GetPrerequisites(mastery.DomainNodeId) ?? Array.Empty<DomainNode>();
                
                gaps.Add(new KnowledgeGap(
                    GapNodeId: mastery.DomainNodeId,
                    Type: DetermineGapType(mastery),
                    Severity: Math.Clamp(1.0 - decayAdjustedConfidence, 0.0, 1.0),
                    Description: $"Struggling with {node?.Title ?? "concept"}",
                    PrerequisitesNeeded: prereqs.Select(p => p.Id).ToList(),
                    RelatedConceptsAffected: Array.Empty<Guid>()));
            }
        }
        
        return gaps.OrderByDescending(g => g.Severity).ToList();
    }

    private GapType DetermineGapType(KnowledgeMastery mastery)
    {
        if (mastery.Evidence.MisconceptionIndex > 0.5)
            return GapType.Conceptual;
        if (mastery.Evidence.ProceduralSkill < 0.3)
            return GapType.Procedural;
        if (mastery.Evidence.ApplicationAbility < 0.3)
            return GapType.Transfer;
        return GapType.Factual;
    }

    private IReadOnlyList<Guid> IdentifyPrerequisiteGaps(
        StudentStateModel state,
        DomainKnowledgeGraph? domainGraph)
    {
        if (domainGraph == null) return Array.Empty<Guid>();
        
        var prereqGaps = new List<Guid>();
        
        foreach (var mastery in state.KnowledgeMasteries.Values)
        {
            if (mastery.Level < MasteryLevel.Proficient)
            {
                var prereqs = domainGraph.GetPrerequisites(mastery.DomainNodeId);
                foreach (var prereq in prereqs)
                {
                    var prereqMastery = state.GetKnowledgeMastery(prereq.Id);
                    if (prereqMastery.Level < MasteryLevel.Developing)
                    {
                        prereqGaps.Add(prereq.Id);
                    }
                }
            }
        }
        
        return prereqGaps.Distinct().ToList();
    }

    private double CalculateOverallMastery(StudentStateModel state)
    {
        if (!state.KnowledgeMasteries.Any())
            return 0.0;
        
        return state.KnowledgeMasteries.Values.Average(m => GetDecayAdjustedConfidence(m));
    }

    private async Task<IReadOnlyList<Guid>> GetRecommendedNextNodesAsync(
        StudentStateModel state,
        DomainKnowledgeGraph? domainGraph,
        IReadOnlyList<Guid> atRiskNodes,
        CancellationToken ct)
    {
        if (domainGraph == null)
            return Array.Empty<Guid>();
        
        var prioritizedReview = atRiskNodes
            .Where(domainGraph.Nodes.ContainsKey)
            .Distinct()
            .ToList();

        // Find nodes that are ready to learn (prerequisites met)
        var readyNodes = domainGraph.Nodes.Values
            .Where(node =>
            {
                var mastery = state.GetKnowledgeMastery(node.Id);
                if (mastery.Level >= MasteryLevel.Proficient)
                    return false; // Already mastered
                
                var prereqs = domainGraph.GetPrerequisites(node.Id);
                return prereqs.All(p => 
                    state.GetKnowledgeMastery(p.Id).Level >= MasteryLevel.Developing);
            })
            .OrderByDescending(n => n.ExamRelevanceWeight)
            .ThenBy(n => GetDecayAdjustedConfidence(state.GetKnowledgeMastery(n.Id)))
            .Select(m => m.DomainNodeId)
            .ToList();

        var combined = prioritizedReview
            .Concat(readyNodes)
            .Distinct()
            .Take(RecommendedNodeCount)
            .ToList();

        return combined;
    }

    private IReadOnlyList<Guid> IdentifyAtRiskConcepts(
        StudentStateModel state,
        DomainKnowledgeGraph? domainGraph)
    {
        if (domainGraph == null)
            return Array.Empty<Guid>();

        return state.KnowledgeMasteries.Values
            .Where(m => m.PracticeAttempts > 0)
            .Where(m =>
            {
                var retention = CalculateRetention(m.LastAssessed);
                var decayAdjusted = GetDecayAdjustedConfidence(m);
                return retention < AtRiskRetentionThreshold ||
                       (m.Level >= MasteryLevel.Proficient && decayAdjusted < AtRiskConfidenceThreshold);
            })
            .OrderBy(m => CalculateRetention(m.LastAssessed))
            .ThenBy(m => GetDecayAdjustedConfidence(m))
            .Select(n => n.Id)
            .ToList();
    }

    private double GetDecayAdjustedConfidence(KnowledgeMastery mastery)
    {
        if (mastery.LastAssessed == DateTimeOffset.MinValue)
            return mastery.Confidence;

        var retention = CalculateRetention(mastery.LastAssessed);
        return mastery.Confidence * retention;
    }

    private int CalculateConsecutiveDays(DateTimeOffset lastActive)
    {
        var today = DateTimeOffset.UtcNow.Date;
        var lastActiveDate = lastActive.Date;
        
        if ((today - lastActiveDate).TotalDays <= 1)
            return 1; // Continue streak or same day
        
        return 0; // Streak broken
    }
}
