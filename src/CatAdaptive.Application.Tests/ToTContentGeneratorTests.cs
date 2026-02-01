using CatAdaptive.Application.Abstractions;
using CatAdaptive.Application.Services;
using CatAdaptive.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CatAdaptive.Application.Tests;

public sealed class ToTContentGeneratorTests
{
    [Fact]
    public async Task GenerateWithToTAsync_UsesThoughtPathsAndReturnsContent()
    {
        var gemini = new Mock<IGeminiService>();
        gemini.Setup(g => g.GenerateThoughtPathsAsync(It.IsAny<string>(), 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "path1", "path2", "path3", "path4" });
        gemini.Setup(g => g.SelectBestPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("best path");
        gemini.Setup(g => g.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("final content");

        var logger = Mock.Of<ILogger<ToTContentGenerator>>();
        var generator = new ToTContentGenerator(gemini.Object, logger);

        var state = BuildState();
        var request = new ContentGenerationRequest(Guid.NewGuid(), "Objective", "explanation", BloomsLevel.Understand);

        var content = await generator.GenerateWithToTAsync(request, state);

        content.Content.Should().Be("final content");
        content.PersonalizationRationale.Should().Be("best path");
        content.EstimatedTimeMinutes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateWithToTAsync_FallsBackOnException()
    {
        var gemini = new Mock<IGeminiService>();
        gemini.Setup(g => g.GenerateThoughtPathsAsync(It.IsAny<string>(), 4, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        gemini.Setup(g => g.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("fallback content");

        var logger = Mock.Of<ILogger<ToTContentGenerator>>();
        var generator = new ToTContentGenerator(gemini.Object, logger);

        var state = BuildState();
        var request = new ContentGenerationRequest(Guid.NewGuid(), "Objective", "explanation", BloomsLevel.Understand);

        var content = await generator.GenerateWithToTAsync(request, state);

        content.Content.Should().Be("fallback content");
        content.PersonalizationRationale.Should().Be("Fallback generation used");
    }

    private static StudentStateModel BuildState()
    {
        var state = new StudentStateModel(Guid.NewGuid());
        state.CurrentAnalysis = new KnowledgeAnalysis(
            Array.Empty<KnowledgeGap>(),
            Array.Empty<Guid>(),
            Array.Empty<Guid>(),
            0.6,
            Array.Empty<Guid>());
        return state;
    }
}
