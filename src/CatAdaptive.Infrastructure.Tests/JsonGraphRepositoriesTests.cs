using CatAdaptive.Domain.Models;
using CatAdaptive.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CatAdaptive.Infrastructure.Tests;

public sealed class JsonGraphRepositoriesTests : IDisposable
{
    private readonly string _dataDirectory;

    public JsonGraphRepositoriesTests()
    {
        _dataDirectory = Path.Combine(Path.GetTempPath(), "catadaptive-graph-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_dataDirectory);
    }

    [Fact]
    public async Task JsonAIContentGraphRepository_SavesAndLoadsByTopic()
    {
        var repository = new JsonAIContentGraphRepository(_dataDirectory, Mock.Of<ILogger<JsonAIContentGraphRepository>>());
        var graph = new AIEnhancedContentGraph();
        var nodeId = Guid.NewGuid();
        graph.AddNode(new ContentNode(
            nodeId,
            ContentNodeType.Explanation,
            "Title",
            "Content",
            ContentModality.Text,
            BloomsLevel.Understand,
            0.4,
            5,
            new[] { Guid.NewGuid() },
            new[] { "tag" },
            ContentOrigin.Slides,
            0.9,
            DateTimeOffset.UtcNow));

        await repository.SaveAsync("Topic One", graph);
        var loaded = await repository.GetByTopicAsync("Topic One");

        loaded.Should().NotBeNull();
        loaded!.Nodes.Should().ContainKey(nodeId);
    }

    [Fact]
    public async Task JsonLearningObjectiveMapRepository_SavesAndLoadsDefault()
    {
        var repository = new JsonLearningObjectiveMapRepository(_dataDirectory, Mock.Of<ILogger<JsonLearningObjectiveMapRepository>>());
        var map = new LearningObjectiveMap();
        var objective = new LearningObjective(Guid.NewGuid(), Guid.NewGuid(), "Objective", BloomsLevel.Remember, "Topic", new[] { "tag" });
        map.AddObjective(objective);

        await repository.SaveDefaultAsync(map);
        var loaded = await repository.GetDefaultAsync();

        loaded.Should().NotBeNull();
        loaded!.Objectives.Should().ContainKey(objective.Id);
    }

    [Fact]
    public async Task JsonDomainGraphRepository_SavesAndLoadsGraph()
    {
        var repository = new JsonDomainGraphRepository(_dataDirectory, Mock.Of<ILogger<JsonDomainGraphRepository>>());
        var graph = new DomainKnowledgeGraph();
        var node = new DomainNode(Guid.NewGuid(), "Node", "Desc", DomainNodeType.Concept, BloomsLevel.Understand, 0.4, 0.7, new[] { "tag" });
        graph.AddNode(node);

        await repository.SaveAsync(graph);
        var loaded = await repository.GetAsync();

        loaded.Should().NotBeNull();
        loaded!.Nodes.Should().ContainKey(node.Id);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }
}
