using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

/// <summary>
/// Repository for AI-enhanced content graphs.
/// </summary>
public interface IAIContentGraphRepository
{
    Task<AIEnhancedContentGraph?> GetByTopicAsync(string topic, CancellationToken ct = default);
    Task SaveAsync(string topic, AIEnhancedContentGraph graph, CancellationToken ct = default);
    Task<AIEnhancedContentGraph?> GetDefaultAsync(CancellationToken ct = default);
    Task SaveDefaultAsync(AIEnhancedContentGraph graph, CancellationToken ct = default);
}
