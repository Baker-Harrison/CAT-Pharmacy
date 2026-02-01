namespace CatAdaptive.Application.Abstractions;

/// <summary>
/// Service for interacting with Gemini AI.
/// </summary>
public interface IGeminiService
{
    Task<T> GenerateFromPromptAsync<T>(string prompt, CancellationToken ct = default) where T : class;
    Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default);
    Task<string> ExecuteReActAsync(string prompt, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GenerateThoughtPathsAsync(string prompt, int pathCount, CancellationToken ct = default);
    Task<string> SelectBestPathAsync(string evaluationPrompt, CancellationToken ct = default);
}

/// <summary>
/// Service for web search capabilities.
/// </summary>
public interface IWebSearchService
{
    Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int maxResults = 10, CancellationToken ct = default);
    Task<IReadOnlyList<WebSearchResult>> SearchAsync(IEnumerable<string> queries, CancellationToken ct = default);
}

/// <summary>
/// Result from a web search.
/// </summary>
public sealed record WebSearchResult(
    string Title,
    string Snippet,
    string Url,
    string? Source = null,
    double Relevance = 0.0);

/// <summary>
/// Configuration for content expansion.
/// </summary>
public sealed record ExpansionConfig(
    bool EnableWebSearch = true,
    int MaxSearchResults = 10,
    IReadOnlyList<string>? ContentSources = null,
    int ExpansionDepth = 3,
    double ValidationThreshold = 0.8);

/// <summary>
/// Core concept extracted from slides for expansion.
/// </summary>
public sealed record CoreConcept(
    Guid Id,
    string Title,
    string Description,
    IReadOnlyList<string> KeyPoints,
    IReadOnlyList<string> Keywords,
    Guid? SourceSlideId = null);

/// <summary>
/// Quality score for content validation.
/// </summary>
public sealed record ContentQualityScore(
    double Accuracy,
    double Clarity,
    double Completeness,
    double Engagement,
    double Appropriateness,
    double Currency,
    double OverallScore,
    string Recommendation);
