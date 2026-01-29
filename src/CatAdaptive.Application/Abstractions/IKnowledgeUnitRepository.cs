using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

public interface IKnowledgeUnitRepository
{
    Task<IReadOnlyList<KnowledgeUnit>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeUnit>> GetByTopicAsync(string topic, CancellationToken ct = default);
    Task<KnowledgeUnit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(KnowledgeUnit unit, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<KnowledgeUnit> units, CancellationToken ct = default);
    Task ReplaceAllAsync(IEnumerable<KnowledgeUnit> units, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
