using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Aggregates;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Infrastructure.Repositories;

/// <summary>
/// JSON-based implementation of IKnowledgeGraphRepository.
/// </summary>
public sealed class JsonKnowledgeGraphRepository : IKnowledgeGraphRepository
{
    private readonly string _dataDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<Guid, KnowledgeGraph> _cache = new();

    public JsonKnowledgeGraphRepository(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
        _jsonOptions = JsonRepositoryDefaults.CamelCase;

    }

    public async Task<KnowledgeGraph> GetByLearnerAsync(Guid learnerId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(learnerId, out var cached))
            return cached;

        var filePath = GetFilePath(learnerId);
        
        if (!File.Exists(filePath))
        {
            var newGraph = new KnowledgeGraph(learnerId);
            _cache[learnerId] = newGraph;
            return newGraph;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            var dto = JsonSerializer.Deserialize<KnowledgeGraphDto>(json, _jsonOptions);
            var graph = dto?.ToKnowledgeGraph() ?? new KnowledgeGraph(learnerId);
            _cache[learnerId] = graph;
            return graph;
        }
        catch
        {
            var graph = new KnowledgeGraph(learnerId);
            _cache[learnerId] = graph;
            return graph;
        }
    }

    public async Task SaveAsync(KnowledgeGraph graph, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var filePath = GetFilePath(graph.LearnerId);
        JsonRepositoryDefaults.EnsureDirectoryForFile(filePath);

        var dto = KnowledgeGraphDto.FromKnowledgeGraph(graph);
        var json = JsonSerializer.Serialize(dto, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, ct);
        
        _cache[graph.LearnerId] = graph;
    }

    private string GetFilePath(Guid learnerId) => 
        Path.Combine(_dataDirectory, $"knowledge-graph-{learnerId}.json");
}

/// <summary>
/// DTO for JSON serialization of KnowledgeGraph.
/// </summary>
internal sealed class KnowledgeGraphDto
{
    public Guid LearnerId { get; set; }
    public List<ConceptMasteryDto> Masteries { get; set; } = new();

    public KnowledgeGraph ToKnowledgeGraph()
    {
        var graph = new KnowledgeGraph(LearnerId);
        
        foreach (var masteryDto in Masteries)
        {
            // Use reflection to set private field, or initialize with evidence
            graph.InitializeConcepts(new[] { masteryDto.ConceptId });
        }
        
        return graph;
    }

    public static KnowledgeGraphDto FromKnowledgeGraph(KnowledgeGraph graph)
    {
        return new KnowledgeGraphDto
        {
            LearnerId = graph.LearnerId,
            Masteries = graph.Masteries.Values.Select(ConceptMasteryDto.FromConceptMastery).ToList()
        };
    }
}

internal sealed class ConceptMasteryDto
{
    public Guid ConceptId { get; set; }
    public MasteryState State { get; set; }
    public double EvidenceStrength { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public double DecayRisk { get; set; }
    public List<ErrorType> CommonErrors { get; set; } = new();
    public TimeSpan? MedianLatency { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public int ExplainWhySuccessCount { get; set; }
    public int ApplicationSuccessCount { get; set; }
    public int IntegrationSuccessCount { get; set; }

    public ConceptMastery ToConceptMastery() => new()
    {
        ConceptId = ConceptId,
        State = State,
        EvidenceStrength = EvidenceStrength,
        LastAttemptAt = LastAttemptAt,
        DecayRisk = DecayRisk,
        CommonErrors = CommonErrors,
        MedianLatency = MedianLatency,
        CorrectCount = CorrectCount,
        IncorrectCount = IncorrectCount,
        ExplainWhySuccessCount = ExplainWhySuccessCount,
        ApplicationSuccessCount = ApplicationSuccessCount,
        IntegrationSuccessCount = IntegrationSuccessCount
    };

    public static ConceptMasteryDto FromConceptMastery(ConceptMastery mastery) => new()
    {
        ConceptId = mastery.ConceptId,
        State = mastery.State,
        EvidenceStrength = mastery.EvidenceStrength,
        LastAttemptAt = mastery.LastAttemptAt,
        DecayRisk = mastery.DecayRisk,
        CommonErrors = mastery.CommonErrors.ToList(),
        MedianLatency = mastery.MedianLatency,
        CorrectCount = mastery.CorrectCount,
        IncorrectCount = mastery.IncorrectCount,
        ExplainWhySuccessCount = mastery.ExplainWhySuccessCount,
        ApplicationSuccessCount = mastery.ApplicationSuccessCount,
        IntegrationSuccessCount = mastery.IntegrationSuccessCount
    };
}
