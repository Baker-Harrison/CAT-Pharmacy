using CatAdaptive.Application.Abstractions;
using CatAdaptive.Application.Models;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Application.Services;

public sealed class ContentIngestionService
{
    private readonly IPptxParser _pptxParser;
    private readonly IKnowledgeUnitRepository _knowledgeUnitRepository;
    private readonly IItemGenerator _itemGenerator;
    private readonly IItemRepository _itemRepository;
    private readonly IAIContentGraphRepository _contentGraphRepository;
    private readonly IDomainGraphRepository _domainGraphRepository;
    private readonly AIContentExpansionService? _contentExpansionService;
    private readonly ILogger<ContentIngestionService> _logger;

    public ContentIngestionService(
        IPptxParser pptxParser,
        IKnowledgeUnitRepository knowledgeUnitRepository,
        IItemGenerator itemGenerator,
        IItemRepository itemRepository,
        IAIContentGraphRepository contentGraphRepository,
        IDomainGraphRepository domainGraphRepository,
        ILogger<ContentIngestionService> logger,
        AIContentExpansionService? contentExpansionService = null)
    {
        _pptxParser = pptxParser;
        _knowledgeUnitRepository = knowledgeUnitRepository;
        _itemGenerator = itemGenerator;
        _itemRepository = itemRepository;
        _contentGraphRepository = contentGraphRepository;
        _domainGraphRepository = domainGraphRepository;
        _contentExpansionService = contentExpansionService;
        _logger = logger;
    }

    public async Task<LearningIngestionResult> IngestAsync(string filePath, CancellationToken ct = default)
    {
        // 1. Knowledge Units
        var existingUnits = await _knowledgeUnitRepository.GetAllAsync(ct);
        IReadOnlyList<KnowledgeUnit> knowledgeUnits;

        if (existingUnits.Count > 0)
        {
            _logger.LogInformation("Found {Count} existing knowledge units. Skipping parsing.", existingUnits.Count);
            knowledgeUnits = existingUnits;
        }
        else
        {
            _logger.LogInformation("Parsing PPTX...");
            knowledgeUnits = await _pptxParser.ParseAsync(filePath, ct);
            await _knowledgeUnitRepository.ReplaceAllAsync(knowledgeUnits, ct);
            await _knowledgeUnitRepository.SaveChangesAsync(ct);
            _logger.LogInformation("Created {Count} knowledge units.", knowledgeUnits.Count);
        }

        // 2. Items
        var existingItems = await _itemRepository.GetAllAsync(ct);
        int itemsCount;

        if (existingItems.Count > 0)
        {
            _logger.LogInformation("Found {Count} existing items. Skipping item generation.", existingItems.Count);
            itemsCount = existingItems.Count;
        }
        else
        {
            _logger.LogInformation("Generating items...");
            var generatedItems = await _itemGenerator.GenerateItemsAsync(knowledgeUnits, ct);
            await _itemRepository.ReplaceAllAsync(generatedItems, ct);
            await _itemRepository.SaveChangesAsync(ct);
            itemsCount = generatedItems.Count;
            _logger.LogInformation("Generated {Count} items.", itemsCount);
        }

        // 3. Build AI-enhanced content graph (if expansion service available)
        if (_contentExpansionService != null)
        {
            _logger.LogInformation("Expanding content graph with AI...");
            var config = new ExpansionConfig(EnableWebSearch: false);
            var contentGraph = await _contentExpansionService.ExpandContentGraph10XAsync(knowledgeUnits, config, ct);
            await _contentGraphRepository.SaveDefaultAsync(contentGraph, ct);
            _logger.LogInformation("AI content graph created with {Count} nodes.", contentGraph.Nodes.Count);
        }
        else
        {
            _logger.LogInformation("Creating basic content graph...");
            var contentGraph = BuildBasicContentGraph(knowledgeUnits);
            await _contentGraphRepository.SaveDefaultAsync(contentGraph, ct);
        }

        // 4. Build domain knowledge graph
        var domainGraph = BuildDomainGraph(knowledgeUnits);
        await _domainGraphRepository.SaveAsync(domainGraph, ct);

        // 5. Return result
        return new LearningIngestionResult(
            knowledgeUnits.Count,
            itemsCount);
    }

    private static AIEnhancedContentGraph BuildBasicContentGraph(IReadOnlyList<KnowledgeUnit> knowledgeUnits)
    {
        var graph = new AIEnhancedContentGraph();

        foreach (var unit in knowledgeUnits)
        {
            var nodeId = Guid.NewGuid();
            var node = new ContentNode(
                Id: nodeId,
                Type: ContentNodeType.Explanation,
                Title: unit.Topic,
                Content: $"{unit.Summary}\n\nKey Points:\n{string.Join("\n• ", unit.KeyPoints)}",
                Modality: ContentModality.Text,
                BloomsLevel: BloomsLevel.Understand,
                Difficulty: 0.5,
                EstimatedTimeMinutes: 10,
                LinkedDomainNodes: new[] { nodeId },
                Tags: new[] { unit.Topic, unit.Subtopic },
                SourceOrigin: ContentOrigin.Slides,
                QualityScore: 0.8,
                CreatedAt: DateTimeOffset.UtcNow);

            graph.AddNode(node);
        }

        return graph;
    }

    private static DomainKnowledgeGraph BuildDomainGraph(IReadOnlyList<KnowledgeUnit> knowledgeUnits)
    {
        var graph = new DomainKnowledgeGraph();

        foreach (var unit in knowledgeUnits)
        {
            var node = new DomainNode(
                Id: Guid.NewGuid(),
                Title: unit.Topic,
                Description: unit.Summary,
                Type: DomainNodeType.Concept,
                BloomsLevel: BloomsLevel.Understand,
                Difficulty: 0.5,
                ExamRelevanceWeight: 1.0,
                Tags: unit.KeyPoints.Take(3).ToList());

            graph.AddNode(node);
        }

        return graph;
    }
}
