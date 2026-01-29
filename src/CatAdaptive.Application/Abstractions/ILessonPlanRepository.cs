using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

public interface ILessonPlanRepository
{
    Task<IReadOnlyList<LessonPlan>> GetAllAsync(CancellationToken ct = default);
    Task<LessonPlan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<LessonPlan> lessons, CancellationToken ct = default);
    Task ReplaceAllAsync(IEnumerable<LessonPlan> lessons, CancellationToken ct = default);
    Task UpdateAsync(LessonPlan lesson, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
