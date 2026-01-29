using CatAdaptive.Domain.Aggregates;

namespace CatAdaptive.Application.Abstractions;

/// <summary>
/// Repository for persisting and retrieving per-learner Knowledge Graphs.
/// </summary>
public interface IKnowledgeGraphRepository
{
    /// <summary>
    /// Gets the knowledge graph for a learner. Creates new if not exists.
    /// </summary>
    Task<KnowledgeGraph> GetByLearnerAsync(Guid learnerId, CancellationToken ct = default);

    /// <summary>
    /// Saves the knowledge graph for a learner.
    /// </summary>
    Task SaveAsync(KnowledgeGraph graph, CancellationToken ct = default);
}
