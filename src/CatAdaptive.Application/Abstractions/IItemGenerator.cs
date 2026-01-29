using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

public interface IItemGenerator
{
    Task<IReadOnlyList<ItemTemplate>> GenerateItemsAsync(
        IEnumerable<KnowledgeUnit> knowledgeUnits,
        CancellationToken ct = default);
}
