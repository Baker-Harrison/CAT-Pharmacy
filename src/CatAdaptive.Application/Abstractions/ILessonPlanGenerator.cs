using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

public interface ILessonPlanGenerator
{
    Task<IReadOnlyList<LessonPlan>> GenerateInitialLessonsAsync(
        AIEnhancedContentGraph contentGraph,
        CancellationToken ct = default);

    Task<IReadOnlyList<LessonPlan>> GenerateNextLessonsAsync(
        AIEnhancedContentGraph contentGraph,
        DomainKnowledgeGraph knowledgeGraph,
        IReadOnlyList<Guid> existingConceptIds,
        CancellationToken ct = default);

    Task<LessonPlan?> GenerateRemediationLessonAsync(
        AIEnhancedContentGraph contentGraph,
        DomainKnowledgeGraph knowledgeGraph,
        Guid conceptId,
        CancellationToken ct = default);
}
