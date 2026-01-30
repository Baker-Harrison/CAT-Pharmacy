using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Infrastructure.Repositories;

/// <summary>
/// JSON-based repository for Enhanced Content Graph persistence.
/// </summary>
public sealed class JsonEnhancedContentGraphRepository : IEnhancedContentGraphRepository
{
    private readonly string _dataDirectory;
    private readonly string _filePath;
    private readonly ILogger<JsonEnhancedContentGraphRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonEnhancedContentGraphRepository(ILogger<JsonEnhancedContentGraphRepository> logger)
    {
        _logger = logger;
        _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatAdaptive", "data");
        _filePath = Path.Combine(_dataDirectory, "enhanced-content-graph.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        Directory.CreateDirectory(_dataDirectory);
    }

    public async Task<EnhancedContentGraph?> GetAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            var json = await File.ReadAllTextAsync(_filePath, ct);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var data = JsonSerializer.Deserialize<EnhancedContentGraphData>(json, _jsonOptions);
            if (data == null)
                return null;

            var graph = new EnhancedContentGraph();
            
            foreach (var node in data.Nodes)
            {
                graph.AddNode(node);
            }

            foreach (var edge in data.Edges)
            {
                graph.AddEdge(edge);
            }

            _logger.LogInformation("Loaded Enhanced Content Graph with {NodeCount} nodes and {EdgeCount} edges", 
                data.Nodes.Count, data.Edges.Count);

            return graph;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Enhanced Content Graph");
            return null;
        }
    }

    public async Task SaveAsync(EnhancedContentGraph contentGraph, CancellationToken ct = default)
    {
        try
        {
            var data = new EnhancedContentGraphData(
                contentGraph.Nodes.Values.ToList(),
                contentGraph.Edges.Values.ToList());

            var json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, json, ct);

            _logger.LogInformation("Saved Enhanced Content Graph with {NodeCount} nodes and {EdgeCount} edges", 
                data.Nodes.Count, data.Edges.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Enhanced Content Graph");
            throw;
        }
    }

    public async Task AddNodeAsync(EnhancedContentNode node, CancellationToken ct = default)
    {
        var graph = await GetAsync(ct) ?? new EnhancedContentGraph();
        graph.AddNode(node);
        await SaveAsync(graph, ct);
    }

    public async Task UpdateNodeAsync(EnhancedContentNode node, CancellationToken ct = default)
    {
        var graph = await GetAsync(ct) ?? throw new InvalidOperationException("Content graph not found");
        graph.AddNode(node); // Add will overwrite existing node
        await SaveAsync(graph, ct);
    }

    public async Task<IReadOnlyList<EnhancedContentNode>> GetNodesByTypeAsync(ContentNodeType type, CancellationToken ct = default)
    {
        var graph = await GetAsync(ct);
        return graph?.GetNodesByType(type) ?? Array.Empty<EnhancedContentNode>();
    }

    public async Task<IReadOnlyList<EnhancedContentNode>> GetContentForDomainNodesAsync(IEnumerable<Guid> domainNodeIds, CancellationToken ct = default)
    {
        var graph = await GetAsync(ct);
        return graph?.GetContentForDomainNodes(domainNodeIds) ?? Array.Empty<EnhancedContentNode>();
    }

    private record EnhancedContentGraphData(
        List<EnhancedContentNode> Nodes,
        List<EnhancedContentEdge> Edges);
}
