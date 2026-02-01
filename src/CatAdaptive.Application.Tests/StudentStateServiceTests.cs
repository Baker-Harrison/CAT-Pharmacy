using CatAdaptive.Application.Abstractions;
using CatAdaptive.Application.Services;
using CatAdaptive.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CatAdaptive.Application.Tests;

public sealed class StudentStateServiceTests
{
    [Fact]
    public async Task GetOrCreateStudentStateAsync_CreatesAndInitializesFromDomainGraph()
    {
        var studentId = Guid.NewGuid();
        var savedState = (StudentStateModel?)null;

        var repository = new Mock<IStudentStateRepository>();
        repository.Setup(r => r.GetByStudentAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => savedState);
        repository.Setup(r => r.SaveAsync(It.IsAny<StudentStateModel>(), It.IsAny<CancellationToken>()))
            .Callback<StudentStateModel, CancellationToken>((state, _) => savedState = state)
            .Returns(Task.CompletedTask);

        var domainGraph = BuildDomainGraph(out var nodeIds);
        var graphRepository = new Mock<IDomainGraphRepository>();
        graphRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(domainGraph);

        var gemini = new Mock<IGeminiService>();
        var logger = Mock.Of<ILogger<StudentStateService>>();

        var service = new StudentStateService(repository.Object, graphRepository.Object, gemini.Object, logger);

        var state = await service.GetOrCreateStudentStateAsync(studentId);

        state.StudentId.Should().Be(studentId);
        state.KnowledgeMasteries.Keys.Should().Contain(nodeIds);
        repository.Verify(r => r.SaveAsync(It.IsAny<StudentStateModel>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMasteryAsync_UpdatesStateAndPersists()
    {
        var studentId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var savedState = new StudentStateModel(studentId);
        savedState.InitializeMasteryForNodes(new[] { nodeId });

        var repository = new Mock<IStudentStateRepository>();
        repository.Setup(r => r.GetByStudentAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => savedState);
        repository.Setup(r => r.SaveAsync(It.IsAny<StudentStateModel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var domainGraph = BuildDomainGraph(nodeId);
        var graphRepository = new Mock<IDomainGraphRepository>();
        graphRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(domainGraph);

        var gemini = new Mock<IGeminiService>();
        var logger = Mock.Of<ILogger<StudentStateService>>();

        var service = new StudentStateService(repository.Object, graphRepository.Object, gemini.Object, logger);

        var masteryEvent = new MasteryEvent(
            NodeId: nodeId,
            Type: MasteryEventType.PracticeAttempt,
            Strength: 0.8,
            Evidence: "response",
            Context: new AssessmentContext(0.4, BloomsLevel.Apply, "quiz", 1),
            Timestamp: DateTimeOffset.UtcNow,
            Confidence: 0.9);

        var updated = await service.UpdateMasteryAsync(studentId, masteryEvent);

        updated.GetKnowledgeMastery(nodeId).PracticeAttempts.Should().Be(1);
        updated.GetKnowledgeMastery(nodeId).Level.Should().NotBe(MasteryLevel.Unknown);
        repository.Verify(r => r.SaveAsync(It.IsAny<StudentStateModel>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeKnowledgeStateAsync_IdentifiesGapsAndRecommendations()
    {
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();
        var graph = BuildDomainGraph(nodeA, nodeB, withPrerequisite: true);

        var state = new StudentStateModel(Guid.NewGuid());
        state.UpdateKnowledgeMastery(new KnowledgeMastery(
            nodeA,
            MasteryLevel.Novice,
            0.2,
            2,
            DateTimeOffset.UtcNow.AddDays(-10),
            Array.Empty<RetrievalEvent>(),
            Array.Empty<KnowledgeGap>(),
            0.4,
            EvidenceVector.Empty));

        state.UpdateKnowledgeMastery(new KnowledgeMastery(
            nodeB,
            MasteryLevel.Proficient,
            0.8,
            1,
            DateTimeOffset.UtcNow.AddDays(-1),
            Array.Empty<RetrievalEvent>(),
            Array.Empty<KnowledgeGap>(),
            0.9,
            EvidenceVector.Empty));

        var repository = new Mock<IStudentStateRepository>();
        var graphRepository = new Mock<IDomainGraphRepository>();
        graphRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(graph);

        var gemini = new Mock<IGeminiService>();
        var logger = Mock.Of<ILogger<StudentStateService>>();

        var service = new StudentStateService(repository.Object, graphRepository.Object, gemini.Object, logger);

        var analysis = await service.AnalyzeKnowledgeStateAsync(state);

        analysis.CriticalGaps.Should().ContainSingle(g => g.GapNodeId == nodeA);
        analysis.RecommendedNextNodes.Should().Contain(nodeA);
        analysis.OverallMasteryScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RecordSessionAsync_UpdatesEngagementMetrics()
    {
        var studentId = Guid.NewGuid();
        var state = new StudentStateModel(studentId);
        var savedState = state;

        var repository = new Mock<IStudentStateRepository>();
        repository.Setup(r => r.GetByStudentAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => savedState);
        repository.Setup(r => r.SaveAsync(It.IsAny<StudentStateModel>(), It.IsAny<CancellationToken>()))
            .Callback<StudentStateModel, CancellationToken>((model, _) => savedState = model)
            .Returns(Task.CompletedTask);

        var graphRepository = new Mock<IDomainGraphRepository>();
        var gemini = new Mock<IGeminiService>();
        var logger = Mock.Of<ILogger<StudentStateService>>();

        var service = new StudentStateService(repository.Object, graphRepository.Object, gemini.Object, logger);

        await service.RecordSessionAsync(studentId, TimeSpan.FromMinutes(30), 0.7);

        savedState.Engagement.TotalSessions.Should().Be(1);
        savedState.Engagement.TotalTimeSpent.Should().Be(TimeSpan.FromMinutes(30));
        savedState.Engagement.AverageConfidence.Should().BeApproximately(0.7, 0.0001);
    }

    private static DomainKnowledgeGraph BuildDomainGraph(out IReadOnlyList<Guid> nodeIds)
    {
        var graph = new DomainKnowledgeGraph();
        var nodes = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var node = new DomainNode(
                Guid.NewGuid(),
                $"Node {i}",
                "Desc",
                DomainNodeType.Concept,
                BloomsLevel.Understand,
                0.3 + i,
                0.5,
                new[] { "tag" });
            graph.AddNode(node);
            nodes.Add(node.Id);
        }

        nodeIds = nodes;
        return graph;
    }

    private static DomainKnowledgeGraph BuildDomainGraph(Guid nodeA, Guid nodeB, bool withPrerequisite = false)
    {
        var graph = new DomainKnowledgeGraph();
        var node1 = new DomainNode(nodeA, "A", "Desc", DomainNodeType.Concept, BloomsLevel.Understand, 0.4, 0.8, new[] { "a" });
        var node2 = new DomainNode(nodeB, "B", "Desc", DomainNodeType.Concept, BloomsLevel.Understand, 0.6, 0.6, new[] { "b" });
        graph.AddNode(node1);
        graph.AddNode(node2);

        if (withPrerequisite)
        {
            graph.AddEdge(new DomainEdge(Guid.NewGuid(), nodeA, nodeB, DomainEdgeType.PrerequisiteOf));
        }

        return graph;
    }

    private static DomainKnowledgeGraph BuildDomainGraph(Guid nodeId)
    {
        var graph = new DomainKnowledgeGraph();
        graph.AddNode(new DomainNode(nodeId, "Node", "Desc", DomainNodeType.Concept, BloomsLevel.Understand, 0.5, 0.7, new[] { "tag" }));
        return graph;
    }
}
