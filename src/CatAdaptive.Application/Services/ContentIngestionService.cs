using CatAdaptive.Application.Abstractions;
using CatAdaptive.Application.Models;
using CatAdaptive.Domain.Aggregates;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Application.Services;

public sealed class ContentIngestionService
{
    private static readonly LearnerProfile DefaultLearner = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Default Learner",
        Array.Empty<string>());

    private readonly IPptxParser _pptxParser;
    private readonly IKnowledgeUnitRepository _knowledgeUnitRepository;
    private readonly IItemGenerator _itemGenerator;
    private readonly IItemRepository _itemRepository;
    private readonly IContentGraphRepository _contentGraphRepository;
    private readonly IKnowledgeGraphRepository _knowledgeGraphRepository;
    private readonly ILogger<ContentIngestionService> _logger;

    public ContentIngestionService(
        IPptxParser pptxParser,
        IKnowledgeUnitRepository knowledgeUnitRepository,
        IItemGenerator itemGenerator,
        IItemRepository itemRepository,
        IContentGraphRepository contentGraphRepository,
        IKnowledgeGraphRepository knowledgeGraphRepository,
        ILogger<ContentIngestionService> logger)
    {
        _pptxParser = pptxParser;
        _knowledgeUnitRepository = knowledgeUnitRepository;
        _itemGenerator = itemGenerator;
        _itemRepository = itemRepository;
        _contentGraphRepository = contentGraphRepository;
        _knowledgeGraphRepository = knowledgeGraphRepository;
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

        // 3. Content & Knowledge Graphs
        var contentGraph = ContentGraphBuilder.BuildFromKnowledgeUnits(knowledgeUnits);
        await _contentGraphRepository.SaveAsync(contentGraph, ct);

        // Ensure KnowledgeGraph exists for the default learner
        var existingKg = await _knowledgeGraphRepository.GetByLearnerAsync(DefaultLearner.Id, ct);
        if (existingKg == null)
        {
             var emptyKnowledgeGraph = new KnowledgeGraph(DefaultLearner.Id);
             await _knowledgeGraphRepository.SaveAsync(emptyKnowledgeGraph, ct);
        }

        // 4. Return result
        return new LearningIngestionResult(
            knowledgeUnits.Count,
            itemsCount);
    }
}
