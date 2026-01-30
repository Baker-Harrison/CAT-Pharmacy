using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Infrastructure.Repositories;

public sealed class JsonKnowledgeUnitRepository : BaseJsonRepository<KnowledgeUnit>, IKnowledgeUnitRepository
{
    public JsonKnowledgeUnitRepository(string dataDirectory) 
        : base(dataDirectory, "knowledge-units.json")
    {
    }

    protected override Guid GetId(KnowledgeUnit unit) => unit.Id;

    public async Task<IReadOnlyList<KnowledgeUnit>> GetByTopicAsync(string topic, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _items.Where(u => u.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}