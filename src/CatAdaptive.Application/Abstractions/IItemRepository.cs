using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

public interface IItemRepository
{
    Task<IReadOnlyList<ItemTemplate>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ItemTemplate>> GetByTopicAsync(string topic, CancellationToken ct = default);
    Task<ItemTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(ItemTemplate item, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<ItemTemplate> items, CancellationToken ct = default);
    Task ReplaceAllAsync(IEnumerable<ItemTemplate> items, CancellationToken ct = default);
    Task UpdateAsync(ItemTemplate item, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
