using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

/// <summary>
/// Repository for domain knowledge graphs.
/// </summary>
public interface IDomainGraphRepository
{
    Task<DomainKnowledgeGraph?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(DomainKnowledgeGraph graph, CancellationToken ct = default);
}
