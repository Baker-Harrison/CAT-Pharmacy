using CatAdaptive.Application.Abstractions;
using CatAdaptive.Application.Models;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Services;

/// <summary>
/// Orchestrates the end-to-end learning flow.
/// Refactored to delegate responsibilities to dedicated services.
/// </summary>
public sealed class LearningFlowService
{
    private readonly ContentIngestionService _ingestionService;
    private readonly AssessmentService _assessmentService;

    public LearningFlowService(
        ContentIngestionService ingestionService,
        AssessmentService assessmentService)
    {
        _ingestionService = ingestionService;
        _assessmentService = assessmentService;
    }

    public Task<LearningIngestionResult> IngestAsync(string filePath, CancellationToken ct = default)
    {
        return _ingestionService.IngestAsync(filePath, ct);
    }

    public Task<IReadOnlyList<LessonPlan>> GetLessonsAsync(CancellationToken ct = default)
    {
        return _assessmentService.GetLessonsAsync(ct);
    }

    public Task<LessonPlan?> GetLessonAsync(Guid lessonId, CancellationToken ct = default)
    {
        return _assessmentService.GetLessonAsync(lessonId, ct);
    }

    public Task<LessonPlan?> SubmitQuizAsync(
        Guid lessonId,
        IReadOnlyList<LessonQuizAnswer> answers,
        CancellationToken ct = default)
    {
        return _assessmentService.SubmitQuizAsync(lessonId, answers, ct);
    }
}