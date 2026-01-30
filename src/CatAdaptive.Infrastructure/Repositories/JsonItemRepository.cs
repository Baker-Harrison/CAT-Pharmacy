using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Infrastructure.Repositories;

public sealed class JsonItemRepository : BaseJsonRepository<ItemTemplate>, IItemRepository
{
    public JsonItemRepository(string dataDirectory) 
        : base(dataDirectory, "item-bank.json")
    {
    }

    protected override Guid GetId(ItemTemplate item) => item.Id;

    public async Task<IReadOnlyList<ItemTemplate>> GetByTopicAsync(string topic, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _items.Where(i => i.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}