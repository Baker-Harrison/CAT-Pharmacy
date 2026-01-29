using CatAdaptive.Domain.Aggregates;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

public interface ILessonPlanGenerator
{
    Task<IReadOnlyList<LessonPlan>> GenerateInitialLessonsAsync(
        ContentGraph contentGraph,
        CancellationToken ct = default);

    Task<IReadOnlyList<LessonPlan>> GenerateNextLessonsAsync(
        ContentGraph contentGraph,
        KnowledgeGraph knowledgeGraph,
        IReadOnlyList<Guid> existingConceptIds,
        CancellationToken ct = default);

    Task<LessonPlan?> GenerateRemediationLessonAsync(
        ContentGraph contentGraph,
        KnowledgeGraph knowledgeGraph,
        Guid conceptId,
        CancellationToken ct = default);
}
