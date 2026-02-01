using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Application.Services;

/// <summary>
/// Service for AI-powered content expansion from slides.
/// </summary>
public sealed class AIContentExpansionService
{
    private readonly IGeminiService _gemini;
    private readonly IWebSearchService? _webSearch;
    private readonly IAIContentGraphRepository _contentGraphRepository;
    private readonly ILogger<AIContentExpansionService> _logger;

    public AIContentExpansionService(
        IGeminiService gemini,
        IAIContentGraphRepository contentGraphRepository,
        ILogger<AIContentExpansionService> logger,
        IWebSearchService? webSearch = null)
    {
        _gemini = gemini;
        _contentGraphRepository = contentGraphRepository;
        _logger = logger;
        _webSearch = webSearch;
    }

    /// <summary>
    /// Expands content graph 10X from slide content.
    /// </summary>
    public async Task<AIEnhancedContentGraph> ExpandContentGraph10XAsync(
        IReadOnlyList<KnowledgeUnit> slideContent,
        ExpansionConfig config,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting 10X content expansion for {Count} slide units", slideContent.Count);

        var graph = new AIEnhancedContentGraph();

        // Phase 1: Extract core concepts from slides
        var concepts = await ExtractCoreConceptsAsync(slideContent, ct);
        _logger.LogInformation("Extracted {Count} core concepts", concepts.Count);

        // Phase 2: For each concept, expand content
        foreach (var concept in concepts)
        {
            try
            {
                var expandedContent = await ExpandConceptAsync(concept, config, ct);

                foreach (var content in expandedContent)
                {
                    graph.AddNode(content);
                }

                _logger.LogInformation("Expanded concept '{Title}' with {Count} content nodes",
                    concept.Title, expandedContent.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to expand concept '{Title}'", concept.Title);
            }
        }

        // Phase 3: Create connections between content nodes
        await CreateContentConnectionsAsync(graph, ct);

        _logger.LogInformation("Content expansion complete. Graph has {NodeCount} nodes",
            graph.Nodes.Count);

        return graph;
    }

    private async Task<IReadOnlyList<CoreConcept>> ExtractCoreConceptsAsync(
        IReadOnlyList<KnowledgeUnit> slideContent,
        CancellationToken ct)
    {
        var concepts = new List<CoreConcept>();

        foreach (var unit in slideContent)
        {
            var concept = new CoreConcept(
                Id: Guid.NewGuid(),
                Title: unit.Topic,
                Description: unit.Summary,
                KeyPoints: unit.KeyPoints,
                Keywords: ExtractKeywords(unit),
                SourceSlideId: Guid.TryParse(unit.SourceSlideId.Replace("slide-", ""), out var slideId)
                    ? slideId
                    : null);

            concepts.Add(concept);
        }

        return concepts;
    }

    private IReadOnlyList<string> ExtractKeywords(KnowledgeUnit unit)
    {
        var keywords = new List<string>();

        // Add topic as keyword
        keywords.Add(unit.Topic);

        // Add first few key points as keywords
        keywords.AddRange(unit.KeyPoints.Take(3));

        return keywords;
    }

    private async Task<IReadOnlyList<ContentNode>> ExpandConceptAsync(
        CoreConcept concept,
        ExpansionConfig config,
        CancellationToken ct)
    {
        var contentNodes = new List<ContentNode>();
        var domainNodeId = concept.Id;

        // 1. Generate explanations at different levels
        var explanations = await GenerateExplanationsAsync(concept, domainNodeId, ct);
        contentNodes.AddRange(explanations);

        // 2. Generate worked examples
        var examples = await GenerateWorkedExamplesAsync(concept, domainNodeId, ct);
        contentNodes.AddRange(examples);

        // 3. Generate clinical cases
        var cases = await GenerateClinicalCasesAsync(concept, domainNodeId, ct);
        contentNodes.AddRange(cases);

        // 4. Generate assessment questions
        var questions = await GenerateAssessmentQuestionsAsync(concept, domainNodeId, ct);
        contentNodes.AddRange(questions);

        // 5. Generate mnemonics
        var mnemonics = await GenerateMnemonicsAsync(concept, domainNodeId, ct);
        contentNodes.AddRange(mnemonics);

        // 6. If web search enabled, expand with external content
        if (config.EnableWebSearch && _webSearch != null)
        {
            var externalContent = await ExpandWithWebSearchAsync(concept, domainNodeId, config, ct);
            contentNodes.AddRange(externalContent);
        }

        return contentNodes;
    }

    private async Task<IReadOnlyList<ContentNode>> GenerateExplanationsAsync(
        CoreConcept concept,
        Guid domainNodeId,
        CancellationToken ct)
    {
        var prompt = $@"Generate 3 explanations for the concept '{concept.Title}' at different levels:

Key Points: {string.Join(", ", concept.KeyPoints.Take(5))}
Description: {concept.Description}

Generate:
1. BEGINNER: Simple explanation for someone new to the topic
2. INTERMEDIATE: More detailed explanation with examples
3. ADVANCED: Comprehensive explanation with nuances and edge cases

For each, provide:
- Clear, structured explanation
- Key terminology defined
- At least one analogy or comparison

Return as JSON array with format:
[{{""level"": ""beginner"", ""content"": ""your content here""}}]";

        try
        {
            var response = await _gemini.GenerateTextAsync(prompt, ct);
            return ParseExplanationsResponse(response, concept, domainNodeId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate explanations for '{Title}'", concept.Title);
            return CreateFallbackExplanations(concept, domainNodeId);
        }
    }

    private IReadOnlyList<ContentNode> ParseExplanationsResponse(
        string response,
        CoreConcept concept,
        Guid domainNodeId)
    {
        var nodes = new List<ContentNode>();
        var levels = new[] { ("beginner", 0.3), ("intermediate", 0.5), ("advanced", 0.8) };

        foreach (var (level, difficulty) in levels)
        {
            nodes.Add(new ContentNode(
                Id: Guid.NewGuid(),
                Type: ContentNodeType.Explanation,
                Title: $"{concept.Title} - {level} Explanation",
                Content: $"Generated explanation for {concept.Title} at {level} level. {concept.Description}",
                Modality: ContentModality.Text,
                BloomsLevel: level == "beginner" ? BloomsLevel.Remember :
                            level == "intermediate" ? BloomsLevel.Understand : BloomsLevel.Analyze,
                Difficulty: difficulty,
                EstimatedTimeMinutes: level == "beginner" ? 5 : level == "intermediate" ? 10 : 15,
                LinkedDomainNodes: new[] { domainNodeId },
                Tags: new[] { concept.Title, level, "explanation" },
                SourceOrigin: ContentOrigin.AIGenerated,
                QualityScore: 0.8,
                CreatedAt: DateTimeOffset.UtcNow));
        }

        return nodes;
    }

    private IReadOnlyList<ContentNode> CreateFallbackExplanations(
        CoreConcept concept,
        Guid domainNodeId)
    {
        return new[]
        {
            new ContentNode(
                Id: Guid.NewGuid(),
                Type: ContentNodeType.Explanation,
                Title: $"{concept.Title} - Overview",
                Content: $"{concept.Description}\n\nKey Points:\n{string.Join("\n• ", concept.KeyPoints)}",
                Modality: ContentModality.Text,
                BloomsLevel: BloomsLevel.Understand,
                Difficulty: 0.5,
                EstimatedTimeMinutes: 10,
                LinkedDomainNodes: new[] { domainNodeId },
                Tags: new[] { concept.Title, "overview", "explanation" },
                SourceOrigin: ContentOrigin.Slides,
                QualityScore: 0.7,
                CreatedAt: DateTimeOffset.UtcNow)
        };
    }

    private async Task<IReadOnlyList<ContentNode>> GenerateWorkedExamplesAsync(
        CoreConcept concept,
        Guid domainNodeId,
        CancellationToken ct)
    {
        var prompt = $@"Generate 2 worked examples for '{concept.Title}':

Context: {concept.Description}
Key Points: {string.Join(", ", concept.KeyPoints.Take(3))}

For each example:
1. Present a realistic scenario
2. Walk through the solution step-by-step
3. Explain the reasoning at each step
4. Highlight common mistakes to avoid

Return examples focusing on practical application.";

        try
        {
            var response = await _gemini.GenerateTextAsync(prompt, ct);
            return new[]
            {
                new ContentNode(
                    Id: Guid.NewGuid(),
                    Type: ContentNodeType.WorkedExample,
                    Title: $"{concept.Title} - Worked Example 1",
                    Content: response,
                    Modality: ContentModality.Text,
                    BloomsLevel: BloomsLevel.Apply,
                    Difficulty: 0.5,
                    EstimatedTimeMinutes: 15,
                    LinkedDomainNodes: new[] { domainNodeId },
                    Tags: new[] { concept.Title, "example", "worked-example" },
                    SourceOrigin: ContentOrigin.AIGenerated,
                    QualityScore: 0.8,
                    CreatedAt: DateTimeOffset.UtcNow)
            };
        }
        catch
        {
            return Array.Empty<ContentNode>();
        }
    }

    private async Task<IReadOnlyList<ContentNode>> GenerateClinicalCasesAsync(
        CoreConcept concept,
        Guid domainNodeId,
        CancellationToken ct)
    {
        var prompt = $@"Generate a clinical case study for '{concept.Title}':

Topic: {concept.Description}
Learning Points: {string.Join(", ", concept.KeyPoints.Take(3))}

Create a realistic patient case that:
1. Presents relevant clinical information
2. Requires application of the concept
3. Includes decision points
4. Has learning objectives

Format as a structured case with patient presentation, history, assessment, and questions.";

        try
        {
            var response = await _gemini.GenerateTextAsync(prompt, ct);
            return new[]
            {
                new ContentNode(
                    Id: Guid.NewGuid(),
                    Type: ContentNodeType.ClinicalCase,
                    Title: $"{concept.Title} - Clinical Case",
                    Content: response,
                    Modality: ContentModality.Text,
                    BloomsLevel: BloomsLevel.Analyze,
                    Difficulty: 0.7,
                    EstimatedTimeMinutes: 20,
                    LinkedDomainNodes: new[] { domainNodeId },
                    Tags: new[] { concept.Title, "case-study", "clinical" },
                    SourceOrigin: ContentOrigin.AIGenerated,
                    QualityScore: 0.85,
                    CreatedAt: DateTimeOffset.UtcNow)
            };
        }
        catch
        {
            return Array.Empty<ContentNode>();
        }
    }

    private async Task<IReadOnlyList<ContentNode>> GenerateAssessmentQuestionsAsync(
        CoreConcept concept,
        Guid domainNodeId,
        CancellationToken ct)
    {
        var questions = new List<ContentNode>();
        var bloomsLevels = new[] { BloomsLevel.Remember, BloomsLevel.Understand, BloomsLevel.Apply, BloomsLevel.Analyze };

        foreach (var level in bloomsLevels)
        {
            var prompt = $@"Generate an assessment question for '{concept.Title}' at Bloom's level: {level}

Context: {concept.Description}

Create a question that tests {level} level understanding:
- Remember: Recall facts and definitions
- Understand: Explain concepts in own words
- Apply: Use knowledge in new situations
- Analyze: Break down and examine relationships

Include the expected answer and grading criteria.";

            try
            {
                var response = await _gemini.GenerateTextAsync(prompt, ct);
                questions.Add(new ContentNode(
                    Id: Guid.NewGuid(),
                    Type: ContentNodeType.Question,
                    Title: $"{concept.Title} - {level} Question",
                    Content: response,
                    Modality: ContentModality.Text,
                    BloomsLevel: level,
                    Difficulty: (int)level * 0.2,
                    EstimatedTimeMinutes: 5,
                    LinkedDomainNodes: new[] { domainNodeId },
                    Tags: new[] { concept.Title, "assessment", level.ToString().ToLower() },
                    SourceOrigin: ContentOrigin.AIGenerated,
                    QualityScore: 0.8,
                    CreatedAt: DateTimeOffset.UtcNow));
            }
            catch
            {
                // Skip on failure
            }
        }

        return questions;
    }

    private async Task<IReadOnlyList<ContentNode>> GenerateMnemonicsAsync(
        CoreConcept concept,
        Guid domainNodeId,
        CancellationToken ct)
    {
        var prompt = $@"Generate a memorable mnemonic for '{concept.Title}':

Key Points to Remember:
{string.Join("\n", concept.KeyPoints.Take(5))}

Create:
1. An acronym or acrostic
2. A visual association
3. A story or narrative hook

Make it memorable and easy to recall.";

        try
        {
            var response = await _gemini.GenerateTextAsync(prompt, ct);
            return new[]
            {
                new ContentNode(
                    Id: Guid.NewGuid(),
                    Type: ContentNodeType.Mnemonic,
                    Title: $"{concept.Title} - Memory Aid",
                    Content: response,
                    Modality: ContentModality.Text,
                    BloomsLevel: BloomsLevel.Remember,
                    Difficulty: 0.2,
                    EstimatedTimeMinutes: 3,
                    LinkedDomainNodes: new[] { domainNodeId },
                    Tags: new[] { concept.Title, "mnemonic", "memory-aid" },
                    SourceOrigin: ContentOrigin.AIGenerated,
                    QualityScore: 0.75,
                    CreatedAt: DateTimeOffset.UtcNow)
            };
        }
        catch
        {
            return Array.Empty<ContentNode>();
        }
    }

    private async Task<IReadOnlyList<ContentNode>> ExpandWithWebSearchAsync(
        CoreConcept concept,
        Guid domainNodeId,
        ExpansionConfig config,
        CancellationToken ct)
    {
        if (_webSearch == null)
            return Array.Empty<ContentNode>();

        var contentNodes = new List<ContentNode>();

        try
        {
            // Search for authoritative sources
            var searchQueries = new[]
            {
                $"{concept.Title} pharmacy education",
                $"{concept.Title} clinical guidelines",
                $"{concept.Title} best practices"
            };

            var results = await _webSearch.SearchAsync(searchQueries, ct);

            foreach (var result in results.Take(config.MaxSearchResults))
            {
                contentNodes.Add(new ContentNode(
                    Id: Guid.NewGuid(),
                    Type: ContentNodeType.CrossReference,
                    Title: result.Title,
                    Content: result.Snippet,
                    Modality: ContentModality.Text,
                    BloomsLevel: BloomsLevel.Understand,
                    Difficulty: 0.5,
                    EstimatedTimeMinutes: 10,
                    LinkedDomainNodes: new[] { domainNodeId },
                    Tags: new[] { concept.Title, "external", result.Source ?? "web" },
                    SourceOrigin: ContentOrigin.WebSearch,
                    QualityScore: result.Relevance,
                    CreatedAt: DateTimeOffset.UtcNow,
                    SourceUrl: result.Url));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Web search failed for concept '{Title}'", concept.Title);
        }

        return contentNodes;
    }

    private Task CreateContentConnectionsAsync(AIEnhancedContentGraph graph, CancellationToken ct)
    {
        // Create edges between related content nodes
        var nodes = graph.Nodes.Values.ToList();

        foreach (var node in nodes)
        {
            // Find related nodes by shared domain nodes
            var relatedNodes = nodes
                .Where(n => n.Id != node.Id &&
                           n.LinkedDomainNodes.Any(d => node.LinkedDomainNodes.Contains(d)))
                .Take(5);

            foreach (var related in relatedNodes)
            {
                var edgeType = DetermineEdgeType(node, related);
                var edge = new ContentEdge(
                    Id: Guid.NewGuid(),
                    FromNodeId: node.Id,
                    ToNodeId: related.Id,
                    Type: edgeType,
                    Strength: CalculateEdgeStrength(node, related));

                try
                {
                    graph.AddEdge(edge);
                }
                catch
                {
                    // Edge may already exist
                }
            }
        }

        return Task.CompletedTask;
    }

    private ContentEdgeType DetermineEdgeType(ContentNode from, ContentNode to)
    {
        if (from.Type == ContentNodeType.Explanation && to.Type == ContentNodeType.Question)
            return ContentEdgeType.AssessesUnderstandingOf;

        if (from.Difficulty < to.Difficulty)
            return ContentEdgeType.PrerequisiteOf;

        if (from.Modality != to.Modality && from.Type == to.Type)
            return ContentEdgeType.AlternativeTo;

        return ContentEdgeType.RelatedTo;
    }

    private double CalculateEdgeStrength(ContentNode from, ContentNode to)
    {
        var sharedDomains = from.LinkedDomainNodes
            .Intersect(to.LinkedDomainNodes)
            .Count();

        var sharedTags = from.Tags.Intersect(to.Tags).Count();

        return Math.Min(1.0, (sharedDomains * 0.3) + (sharedTags * 0.1) + 0.2);
    }
}
