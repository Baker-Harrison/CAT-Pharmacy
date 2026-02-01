using CatAdaptive.Application.Abstractions;
using CatAdaptive.Application.Services;
using CatAdaptive.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CatAdaptive.Application.Tests;

public sealed class AIContentExpansionServiceTests
{
    [Fact]
    public async Task ExpandContentGraph10X_GeneratesContentNodes()
    {
        var gemini = new Mock<IGeminiService>();
        gemini.Setup(g => g.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("generated");

        var graphRepository = new Mock<IAIContentGraphRepository>();
        var logger = Mock.Of<ILogger<AIContentExpansionService>>();

        var service = new AIContentExpansionService(gemini.Object, graphRepository.Object, logger);

        var unit = new KnowledgeUnit(
            Guid.NewGuid(),
            "Topic",
            "Sub",
            "slide-1",
            "Summary",
            new[] { "Point1", "Point2", "Point3" },
            new[] { "Obj1" });

        var graph = await service.ExpandContentGraph10XAsync(new[] { unit }, new ExpansionConfig(EnableWebSearch: false));

        graph.Nodes.Should().NotBeEmpty();
        graph.Nodes.Values.Should().Contain(n => n.Type == ContentNodeType.Explanation);
        graph.Nodes.Values.Should().Contain(n => n.Type == ContentNodeType.Question);
    }

    [Fact]
    public async Task ExpandContentGraph10X_IncludesWebSearchResultsWhenEnabled()
    {
        var gemini = new Mock<IGeminiService>();
        gemini.Setup(g => g.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("generated");

        var webSearch = new Mock<IWebSearchService>();
        webSearch.Setup(w => w.SearchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WebSearchResult("Title 1", "Snippet 1", "http://example.com/1", "source", 0.9),
                new WebSearchResult("Title 2", "Snippet 2", "http://example.com/2", "source", 0.8)
            });

        var graphRepository = new Mock<IAIContentGraphRepository>();
        var logger = Mock.Of<ILogger<AIContentExpansionService>>();

        var service = new AIContentExpansionService(gemini.Object, graphRepository.Object, logger, webSearch.Object);

        var unit = new KnowledgeUnit(
            Guid.NewGuid(),
            "Topic",
            "Sub",
            "slide-1",
            "Summary",
            new[] { "Point1", "Point2", "Point3" },
            new[] { "Obj1" });

        var graph = await service.ExpandContentGraph10XAsync(new[] { unit }, new ExpansionConfig(EnableWebSearch: true, MaxSearchResults: 1));

        graph.Nodes.Values.Should().Contain(n => n.Type == ContentNodeType.CrossReference && n.SourceOrigin == ContentOrigin.WebSearch);
    }
}
