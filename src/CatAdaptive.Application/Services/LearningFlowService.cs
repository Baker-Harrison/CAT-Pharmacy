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
    private static readonly LearnerProfile DefaultLearner = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Default Learner",
        Array.Empty<string>());

    private readonly ContentIngestionService _ingestionService;
    private readonly AssessmentService _assessmentService;
    private readonly IContentGraphRepository _contentGraphRepository;
    private readonly IKnowledgeGraphRepository _knowledgeGraphRepository;
    private readonly ILessonPlanGenerator _lessonPlanGenerator;
    private readonly ILessonPlanRepository _lessonPlanRepository;

    public LearningFlowService(
        ContentIngestionService ingestionService,
        AssessmentService assessmentService,
        IContentGraphRepository contentGraphRepository,
        IKnowledgeGraphRepository knowledgeGraphRepository,
        ILessonPlanGenerator lessonPlanGenerator,
        ILessonPlanRepository lessonPlanRepository)
    {
        _ingestionService = ingestionService;
        _assessmentService = assessmentService;
        _contentGraphRepository = contentGraphRepository;
        _knowledgeGraphRepository = knowledgeGraphRepository;
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

        // Load learner state for adaptive sequencing
        var knowledgeGraph = await _knowledgeGraphRepository.GetByLearnerAsync(DefaultLearner.Id, ct);
        knowledgeGraph.InitializeConcepts(contentGraph.GetConcepts().Select(c => c.Id));
        knowledgeGraph.ApplyDecay(TimeSpan.FromDays(14), DateTimeOffset.UtcNow);
        await _knowledgeGraphRepository.SaveAsync(knowledgeGraph, ct);

        var hasEvidence = knowledgeGraph.Masteries.Values.Any(m =>
            m.State != MasteryState.Unknown ||
            m.CorrectCount > 0 ||
            m.IncorrectCount > 0);

        // Generate new lessons
        var lessons = hasEvidence
            ? await _lessonPlanGenerator.GenerateNextLessonsAsync(
                contentGraph,
                knowledgeGraph,
                Array.Empty<Guid>(),
                ct)
            : await _lessonPlanGenerator.GenerateInitialLessonsAsync(contentGraph, ct);
        
        if (lessons.Count > 0)
        {
            await _lessonPlanRepository.ReplaceAllAsync(lessons, ct);
            await _lessonPlanRepository.SaveChangesAsync(ct);
        }

        return lessons.Count;
    }
}
