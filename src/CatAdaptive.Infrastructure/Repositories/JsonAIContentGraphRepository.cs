using System.Collections.Concurrent;
using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Infrastructure.Repositories;

/// <summary>
/// JSON-based repository for AI-enhanced content graphs.
/// </summary>
public sealed class JsonAIContentGraphRepository : IAIContentGraphRepository
{
    private readonly string _dataDirectory;
    private readonly ILogger<JsonAIContentGraphRepository> _logger;
    private readonly ConcurrentDictionary<string, AIEnhancedContentGraph> _cache = new();
    private const string DefaultTopic = "default";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonAIContentGraphRepository(string dataDirectory, ILogger<JsonAIContentGraphRepository> logger)
    {
        _dataDirectory = dataDirectory;
        _logger = logger;
        Directory.CreateDirectory(_dataDirectory);
    }

    public async Task<AIEnhancedContentGraph?> GetByTopicAsync(string topic, CancellationToken ct = default)
    {
        var key = SanitizeFileName(topic);
        
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var filePath = GetFilePath(key);

        if (!File.Exists(filePath))
        {
            _logger.LogInformation("AI content graph not found for topic '{Topic}'", topic);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            var graph = JsonSerializer.Deserialize<AIEnhancedContentGraph>(json, JsonOptions);
            
            if (graph != null)
            {
                _cache.TryAdd(key, graph);
            }
            
            return graph;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load AI content graph for topic '{Topic}'", topic);
            return null;
        }
    }

    public async Task SaveAsync(string topic, AIEnhancedContentGraph graph, CancellationToken ct = default)
    {
        var key = SanitizeFileName(topic);
        var filePath = GetFilePath(key);

        try
        {
            var json = JsonSerializer.Serialize(graph, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);
            
            _cache.AddOrUpdate(key, graph, (_, _) => graph);
            
            _logger.LogInformation("Saved AI content graph for topic '{Topic}' with {NodeCount} nodes", 
                topic, graph.Nodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save AI content graph for topic '{Topic}'", topic);
            throw;
        }
    }

    public Task<AIEnhancedContentGraph?> GetDefaultAsync(CancellationToken ct = default) =>
        GetByTopicAsync(DefaultTopic, ct);

    public Task SaveDefaultAsync(AIEnhancedContentGraph graph, CancellationToken ct = default) =>
        SaveAsync(DefaultTopic, graph, ct);

    private string GetFilePath(string sanitizedTopic) =>
        Path.Combine(_dataDirectory, $"ai-content-graph-{sanitizedTopic}.json");

    private static string SanitizeFileName(string topic)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", topic.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.ToLowerInvariant().Replace(" ", "-");
    }
}
