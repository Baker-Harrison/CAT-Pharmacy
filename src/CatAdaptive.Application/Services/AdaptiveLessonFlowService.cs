using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Application.Services;

/// <summary>
/// Orchestrates the adaptive lesson flow: Pre-Quiz → Lesson → Post-Quiz → Model Update → Next Lesson.
/// </summary>
public sealed class AdaptiveLessonFlowService
{
    private readonly IAdaptiveSessionRepository _sessionRepository;
    private readonly ILearnerModelRepository _learnerModelRepository;
    private readonly IDomainKnowledgeGraphRepository _domainGraphRepository;
    private readonly IEnhancedContentGraphRepository _contentGraphRepository;
    private readonly TargetSelectionService _targetSelectionService;
    private readonly DiagnosticQuizService _diagnosticQuizService;
    private readonly AdaptiveLessonGenerator _lessonGenerator;
    private readonly ILessonQuizEvaluator _quizEvaluator;
    private readonly ILogger<AdaptiveLessonFlowService> _logger;

    public AdaptiveLessonFlowService(
        IAdaptiveSessionRepository sessionRepository,
        ILearnerModelRepository learnerModelRepository,
        IDomainKnowledgeGraphRepository domainGraphRepository,
        IEnhancedContentGraphRepository contentGraphRepository,
        TargetSelectionService targetSelectionService,
        DiagnosticQuizService diagnosticQuizService,
        AdaptiveLessonGenerator lessonGenerator,
        ILessonQuizEvaluator quizEvaluator,
        ILogger<AdaptiveLessonFlowService> logger)
    {
        _sessionRepository = sessionRepository;
        _learnerModelRepository = learnerModelRepository;
        _domainGraphRepository = domainGraphRepository;
        _contentGraphRepository = contentGraphRepository;
        _targetSelectionService = targetSelectionService;
        _diagnosticQuizService = diagnosticQuizService;
        _lessonGenerator = lessonGenerator;
        _quizEvaluator = quizEvaluator;
        _logger = logger;
    }

    /// <summary>
    /// Starts a new adaptive lesson session.
    /// </summary>
    public async Task<AdaptiveSession> StartNewSessionAsync(Guid learnerId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting new adaptive session for learner {LearnerId}", learnerId);

        // Check for existing active session
        var activeSessions = await _sessionRepository.GetActiveSessionsAsync(learnerId, ct);
        if (activeSessions.Any())
        {
            _logger.LogInformation("Resuming existing session {SessionId}", activeSessions.First().Id);
            return activeSessions.First();
        }

        // Create new session
        var session = await _sessionRepository.CreateAsync(learnerId, ct);

        // Select target node
        var targetNode = await _targetSelectionService.SelectNextTargetAsync(learnerId, ct);
        if (targetNode == null)
        {
            _logger.LogWarning("No target node available for learner {LearnerId}", learnerId);
            return session;
        }

        // Start session with target
        session.StartWithTarget(targetNode);
        await _sessionRepository.SaveAsync(session, ct);

        // Generate diagnostic pre-quiz
        var preQuiz = await _diagnosticQuizService.GenerateDiagnosticQuizAsync(targetNode.DomainNodeId, ct);
        if (preQuiz != null)
        {
            session.SetPreQuiz(preQuiz);
            await _sessionRepository.SaveAsync(session, ct);
        }

        _logger.LogInformation("Started adaptive session {SessionId} with target {TargetNodeId}", 
            session.Id, targetNode.DomainNodeId);

        return session;
    }

    /// <summary>
    /// Submits the diagnostic pre-quiz and generates the lesson.
    /// </summary>
    public async Task<AdaptiveSession> SubmitPreQuizAsync(
        Guid sessionId,
        IReadOnlyList<LessonQuizAnswer> answers,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Submitting pre-quiz for session {SessionId}", sessionId);

        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session == null || session.PreQuiz == null)
        {
            throw new InvalidOperationException("Session or pre-quiz not found");
        }

        // Evaluate pre-quiz
        var preQuizResult = await _diagnosticQuizService.EvaluateDiagnosticQuizAsync(
            session.PreQuiz, answers, ct);

        // Update learner model based on pre-quiz results
        await UpdateLearnerModelFromQuizAsync(session.LearnerId, preQuizResult, session.PreQuiz, ct);

        session.CompletePreQuiz();
        await _sessionRepository.SaveAsync(session, ct);

        // Generate adaptive lesson based on pre-quiz performance
        var lesson = await _lessonGenerator.GenerateLessonAsync(
            session.TargetNode!.DomainNodeId,
            preQuizResult,
            ct);

        if (lesson != null)
        {
            session.SetLesson(lesson);
            await _sessionRepository.SaveAsync(session, ct);
        }

        _logger.LogInformation("Processed pre-quiz for session {SessionId}, generated lesson {LessonId}", 
            sessionId, lesson?.Id);

        return session;
    }

    /// <summary>
    /// Completes the lesson and generates the post-quiz.
    /// </summary>
    public async Task<AdaptiveSession> CompleteLessonAsync(Guid sessionId, CancellationToken ct = default)
    {
        _logger.LogInformation("Completing lesson for session {SessionId}", sessionId);

        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session == null)
        {
            throw new InvalidOperationException("Session not found");
        }

        session.CompleteLesson();
        await _sessionRepository.SaveAsync(session, ct);

        // Generate post-quiz (parallel to pre-quiz but not identical)
        var postQuiz = await _diagnosticQuizService.GenerateDiagnosticQuizAsync(
            session.TargetNode!.DomainNodeId, ct);

        if (postQuiz != null)
        {
            // Store post-quiz in the lesson for later submission
            // This would need to be added to the AdaptiveLesson model
        }

        _logger.LogInformation("Completed lesson for session {SessionId}", sessionId);
        return session;
    }

    /// <summary>
    /// Submits the post-quiz and completes the session.
    /// </summary>
    public async Task<AdaptiveSession> SubmitPostQuizAsync(
        Guid sessionId,
        IReadOnlyList<LessonQuizAnswer> answers,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Submitting post-quiz for session {SessionId}", sessionId);

        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session == null || session.PreQuiz == null)
        {
            throw new InvalidOperationException("Session or pre-quiz not found");
        }

        // Evaluate post-quiz
        var postQuizResult = await _diagnosticQuizService.EvaluateDiagnosticQuizAsync(
            session.PreQuiz, answers, ct);

        // Update learner model
        await UpdateLearnerModelFromQuizAsync(session.LearnerId, postQuizResult, session.PreQuiz, ct);

        // Calculate learning gain
        var learningGain = CalculateLearningGain(session.PreQuiz, postQuizResult);

        // Generate session result
        var result = await GenerateSessionResultAsync(
            session,
            postQuizResult,
            learningGain,
            ct);

        session.CompleteWithResult(result);
        await _sessionRepository.SaveAsync(session, ct);

        _logger.LogInformation("Completed session {SessionId} with learning gain {Gain}%", 
            sessionId, learningGain);

        return session;
    }

    /// <summary>
    /// Gets the next recommended targets for a learner.
    /// </summary>
    public async Task<IReadOnlyList<TargetNode>> GetNextTargetsAsync(Guid learnerId, CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetMostRecentAsync(learnerId, ct);
        if (session?.RecommendedNextNodes == null || session.RecommendedNextNodes.Count == 0)
        {
            return await _targetSelectionService.SelectMultipleTargetsAsync(learnerId, 5, ct);
        }

        // Convert recommended node IDs to TargetNodes
        var domainGraph = await _domainGraphRepository.GetAsync(ct);
        if (domainGraph == null)
            return Array.Empty<TargetNode>();

        var learnerModel = await _learnerModelRepository.GetByLearnerAsync(learnerId, ct);
        if (learnerModel == null)
            return Array.Empty<TargetNode>();

        return session.RecommendedNextNodes
            .Select(nodeId => domainGraph.GetNode(nodeId))
            .Where(n => n != null)
            .Cast<DomainNode>()
            .Select(n => new TargetNode(
                n.Id,
                n.Title,
                0, // Would calculate actual score
                "Recommended based on previous session"))
            .ToList();
    }

    private async Task UpdateLearnerModelFromQuizAsync(
        Guid learnerId,
        DiagnosticQuizResult quizResult,
        DiagnosticPreQuiz quiz,
        CancellationToken ct)
    {
        foreach (var qr in quizResult.QuestionResults)
        {
            var question = quiz.Questions.First(q => q.Id == qr.QuestionId);
            
            var retrievalEvent = new RetrievalEvent(
                quizResult.CompletedAt,
                qr.IsCorrect,
                qr.ErrorType,
                qr.Confidence,
                0, // Would track actual latency
                question.Format);

            await _learnerModelRepository.UpdateMasteryAsync(
                learnerId,
                question.DomainNodeId,
                retrievalEvent,
                ct);
        }
    }

    private double CalculateLearningGain(DiagnosticPreQuiz? preQuiz, DiagnosticQuizResult postQuizResult)
    {
        // Simplified: would compare pre-quiz and post-quiz scores
        // For now, return the post-quiz score as the gain
        return postQuizResult.ScorePercent;
    }

    private async Task<AdaptiveSessionResult> GenerateSessionResultAsync(
        AdaptiveSession session,
        DiagnosticQuizResult postQuizResult,
        double learningGain,
        CancellationToken ct)
    {
        var strengths = new List<string>();
        var weaknesses = new List<string>();

        // Analyze results to identify strengths and weaknesses
        var correctByNode = postQuizResult.QuestionResults
            .Where(qr => qr.IsCorrect)
            .GroupBy(qr => GetQuestionNodeId(qr.QuestionId, session.PreQuiz!))
            .ToList();

        var incorrectByNode = postQuizResult.QuestionResults
            .Where(qr => !qr.IsCorrect)
            .GroupBy(qr => GetQuestionNodeId(qr.QuestionId, session.PreQuiz!))
            .ToList();

        var domainGraph = await _domainGraphRepository.GetAsync(ct);
        if (domainGraph != null)
        {
            foreach (var group in correctByNode)
            {
                var node = domainGraph.GetNode(group.Key);
                if (node != null)
                    strengths.Add(node.Title);
            }

            foreach (var group in incorrectByNode)
            {
                var node = domainGraph.GetNode(group.Key);
                if (node != null)
                    weaknesses.Add(node.Title);
            }
        }

        // Get next recommended nodes
        var nextTargets = await _targetSelectionService.SelectMultipleTargetsAsync(
            session.LearnerId, 3, ct);
        var recommendedNodeIds = nextTargets.Select(t => t.DomainNodeId).ToList();

        return new AdaptiveSessionResult(
            session.Id,
            session.TargetNode!.DomainNodeId,
            null, // Would include pre-quiz result
            new LessonQuizResult(
                postQuizResult.CompletedAt,
                postQuizResult.ScorePercent,
                postQuizResult.QuestionResults.Select(qr => new LessonQuizQuestionResult(
                    qr.QuestionId,
                    qr.Score,
                    qr.IsCorrect,
                    qr.Feedback)).ToList()),
            learningGain,
            strengths,
            weaknesses,
            recommendedNodeIds,
            DateTimeOffset.UtcNow);
    }

    private Guid GetQuestionNodeId(Guid questionId, DiagnosticPreQuiz quiz)
    {
        return quiz.Questions.First(q => q.Id == questionId).DomainNodeId;
    }
}
