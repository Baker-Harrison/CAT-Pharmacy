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
    private readonly IContentGraphRepository _contentGraphRepository;
    private readonly ILessonPlanGenerator _lessonPlanGenerator;
    private readonly ILessonPlanRepository _lessonPlanRepository;

    public LearningFlowService(
        ContentIngestionService ingestionService,
        AssessmentService assessmentService,
        IContentGraphRepository contentGraphRepository,
        ILessonPlanGenerator lessonPlanGenerator,
        ILessonPlanRepository lessonPlanRepository)
    {
        _ingestionService = ingestionService;
        _assessmentService = assessmentService;
        _contentGraphRepository = contentGraphRepository;
        _lessonPlanGenerator = lessonPlanGenerator;
        _lessonPlanRepository = lessonPlanRepository;
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

    /// <summary>
    /// Generates lessons based on the current content graph.
    /// </summary>
    public async Task<int> GenerateLessonsAsync(CancellationToken ct = default)
    {
        // Get the current content graph
        var contentGraph = await _contentGraphRepository.GetAsync(ct);
        if (contentGraph == null)
        {
            throw new InvalidOperationException("Content graph not found. Please upload content first.");
        }

        // Check if lessons already exist
        var existingLessons = await _lessonPlanRepository.GetAllAsync(ct);
        if (existingLessons.Count > 0)
        {
            return existingLessons.Count; // Return existing count
        }

        // Generate new lessons
        var lessons = await _lessonPlanGenerator.GenerateInitialLessonsAsync(contentGraph, ct);
        
        if (lessons.Count > 0)
        {
            await _lessonPlanRepository.ReplaceAllAsync(lessons, ct);
            await _lessonPlanRepository.SaveChangesAsync(ct);
        }

        return lessons.Count;
    }
}