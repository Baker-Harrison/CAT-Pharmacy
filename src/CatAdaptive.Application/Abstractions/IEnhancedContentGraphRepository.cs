using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

/// <summary>
/// Repository for Enhanced Content Graph operations.
/// </summary>
public interface IEnhancedContentGraphRepository
{
    /// <summary>
    /// Gets the enhanced content graph.
    /// </summary>
    Task<EnhancedContentGraph?> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the enhanced content graph.
    /// </summary>
    Task SaveAsync(EnhancedContentGraph contentGraph, CancellationToken ct = default);

    /// <summary>
    /// Adds a new content node.
    /// </summary>
    Task AddNodeAsync(EnhancedContentNode node, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing content node.
    /// </summary>
    Task UpdateNodeAsync(EnhancedContentNode node, CancellationToken ct = default);

    /// <summary>
    /// Gets content nodes by type.
    /// </summary>
    Task<IReadOnlyList<EnhancedContentNode>> GetNodesByTypeAsync(ContentNodeType type, CancellationToken ct = default);

    /// <summary>
    /// Gets content for specific domain nodes.
    /// </summary>
    Task<IReadOnlyList<EnhancedContentNode>> GetContentForDomainNodesAsync(IEnumerable<Guid> domainNodeIds, CancellationToken ct = default);
}
