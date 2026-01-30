using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Application.Services;

/// <summary>
/// Generates adaptive lessons based on diagnostic quiz results and target nodes.
/// </summary>
public sealed class AdaptiveLessonGenerator
{
    private readonly IEnhancedContentGraphRepository _contentGraphRepository;
    private readonly IDomainKnowledgeGraphRepository _domainGraphRepository;
    private readonly ILogger<AdaptiveLessonGenerator> _logger;

    public AdaptiveLessonGenerator(
        IEnhancedContentGraphRepository contentGraphRepository,
        IDomainKnowledgeGraphRepository domainGraphRepository,
        ILogger<AdaptiveLessonGenerator> logger)
    {
        _contentGraphRepository = contentGraphRepository;
        _domainGraphRepository = domainGraphRepository;
        _logger = logger;
    }

    /// <summary>
    /// Generates an adaptive lesson for a target node based on diagnostic results.
    /// </summary>
    public async Task<AdaptiveLesson?> GenerateLessonAsync(
        Guid targetNodeId,
        DiagnosticQuizResult diagnosticResult,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Generating adaptive lesson for target node {NodeId}", targetNodeId);

        var contentGraph = await _contentGraphRepository.GetAsync(ct);
        if (contentGraph == null)
        {
            _logger.LogWarning("Content Graph not found");
            return null;
        }

        var domainGraph = await _domainGraphRepository.GetAsync(ct);
        if (domainGraph == null)
        {
            _logger.LogWarning("Domain Knowledge Graph not found");
            return null;
        }

        var targetNode = domainGraph.GetNode(targetNodeId);
        if (targetNode == null)
        {
            _logger.LogWarning("Target node {NodeId} not found", targetNodeId);
            return null;
        }

        // Analyze diagnostic results to determine lesson focus
        var lessonFocus = AnalyzeLessonFocus(diagnosticResult);
        
        // Generate lesson components
        var components = new List<LessonComponent>();
        int orderIndex = 0;

        // 1. Prediction Prompt (activate prior knowledge)
        var predictionPrompt = GeneratePredictionPrompt(targetNode, contentGraph, orderIndex++);
        if (predictionPrompt != null)
            components.Add(predictionPrompt);

        // 2. Focused Explanation (address specific gaps)
        var explanation = GenerateFocusedExplanation(targetNode, contentGraph, lessonFocus, orderIndex++);
        if (explanation != null)
            components.Add(explanation);

        // 3. Worked Example (demonstrate application)
        var workedExample = GenerateWorkedExample(targetNode, contentGraph, lessonFocus, orderIndex++);
        if (workedExample != null)
            components.Add(workedExample);

        // 4. Active Generation Task (practice with feedback)
        var activeTask = GenerateActiveGenerationTask(targetNode, contentGraph, lessonFocus, orderIndex++);
        if (activeTask != null)
            components.Add(activeTask);

        // 5. Visual Aid (if needed for conceptual understanding)
        if (lessonFocus.NeedsVisualSupport)
        {
            var visual = GenerateVisualAid(targetNode, contentGraph, orderIndex++);
            if (visual != null)
                components.Add(visual);
        }

        // 6. Mnemonic Device (if struggling with recall)
        if (lessonFocus.NeedsMnemonicSupport)
        {
            var mnemonic = GenerateMnemonicDevice(targetNode, contentGraph, orderIndex++);
            if (mnemonic != null)
                components.Add(mnemonic);
        }

        if (components.Count == 0)
        {
            _logger.LogWarning("No components generated for target node {NodeId}", targetNodeId);
            return null;
        }

        var lesson = new AdaptiveLesson(
            Guid.NewGuid(),
            targetNodeId,
            $"Adaptive Lesson: {targetNode.Title}",
            GenerateLessonSummary(targetNode, lessonFocus),
            components,
            components.Sum(c => 5), // Estimate 5 minutes per component
            DateTimeOffset.UtcNow);

        _logger.LogInformation("Generated adaptive lesson {LessonId} with {ComponentCount} components", 
            lesson.Id, components.Count);

        return lesson;
    }

    private LessonFocus AnalyzeLessonFocus(DiagnosticQuizResult diagnosticResult)
    {
        var focus = new LessonFocus();

        // Analyze error types
        var errorTypes = diagnosticResult.QuestionResults
            .Where(qr => !qr.IsCorrect)
            .GroupBy(qr => qr.ErrorType)
            .ToDictionary(g => g.Key, g => g.Count());

        if (errorTypes.ContainsKey(ErrorType.Incomplete))
        {
            focus.NeedsRecallSupport = true;
            focus.NeedsMnemonicSupport = true;
        }

        if (errorTypes.ContainsKey(ErrorType.Conceptual))
        {
            focus.NeedsConceptualClarification = true;
            focus.NeedsVisualSupport = true;
        }

        if (errorTypes.ContainsKey(ErrorType.Conceptual))
        {
            focus.NeedsApplicationPractice = true;
        }

        // Analyze confidence levels
        var avgConfidence = diagnosticResult.QuestionResults
            .Average(qr => qr.Confidence);

        if (avgConfidence < 0.5)
        {
            focus.NeedsConfidenceBuilding = true;
        }

        // Analyze Bloom's level performance
        var performanceByLevel = diagnosticResult.QuestionResults
            .GroupBy(qr => GetQuestionBloomsLevel(qr.QuestionId))
            .ToDictionary(g => g.Key, g => g.Average(qr => qr.Score));

        if (performanceByLevel.ContainsKey(BloomsLevel.Apply) && 
            performanceByLevel[BloomsLevel.Apply] < 0.6)
        {
            focus.NeedsApplicationPractice = true;
        }

        return focus;
    }

    private LessonComponent? GeneratePredictionPrompt(
        DomainNode targetNode,
        EnhancedContentGraph contentGraph,
        int orderIndex)
    {
        var prompt = $"Before we dive into {targetNode.Title}, take a moment to predict: " +
                    $"What do you already know about this topic? What questions do you have?";

        return new LessonComponent(
            Guid.NewGuid(),
            LessonComponentType.PredictionPrompt,
            "Activate Prior Knowledge",
            prompt,
            ContentModality.Text,
            orderIndex);
    }

    private LessonComponent? GenerateFocusedExplanation(
        DomainNode targetNode,
        EnhancedContentGraph contentGraph,
        LessonFocus focus,
        int orderIndex)
    {
        var explanations = contentGraph.GetExplanationsForDomain(targetNode.Id);
        
        // Select explanation based on focus
        var selectedExplanation = explanations.FirstOrDefault();
        if (selectedExplanation == null)
        {
            // Generate default explanation
            selectedExplanation = new EnhancedContentNode(
                Guid.NewGuid(),
                ContentNodeType.Explanation,
                $"Understanding {targetNode.Title}",
                targetNode.Description,
                ContentModality.Text,
                BloomsLevel.Understand,
                targetNode.Difficulty,
                5,
                new[] { targetNode.Id },
                new[] { "explanation" });
        }

        return new LessonComponent(
            Guid.NewGuid(),
            LessonComponentType.FocusedExplanation,
            "Key Concepts",
            selectedExplanation.Content,
            selectedExplanation.Modality,
            orderIndex);
    }

    private LessonComponent? GenerateWorkedExample(
        DomainNode targetNode,
        EnhancedContentGraph contentGraph,
        LessonFocus focus,
        int orderIndex)
    {
        var examples = contentGraph.GetExamplesForDomain(targetNode.Id);
        
        // Prefer worked problems if application practice is needed
        var selectedExample = focus.NeedsApplicationPractice
            ? examples.FirstOrDefault(e => e.Type == ContentNodeType.WorkedExample)
            : examples.FirstOrDefault();

        if (selectedExample == null)
        {
            // Generate default example
            selectedExample = new EnhancedContentNode(
                Guid.NewGuid(),
                ContentNodeType.WorkedExample,
                $"Example: {targetNode.Title}",
                $"Here's how {targetNode.Title} applies in practice...",
                ContentModality.Text,
                BloomsLevel.Apply,
                targetNode.Difficulty,
                5,
                new[] { targetNode.Id },
                new[] { "example" });
        }

        return new LessonComponent(
            Guid.NewGuid(),
            LessonComponentType.WorkedExample,
            "Worked Example",
            selectedExample.Content,
            selectedExample.Modality,
            orderIndex);
    }

    private LessonComponent? GenerateActiveGenerationTask(
        DomainNode targetNode,
        EnhancedContentGraph contentGraph,
        LessonFocus focus,
        int orderIndex)
    {
        var task = $"Now it's your turn! Create your own example of {targetNode.Title}. " +
                  $"Explain it in your own words as if teaching to a peer.";

        return new LessonComponent(
            Guid.NewGuid(),
            LessonComponentType.ActiveGenerationTask,
            "Practice & Apply",
            task,
            ContentModality.Text,
            orderIndex);
    }

    private LessonComponent? GenerateVisualAid(
        DomainNode targetNode,
        EnhancedContentGraph contentGraph,
        int orderIndex)
    {
        var visuals = contentGraph.GetVisualsForDomain(targetNode.Id);
        var selectedVisual = visuals.FirstOrDefault();

        if (selectedVisual == null)
        {
            // Generate placeholder for visual
            selectedVisual = new EnhancedContentNode(
                Guid.NewGuid(),
                ContentNodeType.Visual,
                $"Visual: {targetNode.Title}",
                $"[Visual representation of {targetNode.Title} would appear here]",
                ContentModality.Visual,
                BloomsLevel.Understand,
                targetNode.Difficulty,
                2,
                new[] { targetNode.Id },
                new[] { "visual" });
        }

        return new LessonComponent(
            Guid.NewGuid(),
            LessonComponentType.VisualAid,
            "Visual Aid",
            selectedVisual.Content,
            selectedVisual.Modality,
            orderIndex);
    }

    private LessonComponent? GenerateMnemonicDevice(
        DomainNode targetNode,
        EnhancedContentGraph contentGraph,
        int orderIndex)
    {
        var mnemonics = contentGraph.GetMnemonicsForDomain(targetNode.Id);
        var selectedMnemonic = mnemonics.FirstOrDefault();

        if (selectedMnemonic == null)
        {
            // Generate simple mnemonic
            selectedMnemonic = new EnhancedContentNode(
                Guid.NewGuid(),
                ContentNodeType.Mnemonic,
                $"Mnemonic for {targetNode.Title}",
                $"Create a memorable phrase or acronym to help remember {targetNode.Title}",
                ContentModality.Text,
                BloomsLevel.Remember,
                targetNode.Difficulty,
                2,
                new[] { targetNode.Id },
                new[] { "mnemonic" });
        }

        return new LessonComponent(
            Guid.NewGuid(),
            LessonComponentType.MnemonicDevice,
            "Memory Aid",
            selectedMnemonic.Content,
            selectedMnemonic.Modality,
            orderIndex);
    }

    private string GenerateLessonSummary(DomainNode targetNode, LessonFocus focus)
    {
        var summary = $"This adaptive lesson focuses on {targetNode.Title}. ";
        
        if (focus.NeedsConceptualClarification)
            summary += "It emphasizes conceptual understanding. ";
        
        if (focus.NeedsApplicationPractice)
            summary += "It includes practical application examples. ";
        
        if (focus.NeedsRecallSupport)
            summary += "It provides memory aids and recall strategies. ";

        return summary.Trim();
    }

    private BloomsLevel GetQuestionBloomsLevel(Guid questionId)
    {
        // Would need to track this in the question data
        // For now, return a default
        return BloomsLevel.Understand;
    }

    private class LessonFocus
    {
        public bool NeedsRecallSupport { get; set; }
        public bool NeedsConceptualClarification { get; set; }
        public bool NeedsApplicationPractice { get; set; }
        public bool NeedsVisualSupport { get; set; }
        public bool NeedsMnemonicSupport { get; set; }
        public bool NeedsConfidenceBuilding { get; set; }
    }
}
