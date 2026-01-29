using CatAdaptive.Domain.Aggregates;

namespace CatAdaptive.Application.Abstractions;

/// <summary>
/// Repository for persisting and retrieving Content Graphs.
/// </summary>
public interface IContentGraphRepository
{
    /// <summary>
    /// Gets the current content graph (singleton per course/context).
    /// </summary>
    Task<ContentGraph> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the content graph.
    /// </summary>
    Task SaveAsync(ContentGraph graph, CancellationToken ct = default);
}
