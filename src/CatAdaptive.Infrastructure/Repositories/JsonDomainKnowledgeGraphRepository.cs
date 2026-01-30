using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Infrastructure.Repositories;

/// <summary>
/// JSON-based repository for Domain Knowledge Graph persistence.
/// </summary>
public sealed class JsonDomainKnowledgeGraphRepository : IDomainKnowledgeGraphRepository
{
    private readonly string _dataDirectory;
    private readonly string _filePath;
    private readonly ILogger<JsonDomainKnowledgeGraphRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonDomainKnowledgeGraphRepository(ILogger<JsonDomainKnowledgeGraphRepository> logger)
    {
        _logger = logger;
        _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatAdaptive", "data");
        _filePath = Path.Combine(_dataDirectory, "domain-knowledge-graph.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        Directory.CreateDirectory(_dataDirectory);
    }

    public async Task<DomainKnowledgeGraph?> GetAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            var json = await File.ReadAllTextAsync(_filePath, ct);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            // Deserialize the graph data
            var data = JsonSerializer.Deserialize<DomainKnowledgeGraphData>(json, _jsonOptions);
            if (data == null)
                return null;

            // Reconstruct the graph
            var graph = new DomainKnowledgeGraph();
            
            // Add nodes
            foreach (var node in data.Nodes)
            {
                graph.AddNode(node);
            }

            // Add edges
            foreach (var edge in data.Edges)
            {
                graph.AddEdge(edge);
            }

            _logger.LogInformation("Loaded Domain Knowledge Graph with {NodeCount} nodes and {EdgeCount} edges", 
                data.Nodes.Count, data.Edges.Count);

            return graph;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Domain Knowledge Graph");
            return null;
        }
    }

    public async Task SaveAsync(DomainKnowledgeGraph graph, CancellationToken ct = default)
    {
        try
        {
            var data = new DomainKnowledgeGraphData(
                graph.Nodes.Values.ToList(),
                graph.Edges.Values.ToList());

            var json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, json, ct);

            _logger.LogInformation("Saved Domain Knowledge Graph with {NodeCount} nodes and {EdgeCount} edges", 
                data.Nodes.Count, data.Edges.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Domain Knowledge Graph");
            throw;
        }
    }

    public async Task<DomainKnowledgeGraph> InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Initializing Domain Knowledge Graph with basic pharmacy concepts");

        var graph = new DomainKnowledgeGraph();

        // Add basic drug class concepts
        var aceInhibitors = new DomainNode(
            Guid.NewGuid(),
            DomainNodeType.Concept,
            "ACE Inhibitors",
            "Angiotensin-Converting Enzyme inhibitors block the conversion of angiotensin I to angiotensin II",
            BloomsLevel.Understand,
            0.6,
            0.9,
            new[] { "antihypertensive", "pharmacology", "mechanism" });

        var betaBlockers = new DomainNode(
            Guid.NewGuid(),
            DomainNodeType.Concept,
            "Beta Blockers",
            "Beta-adrenergic antagonists that block the effects of epinephrine",
            BloomsLevel.Understand,
            0.7,
            0.9,
            new[] { "antihypertensive", "pharmacology", "mechanism" });

        var doseCalculation = new DomainNode(
            Guid.NewGuid(),
            DomainNodeType.Skill,
            "Dose Calculation",
            "Calculate medication doses based on patient weight and concentration",
            BloomsLevel.Apply,
            0.8,
            0.95,
            new[] { "calculation", "clinical", "skills" });

        var hypertensionManagement = new DomainNode(
            Guid.NewGuid(),
            DomainNodeType.Objective,
            "Hypertension Management",
            "Develop comprehensive treatment plans for hypertensive patients",
            BloomsLevel.Create,
            0.9,
            0.85,
            new[] { "therapeutics", "clinical", "management" });

        graph.AddNode(aceInhibitors);
        graph.AddNode(betaBlockers);
        graph.AddNode(doseCalculation);
        graph.AddNode(hypertensionManagement);

        // Add prerequisite relationships
        graph.AddEdge(new DomainEdge(
            Guid.NewGuid(),
            doseCalculation.Id,
            hypertensionManagement.Id,
            DomainEdgeType.PrerequisiteOf,
            1.0));

        graph.AddEdge(new DomainEdge(
            Guid.NewGuid(),
            aceInhibitors.Id,
            hypertensionManagement.Id,
            DomainEdgeType.PartOf,
            0.8));

        graph.AddEdge(new DomainEdge(
            Guid.NewGuid(),
            betaBlockers.Id,
            hypertensionManagement.Id,
            DomainEdgeType.PartOf,
            0.8));

        // Add contrasting relationship
        graph.AddEdge(new DomainEdge(
            Guid.NewGuid(),
            aceInhibitors.Id,
            betaBlockers.Id,
            DomainEdgeType.ContrastsWith,
            0.6));

        await SaveAsync(graph, ct);

        _logger.LogInformation("Initialized Domain Knowledge Graph with {NodeCount} nodes", graph.Nodes.Count);

        return graph;
    }

    private record DomainKnowledgeGraphData(
        List<DomainNode> Nodes,
        List<DomainEdge> Edges);
}
