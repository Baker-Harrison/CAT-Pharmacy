using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Application.Services;

/// <summary>
/// Service for selecting target nodes for adaptive lessons based on learner model and domain knowledge.
/// </summary>
public sealed class TargetSelectionService
{
    private readonly IDomainKnowledgeGraphRepository _domainGraphRepository;
    private readonly ILearnerModelRepository _learnerModelRepository;
    private readonly ILogger<TargetSelectionService> _logger;

    public TargetSelectionService(
        IDomainKnowledgeGraphRepository domainGraphRepository,
        ILearnerModelRepository learnerModelRepository,
        ILogger<TargetSelectionService> logger)
    {
        _domainGraphRepository = domainGraphRepository;
        _learnerModelRepository = learnerModelRepository;
        _logger = logger;
    }

    /// <summary>
    /// Selects the next target node for a learner.
    /// </summary>
    public async Task<TargetNode?> SelectNextTargetAsync(Guid learnerId, CancellationToken ct = default)
    {
        _logger.LogInformation("Selecting next target for learner {LearnerId}", learnerId);

        var domainGraph = await _domainGraphRepository.GetAsync(ct);
        if (domainGraph == null)
        {
            _logger.LogWarning("Domain Knowledge Graph not found");
            return null;
        }

        var learnerModel = await _learnerModelRepository.GetByLearnerAsync(learnerId, ct);
        if (learnerModel == null)
        {
            _logger.LogWarning("Learner Model not found for learner {LearnerId}", learnerId);
            return null;
        }

        // Get candidate nodes
        var candidates = await GetCandidateNodesAsync(domainGraph, learnerModel, ct);
        
        // Score and rank candidates
        var scoredCandidates = candidates
            .Select(c => (Candidate: c, Score: ScoreCandidate(c, learnerModel, domainGraph)))
            .OrderByDescending(x => x.Score)
            .ToList();

        if (!scoredCandidates.Any())
        {
            _logger.LogInformation("No suitable target nodes found for learner {LearnerId}", learnerId);
            return null;
        }

        var selected = scoredCandidates.First();
        _logger.LogInformation("Selected target node {NodeId} with score {Score}", 
            selected.Candidate.Id, selected.Score);

        return new TargetNode(
            selected.Candidate.Id,
            selected.Candidate.Title,
            selected.Score,
            GenerateRationale(selected.Candidate, learnerModel));
    }

    /// <summary>
    /// Gets multiple target nodes for batch selection.
    /// </summary>
    public async Task<IReadOnlyList<TargetNode>> SelectMultipleTargetsAsync(
        Guid learnerId, 
        int count = 5, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Selecting {Count} targets for learner {LearnerId}", count, learnerId);

        var domainGraph = await _domainGraphRepository.GetAsync(ct);
        if (domainGraph == null)
            return Array.Empty<TargetNode>();

        var learnerModel = await _learnerModelRepository.GetByLearnerAsync(learnerId, ct);
        if (learnerModel == null)
            return Array.Empty<TargetNode>();

        var candidates = await GetCandidateNodesAsync(domainGraph, learnerModel, ct);
        var scoredCandidates = candidates
            .Select(c => (Candidate: c, Score: ScoreCandidate(c, learnerModel, domainGraph)))
            .OrderByDescending(x => x.Score)
            .Take(count)
            .ToList();

        return scoredCandidates.Select(x => new TargetNode(
            x.Candidate.Id,
            x.Candidate.Title,
            x.Score,
            GenerateRationale(x.Candidate, learnerModel))).ToList();
    }

    private async Task<IReadOnlyList<DomainNode>> GetCandidateNodesAsync(
        DomainKnowledgeGraph domainGraph,
        LearnerModel learnerModel,
        CancellationToken ct)
    {
        var candidates = new List<DomainNode>();

        // 1. Remediation targets (mastery < 0.5)
        var remediationTargets = learnerModel.GetRemediationTargets()
            .Select(m => domainGraph.GetNode(m.NodeId))
            .Where(n => n != null)
            .Cast<DomainNode>()
            .ToList();
        candidates.AddRange(remediationTargets);

        // 2. Reinforcement targets (mastery 0.5-0.8)
        var reinforcementTargets = learnerModel.GetReinforcementTargets()
            .Select(m => domainGraph.GetNode(m.NodeId))
            .Where(n => n != null)
            .Cast<DomainNode>()
            .ToList();
        candidates.AddRange(reinforcementTargets);

        // 3. Spaced reactivation targets
        var reactivationTargets = learnerModel.GetSpacedReactivationTargets()
            .Select(m => domainGraph.GetNode(m.NodeId))
            .Where(n => n != null)
            .Cast<DomainNode>()
            .ToList();
        candidates.AddRange(reactivationTargets);

        // 4. New concepts (not exposed)
        var unexposedNodes = learnerModel.GetUnexposedNodes()
            .Where(m => m.NodeId != Guid.Empty) // Filter out placeholder
            .Select(m => domainGraph.GetNode(m.NodeId))
            .Where(n => n != null)
            .Cast<DomainNode>()
            .ToList();
        candidates.AddRange(unexposedNodes);

        // Filter out nodes that don't have prerequisites met
        var filteredCandidates = candidates
            .Where(c => ArePrerequisitesMet(c, learnerModel, domainGraph))
            .Distinct()
            .ToList();

        _logger.LogInformation("Found {CandidateCount} candidate target nodes", filteredCandidates.Count);
        return filteredCandidates;
    }

    private double ScoreCandidate(DomainNode candidate, LearnerModel learnerModel, DomainKnowledgeGraph domainGraph)
    {
        var mastery = learnerModel.GetMastery(candidate.Id);
        double score = 0;

        // Base score on mastery state
        score += mastery.State switch
        {
            MasteryState.Unknown => 100, // High priority for new content
            MasteryState.Fragile => 80,
            MasteryState.Functional => mastery.MasteryProbability < 0.6 ? 90 : 40,
            MasteryState.Robust => mastery.MasteryProbability < 0.8 ? 70 : 20,
            MasteryState.TransferReady => mastery.MasteryProbability < 0.9 ? 50 : 10,
            _ => 0
        };

        // Adjust based on exam relevance
        score += candidate.ExamRelevanceWeight * 20;

        // Adjust based on forgetting risk (for mastered content)
        if (mastery.LastRetrievalTimestamp.HasValue)
        {
            var daysSinceRetrieval = (DateTimeOffset.UtcNow - mastery.LastRetrievalTimestamp.Value).TotalDays;
            if (daysSinceRetrieval > 7)
            {
                score += Math.Min(daysSinceRetrieval * 2, 30); // Boost for spaced reactivation
            }
        }

        // Adjust based on prerequisite importance
        var dependents = new List<DomainNode>(); // TODO: Implement GetDependents in DomainKnowledgeGraph
        score += dependents.Count * 5; // Important if many concepts depend on it

        // Adjust based on error types
        if (mastery.ErrorTypes.Contains(ErrorType.Conceptual))
        {
            score += 15; // Boost for conceptual errors
        }
        else if (mastery.ErrorTypes.Contains(ErrorType.Procedural))
        {
            score += 10; // Boost for procedural errors
        }

        return Math.Round(score, 2);
    }

    private bool ArePrerequisitesMet(DomainNode node, LearnerModel learnerModel, DomainKnowledgeGraph domainGraph)
    {
        var prerequisites = domainGraph.GetPrerequisites(node.Id);
        
        foreach (var prereq in prerequisites)
        {
            var prereqMastery = learnerModel.GetMastery(prereq.Id);
            // Require at least Fragile state for prerequisites
            if (prereqMastery.State == MasteryState.Unknown)
                return false;
        }

        return true;
    }

    private string GenerateRationale(DomainNode node, LearnerModel learnerModel)
    {
        var mastery = learnerModel.GetMastery(node.Id);

        return mastery.State switch
        {
            MasteryState.Unknown => $"New concept: {node.Title}",
            MasteryState.Fragile => $"Needs reinforcement: {node.Title}",
            MasteryState.Functional => mastery.MasteryProbability < 0.6 
                ? $"Needs improvement: {node.Title}" 
                : $"Ready for advancement: {node.Title}",
            MasteryState.Robust => mastery.MasteryProbability < 0.8 
                ? $"Strengthen transfer skills: {node.Title}" 
                : $"Maintain mastery: {node.Title}",
            MasteryState.TransferReady => mastery.MasteryProbability < 0.9 
                ? $"Review needed: {node.Title}" 
                : $"Mastered: {node.Title}",
            _ => $"Target: {node.Title}"
        };
    }
}
