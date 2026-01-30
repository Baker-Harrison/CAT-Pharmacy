using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Application.Services;

/// <summary>
/// Service for generating and evaluating diagnostic pre-quizzes.
/// </summary>
public sealed class DiagnosticQuizService
{
    private readonly IEnhancedContentGraphRepository _contentGraphRepository;
    private readonly IDomainKnowledgeGraphRepository _domainGraphRepository;
    private readonly ILessonQuizEvaluator _quizEvaluator;
    private readonly ILogger<DiagnosticQuizService> _logger;

    public DiagnosticQuizService(
        IEnhancedContentGraphRepository contentGraphRepository,
        IDomainKnowledgeGraphRepository domainGraphRepository,
        ILessonQuizEvaluator quizEvaluator,
        ILogger<DiagnosticQuizService> logger)
    {
        _contentGraphRepository = contentGraphRepository;
        _domainGraphRepository = domainGraphRepository;
        _quizEvaluator = quizEvaluator;
        _logger = logger;
    }

    /// <summary>
    /// Generates a diagnostic pre-quiz for a target node.
    /// </summary>
    public async Task<DiagnosticPreQuiz?> GenerateDiagnosticQuizAsync(
        Guid targetNodeId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Generating diagnostic quiz for target node {NodeId}", targetNodeId);

        var domainGraph = await _domainGraphRepository.GetAsync(ct);
        if (domainGraph == null)
        {
            _logger.LogWarning("Domain Knowledge Graph not found");
            return null;
        }

        var contentGraph = await _contentGraphRepository.GetAsync(ct);
        if (contentGraph == null)
        {
            _logger.LogWarning("Content Graph not found");
            return null;
        }

        var targetNode = domainGraph.GetNode(targetNodeId);
        if (targetNode == null)
        {
            _logger.LogWarning("Target node {NodeId} not found in domain graph", targetNodeId);
            return null;
        }

        // Get related nodes for comprehensive assessment
        var relatedNodes = GetRelatedNodesForAssessment(targetNodeId, domainGraph);
        var allNodeIds = relatedNodes.Select(n => n.Id).Append(targetNodeId).ToList();

        // Get questions for all related nodes
        var questions = new List<DiagnosticQuestion>();
        
        // Add questions for target node at different Bloom's levels
        var targetQuestions = await GenerateQuestionsForNode(
            targetNode, 
            contentGraph, 
            new[] { BloomsLevel.Remember, BloomsLevel.Understand, BloomsLevel.Apply },
            3,
            ct);
        questions.AddRange(targetQuestions);

        // Add questions for prerequisites (to check foundation)
        var prerequisites = domainGraph.GetPrerequisites(targetNodeId);
        foreach (var prereq in prerequisites.Take(2)) // Limit to 2 prerequisites
        {
            var prereqQuestions = await GenerateQuestionsForNode(
                prereq,
                contentGraph,
                new[] { BloomsLevel.Remember, BloomsLevel.Understand },
                1,
                ct);
            questions.AddRange(prereqQuestions);
        }

        if (questions.Count == 0)
        {
            _logger.LogWarning("No questions generated for target node {NodeId}", targetNodeId);
            return null;
        }

        var quiz = new DiagnosticPreQuiz(
            Guid.NewGuid(),
            targetNodeId,
            questions,
            DateTimeOffset.UtcNow);

        _logger.LogInformation("Generated diagnostic quiz with {QuestionCount} questions for target node {NodeId}", 
            questions.Count, targetNodeId);

        return quiz;
    }

    /// <summary>
    /// Evaluates a diagnostic quiz and creates results.
    /// </summary>
    public async Task<DiagnosticQuizResult> EvaluateDiagnosticQuizAsync(
        DiagnosticPreQuiz quiz,
        IReadOnlyList<LessonQuizAnswer> answers,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Evaluating diagnostic quiz {QuizId}", quiz.Id);

        // Convert to LessonQuiz for evaluation
        var lessonQuestions = quiz.Questions.Select(q => new LessonQuizQuestion(
            q.Id,
            q.DomainNodeId,
            q.Format == PromptFormat.ShortAnswer ? LessonQuizQuestionType.FillInBlank : LessonQuizQuestionType.OpenResponse,
            q.Prompt,
            q.ExpectedAnswer,
            new EvaluationRubric { RequiredPoints = new List<string> { "Accuracy", "Completeness" } })).ToList();

        var lessonQuiz = new LessonQuiz(lessonQuestions);

        // Evaluate using existing evaluator
        var lessonResult = await _quizEvaluator.EvaluateAsync(lessonQuiz, answers, ct);

        // Convert back to diagnostic results with error type analysis
        var diagnosticResults = new List<DiagnosticQuestionResult>();
        
        foreach (var lessonQr in lessonResult.QuestionResults)
        {
            var question = quiz.Questions.First(q => q.Id == lessonQr.QuestionId);
            var answer = answers.First(a => a.QuestionId == lessonQr.QuestionId);
            
            var errorType = DetermineErrorType(lessonQr.IsCorrect, answer.ResponseText, question);
            var confidence = ExtractConfidence(answer.ResponseText);
            
            diagnosticResults.Add(new DiagnosticQuestionResult(
                lessonQr.QuestionId,
                lessonQr.IsCorrect,
                lessonQr.Score,
                lessonQr.Feedback,
                errorType,
                confidence));
        }

        var result = new DiagnosticQuizResult(
            quiz.Id,
            lessonResult.ScorePercent,
            diagnosticResults,
            DateTimeOffset.UtcNow);

        _logger.LogInformation("Evaluated diagnostic quiz {QuizId} with score {Score}%", 
            quiz.Id, result.ScorePercent);

        return result;
    }

    private async Task<IReadOnlyList<DiagnosticQuestion>> GenerateQuestionsForNode(
        DomainNode node,
        EnhancedContentGraph contentGraph,
        BloomsLevel[] bloomsLevels,
        int count,
        CancellationToken ct)
    {
        var questions = new List<DiagnosticQuestion>();

        foreach (var bloomsLevel in bloomsLevels)
        {
            var contentQuestions = contentGraph.GetQuestionsForDomain(node.Id, bloomsLevel);
            
            foreach (var content in contentQuestions.Take(count / bloomsLevels.Length + 1))
            {
                var format = bloomsLevel switch
                {
                    BloomsLevel.Remember => PromptFormat.ShortAnswer,
                    BloomsLevel.Understand => PromptFormat.ExplainWhy,
                    BloomsLevel.Apply => PromptFormat.Application,
                    BloomsLevel.Analyze => PromptFormat.Application,
                    _ => PromptFormat.ExplainWhy
                };

                questions.Add(new DiagnosticQuestion(
                    Guid.NewGuid(),
                    node.Id,
                    content.Content,
                    format,
                    bloomsLevel,
                    content.Content)); // Simplified - would extract actual expected answer
            }
        }

        return questions.Take(count).ToList();
    }

    private IReadOnlyList<DomainNode> GetRelatedNodesForAssessment(Guid nodeId, DomainKnowledgeGraph domainGraph)
    {
        var related = new List<DomainNode>();
        
        // Get prerequisites
        related.AddRange(domainGraph.GetPrerequisites(nodeId));
        
        // Get related concepts
        related.AddRange(domainGraph.GetRelatedNodes(nodeId));
        
        // Get contrasting concepts
        related.AddRange(domainGraph.GetContrastingNodes(nodeId));
        
        return related.Distinct().ToList();
    }

    private ErrorType DetermineErrorType(bool isCorrect, string response, DiagnosticQuestion question)
    {
        if (isCorrect)
            return ErrorType.None;

        // Simple heuristic for error type detection
        if (string.IsNullOrWhiteSpace(response))
            return ErrorType.Incomplete;

        if (question.BloomsLevel >= BloomsLevel.Apply)
            return ErrorType.Conceptual; // Using Conceptual for transfer failures

        return ErrorType.Conceptual; // Using Conceptual for misunderstandings
    }

    private double ExtractConfidence(string response)
    {
        // Simple confidence extraction based on response length and certainty words
        if (string.IsNullOrWhiteSpace(response))
            return 0.0;

        var lower = response.ToLower();
        var certaintyWords = new[] { "i think", "maybe", "probably", "not sure", "i guess" };
        var hasUncertainty = certaintyWords.Any(word => lower.Contains(word));

        if (hasUncertainty)
            return 0.3;
        else if (response.Length > 100)
            return 0.8;
        else
            return 0.6;
    }
}
