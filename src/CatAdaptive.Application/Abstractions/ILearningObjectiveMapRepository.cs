using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

/// <summary>
/// Repository for learning objective mappings.
/// </summary>
public interface ILearningObjectiveMapRepository
{
    Task<LearningObjectiveMap?> GetByTopicAsync(string topic, CancellationToken ct = default);
    Task SaveAsync(string topic, LearningObjectiveMap map, CancellationToken ct = default);
    Task<LearningObjectiveMap?> GetDefaultAsync(CancellationToken ct = default);
    Task SaveDefaultAsync(LearningObjectiveMap map, CancellationToken ct = default);
}
