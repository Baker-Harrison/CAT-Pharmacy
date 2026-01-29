using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Aggregates;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Infrastructure.Repositories;

/// <summary>
/// JSON-based implementation of IContentGraphRepository.
/// </summary>
public sealed class JsonContentGraphRepository : IContentGraphRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private ContentGraph? _cachedGraph;

    public JsonContentGraphRepository(string dataDirectory)
    {
        _filePath = Path.Combine(dataDirectory, "content-graph.json");
        _jsonOptions = JsonRepositoryDefaults.CamelCase;

    }

    public async Task<ContentGraph> GetAsync(CancellationToken ct = default)
    {
        if (_cachedGraph != null)
            return _cachedGraph;

        if (!File.Exists(_filePath))
        {
            _cachedGraph = new ContentGraph();
            return _cachedGraph;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            var dto = JsonSerializer.Deserialize<ContentGraphDto>(json, _jsonOptions);
            _cachedGraph = dto?.ToContentGraph() ?? new ContentGraph();
            return _cachedGraph;
        }
        catch
        {
            _cachedGraph = new ContentGraph();
            return _cachedGraph;
        }
    }

    public async Task SaveAsync(ContentGraph graph, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(graph);

        JsonRepositoryDefaults.EnsureDirectoryForFile(_filePath);


        var dto = ContentGraphDto.FromContentGraph(graph);
        var json = JsonSerializer.Serialize(dto, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct);
        
        _cachedGraph = graph;
    }
}

/// <summary>
/// DTO for JSON serialization of ContentGraph.
/// </summary>
internal sealed class ContentGraphDto
{
    public List<ContentNodeDto> Nodes { get; set; } = new();
    public List<ContentEdgeDto> Edges { get; set; } = new();

    public ContentGraph ToContentGraph()
    {
        var graph = new ContentGraph();
        
        foreach (var nodeDto in Nodes)
        {
            graph.AddNode(nodeDto.ToContentNode());
        }
        
        foreach (var edgeDto in Edges)
        {
            graph.AddEdge(edgeDto.ToContentEdge());
        }
        
        return graph;
    }

    public static ContentGraphDto FromContentGraph(ContentGraph graph)
    {
        return new ContentGraphDto
        {
            Nodes = graph.Nodes.Values.Select(ContentNodeDto.FromContentNode).ToList(),
            Edges = graph.Edges.Values.Select(ContentEdgeDto.FromContentEdge).ToList()
        };
    }
}

internal sealed class ContentNodeDto
{
    public Guid Id { get; set; }
    public ContentNodeType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public ContentOrigin SourceOrigin { get; set; }
    public string SourceRef { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public bool InstructorAligned { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ContentNode ToContentNode() => new()
    {
        Id = Id,
        Type = Type,
        Text = Text,
        SourceOrigin = SourceOrigin,
        SourceRef = SourceRef,
        Confidence = Confidence,
        InstructorAligned = InstructorAligned,
        Version = Version,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };

    public static ContentNodeDto FromContentNode(ContentNode node) => new()
    {
        Id = node.Id,
        Type = node.Type,
        Text = node.Text,
        SourceOrigin = node.SourceOrigin,
        SourceRef = node.SourceRef,
        Confidence = node.Confidence,
        InstructorAligned = node.InstructorAligned,
        Version = node.Version,
        CreatedAt = node.CreatedAt,
        UpdatedAt = node.UpdatedAt
    };
}

internal sealed class ContentEdgeDto
{
    public Guid Id { get; set; }
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public ContentEdgeType Type { get; set; }

    public ContentEdge ToContentEdge() => new()
    {
        Id = Id,
        FromNodeId = FromNodeId,
        ToNodeId = ToNodeId,
        Type = Type
    };

    public static ContentEdgeDto FromContentEdge(ContentEdge edge) => new()
    {
        Id = edge.Id,
        FromNodeId = edge.FromNodeId,
        ToNodeId = edge.ToNodeId,
        Type = edge.Type
    };
}
