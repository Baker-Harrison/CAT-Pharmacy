using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Infrastructure.Repositories;

/// <summary>
/// JSON-based repository for domain knowledge graphs.
/// </summary>
public sealed class JsonDomainGraphRepository : IDomainGraphRepository
{
    private readonly string _dataDirectory;
    private readonly ILogger<JsonDomainGraphRepository> _logger;
    private DomainKnowledgeGraph? _cachedGraph;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonDomainGraphRepository(string dataDirectory, ILogger<JsonDomainGraphRepository> logger)
    {
        _dataDirectory = dataDirectory;
        _logger = logger;
        Directory.CreateDirectory(_dataDirectory);
    }

    public async Task<DomainKnowledgeGraph?> GetAsync(CancellationToken ct = default)
    {
        if (_cachedGraph != null)
            return _cachedGraph;

        var filePath = GetFilePath();

        if (!File.Exists(filePath))
        {
            _logger.LogInformation("Domain knowledge graph not found, creating empty graph");
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            var graph = JsonSerializer.Deserialize<DomainKnowledgeGraph>(json, JsonOptions);
            
            _cachedGraph = graph;
            _logger.LogInformation("Loaded domain knowledge graph with {NodeCount} nodes", 
                graph?.Nodes.Count ?? 0);
            
            return graph;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load domain knowledge graph");
            return null;
        }
    }

    public async Task SaveAsync(DomainKnowledgeGraph graph, CancellationToken ct = default)
    {
        var filePath = GetFilePath();

        try
        {
            var json = JsonSerializer.Serialize(graph, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);
            
            _cachedGraph = graph;
            
            _logger.LogInformation("Saved domain knowledge graph with {NodeCount} nodes and {EdgeCount} edges", 
                graph.Nodes.Count, graph.Edges.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save domain knowledge graph");
            throw;
        }
    }

    private string GetFilePath() =>
        Path.Combine(_dataDirectory, "domain-knowledge-graph.json");
}
