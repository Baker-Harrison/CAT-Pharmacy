using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

/// <summary>
/// Repository for Domain Knowledge Graph operations.
/// </summary>
public interface IDomainKnowledgeGraphRepository
{
    /// <summary>
    /// Gets the domain knowledge graph.
    /// </summary>
    Task<DomainKnowledgeGraph?> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the domain knowledge graph.
    /// </summary>
    Task SaveAsync(DomainKnowledgeGraph graph, CancellationToken ct = default);

    /// <summary>
    /// Initializes the domain graph with basic structure.
    /// </summary>
    Task<DomainKnowledgeGraph> InitializeAsync(CancellationToken ct = default);
}
