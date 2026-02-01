using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Aggregates;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Application.Services;

public sealed class AssessmentService
{
    private static readonly LearnerProfile DefaultLearner = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Default Learner",
        Array.Empty<string>());

    private readonly ILessonPlanRepository _lessonPlanRepository;
    private readonly ILessonQuizEvaluator _lessonQuizEvaluator;
    private readonly IKnowledgeGraphRepository _knowledgeGraphRepository;
    private readonly IContentGraphRepository _contentGraphRepository;
    private readonly ILessonPlanGenerator _lessonPlanGenerator;
    private readonly ILogger<AssessmentService> _logger;

    public AssessmentService(
        ILessonPlanRepository lessonPlanRepository,
        ILessonQuizEvaluator lessonQuizEvaluator,
        IKnowledgeGraphRepository knowledgeGraphRepository,
        IContentGraphRepository contentGraphRepository,
        ILessonPlanGenerator lessonPlanGenerator,
        ILogger<AssessmentService> logger)
    {
        _lessonPlanRepository = lessonPlanRepository;
        _lessonQuizEvaluator = lessonQuizEvaluator;
        _knowledgeGraphRepository = knowledgeGraphRepository;
        _contentGraphRepository = contentGraphRepository;
        _lessonPlanGenerator = lessonPlanGenerator;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LessonPlan>> GetLessonsAsync(CancellationToken ct = default)
    {
        var lessons = await _lessonPlanRepository.GetAllAsync(ct);
        return lessons
            .OrderBy(l => l.CreatedAt)
            .ToList();
    }

    public Task<LessonPlan?> GetLessonAsync(Guid lessonId, CancellationToken ct = default)
    {
        return _lessonPlanRepository.GetByIdAsync(lessonId, ct);
    }

    public async Task<LessonPlan?> SubmitQuizAsync(
        Guid lessonId,
        IReadOnlyList<LessonQuizAnswer> answers,
        CancellationToken ct = default)
    {
        var lesson = await _lessonPlanRepository.GetByIdAsync(lessonId, ct);
        if (lesson == null)
        {
            _logger.LogWarning("Quiz submission failed: Lesson {LessonId} not found.", lessonId);
            return null;
        }

        _logger.LogInformation("Evaluating quiz for lesson {LessonId}...", lessonId);
        var quizResult = await _lessonQuizEvaluator.EvaluateAsync(lesson.Quiz, answers, ct);
        var updatedLesson = lesson.WithQuizResult(quizResult);

        await _lessonPlanRepository.UpdateAsync(updatedLesson, ct);
        await _lessonPlanRepository.SaveChangesAsync(ct);

        var contentGraph = await _contentGraphRepository.GetAsync(ct);
        var knowledgeGraph = await _knowledgeGraphRepository.GetByLearnerAsync(DefaultLearner.Id, ct);
        if (knowledgeGraph != null)
        {
            if (contentGraph != null)
            {
                knowledgeGraph.InitializeConcepts(contentGraph.GetConcepts().Select(c => c.Id));
            }

            knowledgeGraph.ApplyDecay(TimeSpan.FromDays(14), DateTimeOffset.UtcNow);
            UpdateKnowledgeGraphFromQuiz(knowledgeGraph, lesson, quizResult, answers);
            await _knowledgeGraphRepository.SaveAsync(knowledgeGraph, ct);
        }

        if (contentGraph != null)
        {
             var existingLessons = await _lessonPlanRepository.GetAllAsync(ct);
             var kgForGen = knowledgeGraph ?? new KnowledgeGraph(DefaultLearner.Id);
             var existingConceptIds = BuildExistingConceptIdsForGeneration(existingLessons, kgForGen);

             if (quizResult.ScorePercent < 80)
             {
                 _logger.LogInformation("Score {Score}% < 80%. Generating remediation...", quizResult.ScorePercent);
                 var remediationLesson = await _lessonPlanGenerator.GenerateRemediationLessonAsync(
                     contentGraph,
                     kgForGen,
                     lesson.ConceptId,
                     ct);

                 if (remediationLesson != null)
                 {
                     await _lessonPlanRepository.AddRangeAsync(new[] { remediationLesson }, ct);
                     await _lessonPlanRepository.SaveChangesAsync(ct);
                     _logger.LogInformation("Remediation lesson generated.");
                 }
                 else
                 {
                     _logger.LogWarning("Remediation generation failed or returned null.");
                 }
             }
             else
             {
                 _logger.LogInformation("Score {Score}% >= 80%. Generating next lessons...", quizResult.ScorePercent);
                 var nextLessons = await _lessonPlanGenerator.GenerateNextLessonsAsync(
                     contentGraph,
                     kgForGen,
                     existingConceptIds,
                     ct);

                 if (nextLessons.Count > 0)
                 {
                     await _lessonPlanRepository.AddRangeAsync(nextLessons, ct);
                     await _lessonPlanRepository.SaveChangesAsync(ct);
                     _logger.LogInformation("Generated {Count} new lessons.", nextLessons.Count);
                 }
             }
        }

        return updatedLesson;
    }

    private static IReadOnlyList<Guid> BuildExistingConceptIdsForGeneration(
        IReadOnlyList<LessonPlan> existingLessons,
        KnowledgeGraph knowledgeGraph)
    {
        var reviewConcepts = knowledgeGraph.GetAtRiskConcepts(0.6)
            .Select(m => m.ConceptId)
            .Concat(knowledgeGraph.GetConceptsByState(MasteryState.Fragile).Select(m => m.ConceptId))
            .ToHashSet();

        return existingLessons
            .Select(l => l.ConceptId)
            .Where(id => !reviewConcepts.Contains(id))
            .Distinct()
            .ToList();
    }

    private static void UpdateKnowledgeGraphFromQuiz(
        KnowledgeGraph knowledgeGraph,
        LessonPlan lesson,
        LessonQuizResult quizResult,
        IReadOnlyList<LessonQuizAnswer> answers)
    {
        var questionLookup = lesson.Quiz.Questions.ToDictionary(q => q.Id, q => q);
        var answerLookup = answers.ToDictionary(a => a.QuestionId, a => a.ResponseText);

        foreach (var result in quizResult.QuestionResults)
        {
            if (!questionLookup.TryGetValue(result.QuestionId, out var question))
            {
                continue;
            }

            var responseText = answerLookup.TryGetValue(result.QuestionId, out var response)
                ? response
                : string.Empty;

            var promptFormat = question.Type == LessonQuizQuestionType.OpenResponse
                ? PromptFormat.ExplainWhy
                : PromptFormat.ShortAnswer;

            var evidence = EvidenceRecord.Create(
                DefaultLearner.Id,
                question.Id,
                new[] { question.ConceptId },
                responseText,
                result.IsCorrect,
                result.Score,
                latencyMs: 0,
                promptFormat: promptFormat,
                errorType: result.IsCorrect ? ErrorType.None : ErrorType.Conceptual);

            knowledgeGraph.UpdateFromEvidence(evidence);
        }
    }
}
