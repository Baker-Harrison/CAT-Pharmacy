using System.Collections.Concurrent;
using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Infrastructure.Repositories;

/// <summary>
/// JSON-based repository for learning objective maps.
/// </summary>
public sealed class JsonLearningObjectiveMapRepository : ILearningObjectiveMapRepository
{
    private readonly string _dataDirectory;
    private readonly ILogger<JsonLearningObjectiveMapRepository> _logger;
    private readonly ConcurrentDictionary<string, LearningObjectiveMap> _cache = new();
    private const string DefaultTopic = "default";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonLearningObjectiveMapRepository(string dataDirectory, ILogger<JsonLearningObjectiveMapRepository> logger)
    {
        _dataDirectory = dataDirectory;
        _logger = logger;
        Directory.CreateDirectory(_dataDirectory);
    }

    public async Task<LearningObjectiveMap?> GetByTopicAsync(string topic, CancellationToken ct = default)
    {
        var key = SanitizeFileName(topic);
        
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var filePath = GetFilePath(key);

        if (!File.Exists(filePath))
        {
            _logger.LogInformation("Learning objective map not found for topic '{Topic}'", topic);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            var map = JsonSerializer.Deserialize<LearningObjectiveMap>(json, JsonOptions);
            
            if (map != null)
            {
                _cache.TryAdd(key, map);
            }
            
            return map;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load learning objective map for topic '{Topic}'", topic);
            return null;
        }
    }

    public async Task SaveAsync(string topic, LearningObjectiveMap map, CancellationToken ct = default)
    {
        var key = SanitizeFileName(topic);
        var filePath = GetFilePath(key);

        try
        {
            var json = JsonSerializer.Serialize(map, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);
            
            _cache.AddOrUpdate(key, map, (_, _) => map);
            
            _logger.LogInformation("Saved learning objective map for topic '{Topic}' with {Count} objectives", 
                topic, map.Objectives.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save learning objective map for topic '{Topic}'", topic);
            throw;
        }
    }

    public Task<LearningObjectiveMap?> GetDefaultAsync(CancellationToken ct = default) =>
        GetByTopicAsync(DefaultTopic, ct);

    public Task SaveDefaultAsync(LearningObjectiveMap map, CancellationToken ct = default) =>
        SaveAsync(DefaultTopic, map, ct);

    private string GetFilePath(string sanitizedTopic) =>
        Path.Combine(_dataDirectory, $"learning-objective-map-{sanitizedTopic}.json");

    private static string SanitizeFileName(string topic)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", topic.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.ToLowerInvariant().Replace(" ", "-");
    }
}
