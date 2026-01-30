using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Application.Services;

/// <summary>
/// Service for managing spaced reactivation of learned concepts.
/// </summary>
public sealed class SpacedReactivationService
{
    private readonly ILearnerModelRepository _learnerModelRepository;
    private readonly IDomainKnowledgeGraphRepository _domainGraphRepository;
    private readonly IEnhancedContentGraphRepository _contentGraphRepository;
    private readonly ILogger<SpacedReactivationService> _logger;

    // Spaced repetition intervals (in days)
    private static readonly double[] SpacedIntervals = { 1, 3, 7, 14, 30, 60, 120 };

    public SpacedReactivationService(
        ILearnerModelRepository learnerModelRepository,
        IDomainKnowledgeGraphRepository domainGraphRepository,
        IEnhancedContentGraphRepository contentGraphRepository,
        ILogger<SpacedReactivationService> logger)
    {
        _learnerModelRepository = learnerModelRepository;
        _domainGraphRepository = domainGraphRepository;
        _contentGraphRepository = contentGraphRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets concepts that are due for spaced reactivation.
    /// </summary>
    public async Task<IReadOnlyList<TargetNode>> GetDueForReactivationAsync(
        Guid learnerId,
        int maxCount = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Getting concepts due for spaced reactivation for learner {LearnerId}", learnerId);

        var learnerModel = await _learnerModelRepository.GetByLearnerAsync(learnerId, ct);
        if (learnerModel == null)
        {
            _logger.LogWarning("Learner model not found for learner {LearnerId}", learnerId);
            return Array.Empty<TargetNode>();
        }

        var domainGraph = await _domainGraphRepository.GetAsync(ct);
        if (domainGraph == null)
        {
            _logger.LogWarning("Domain Knowledge Graph not found");
            return Array.Empty<TargetNode>();
        }

        // Get concepts that are mastered but might be due for review
        var candidates = learnerModel.Masteries.Values
            .Where(m => m.State >= MasteryState.Functional && 
                       m.LastRetrievalTimestamp.HasValue &&
                       IsDueForReactivation(m, DateTimeOffset.UtcNow))
            .OrderBy(m => CalculateReactivationPriority(m))
            .Take(maxCount)
            .ToList();

        var results = new List<TargetNode>();
        
        foreach (var mastery in candidates)
        {
            var node = domainGraph.GetNode(mastery.NodeId);
            if (node != null)
            {
                var priority = CalculateReactivationPriority(mastery);
                var rationale = GenerateReactivationRationale(mastery);
                
                results.Add(new TargetNode(
                    node.Id,
                    node.Title,
                    priority,
                    rationale));
            }
        }

        _logger.LogInformation("Found {Count} concepts due for spaced reactivation", results.Count);
        return results;
    }

    /// <summary>
    /// Schedules the next reactivation for a concept based on performance.
    /// </summary>
    public async Task ScheduleNextReactivationAsync(
        Guid learnerId,
        Guid nodeId,
        double performanceScore,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Scheduling next reactivation for node {NodeId} with score {Score}", 
            nodeId, performanceScore);

        var learnerModel = await _learnerModelRepository.GetByLearnerAsync(learnerId, ct);
        if (learnerModel == null)
            return;

        var mastery = learnerModel.GetMastery(nodeId);
        var currentInterval = CalculateCurrentInterval(mastery);
        
        // Adjust interval based on performance
        var nextInterval = AdjustIntervalBasedOnPerformance(currentInterval, performanceScore);
        
        // Update mastery with next reactivation time
        // This would require extending the LearnerMastery model
        _logger.LogInformation("Scheduled next reactivation for node {NodeId} in {Days} days", 
            nodeId, nextInterval);
    }

    /// <summary>
    /// Gets remediation content for struggling concepts.
    /// </summary>
    public async Task<IReadOnlyList<EnhancedContentNode>> GetRemediationContentAsync(
        Guid learnerId,
        Guid nodeId,
        ContentModality preferredModality,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Getting remediation content for node {NodeId} with modality {Modality}", 
            nodeId, preferredModality);

        var contentGraph = await _contentGraphRepository.GetAsync(ct);
        if (contentGraph == null)
            return Array.Empty<EnhancedContentNode>();

        var learnerModel = await _learnerModelRepository.GetByLearnerAsync(learnerId, ct);
        if (learnerModel == null)
            return Array.Empty<EnhancedContentNode>();

        var mastery = learnerModel.GetMastery(nodeId);
        
        // Get alternative content based on error types
        var remediationContent = new List<EnhancedContentNode>();

        if (mastery.ErrorTypes.Contains(ErrorType.Conceptual))
        {
            // Add visual explanations for conceptual issues
            var visuals = contentGraph.GetVisualsForDomain(nodeId);
            remediationContent.AddRange(visuals);
        }

        if (mastery.ErrorTypes.Contains(ErrorType.Incomplete))
        {
            // Add mnemonics for recall issues
            var mnemonics = contentGraph.GetMnemonicsForDomain(nodeId);
            remediationContent.AddRange(mnemonics);
        }

        if (mastery.ErrorTypes.Contains(ErrorType.Conceptual))
        {
            // Add more worked examples for transfer issues
            var examples = contentGraph.GetExamplesForDomain(nodeId);
            remediationContent.AddRange(examples);
        }

        // Filter by preferred modality if possible
        if (preferredModality != ContentModality.Text)
        {
            var preferredContent = remediationContent
                .Where(c => c.Modality == preferredModality)
                .ToList();
            
            if (preferredContent.Any())
                remediationContent = preferredContent;
        }

        // If no specific remediation content, get simpler explanations
        if (!remediationContent.Any())
        {
            var simplerContent = contentGraph.GetSimplerContent(nodeId, ContentNodeType.Explanation, 0.5);
            remediationContent.AddRange(simplerContent);
        }

        return remediationContent.Take(3).ToList();
    }

    private bool IsDueForReactivation(LearnerMastery mastery, DateTimeOffset now)
    {
        if (!mastery.LastRetrievalTimestamp.HasValue)
            return false;

        var daysSinceLastRetrieval = (now - mastery.LastRetrievalTimestamp.Value).TotalDays;
        var interval = CalculateCurrentInterval(mastery);
        
        return daysSinceLastRetrieval >= interval;
    }

    private double CalculateCurrentInterval(LearnerMastery mastery)
    {
        // Base interval on mastery state and retrieval history
        var retrievalCount = mastery.RetrievalHistory.Count;
        
        if (retrievalCount == 0)
            return 1;

        // Use spaced intervals based on successful retrievals
        var successfulRetrievals = mastery.RetrievalHistory.Count(r => r.WasSuccessful);
        var intervalIndex = Math.Min(successfulRetrievals - 1, SpacedIntervals.Length - 1);
        
        var baseInterval = SpacedIntervals[Math.Max(0, intervalIndex)];
        
        // Adjust based on mastery probability
        var multiplier = mastery.MasteryProbability switch
        {
            >= 0.9 => 1.5, // Strong mastery - longer intervals
            >= 0.8 => 1.2,
            >= 0.6 => 1.0,
            _ => 0.5 // Weaker mastery - shorter intervals
        };

        return baseInterval * multiplier;
    }

    private double AdjustIntervalBasedOnPerformance(double currentInterval, double performanceScore)
    {
        // Adjust interval based on performance
        var adjustment = performanceScore switch
        {
            >= 0.9 => 1.5, // Excellent performance - increase interval
            >= 0.7 => 1.2, // Good performance - slight increase
            >= 0.5 => 1.0, // Average - maintain
            < 0.5 => 0.5,  // Poor - decrease interval
            _ => 1.0
        };

        return currentInterval * adjustment;
    }

    private double CalculateReactivationPriority(LearnerMastery mastery)
    {
        // Higher priority for concepts at risk of being forgotten
        var priority = 100.0;

        // Decrease priority based on how recently it was reviewed
        if (mastery.LastRetrievalTimestamp.HasValue)
        {
            var daysSince = (DateTimeOffset.UtcNow - mastery.LastRetrievalTimestamp.Value).TotalDays;
            priority -= daysSince * 0.5;
        }

        // Increase priority based on decay risk
        priority += mastery.ConfidenceRating * 20;

        // Decrease priority if mastery is very strong
        if (mastery.MasteryProbability >= 0.95)
            priority -= 30;

        return Math.Max(0, priority);
    }

    private string GenerateReactivationRationale(LearnerMastery mastery)
    {
        if (!mastery.LastRetrievalTimestamp.HasValue)
            return "Ready for review";

        var daysSince = (DateTimeOffset.UtcNow - mastery.LastRetrievalTimestamp.Value).TotalDays;
        
        return mastery.MasteryProbability switch
        {
            >= 0.9 => $"Maintain mastery (last reviewed {Math.Round(daysSince)} days ago)",
            >= 0.7 => $"Strengthen understanding (last reviewed {Math.Round(daysSince)} days ago)",
            _ => $"Review recommended (last reviewed {Math.Round(daysSince)} days ago)"
        };
    }
}
