using CatAdaptive.Domain.Models;
using FluentAssertions;

namespace CatAdaptive.Domain.Tests;

public sealed class StudentStateModelTests
{
    [Fact]
    public void GetKnowledgeMastery_ReturnsNewWhenMissing()
    {
        var state = new StudentStateModel(Guid.NewGuid());
        var nodeId = Guid.NewGuid();

        var mastery = state.GetKnowledgeMastery(nodeId);

        mastery.DomainNodeId.Should().Be(nodeId);
        mastery.Level.Should().Be(MasteryLevel.Unknown);
    }

    [Fact]
    public void UpdateKnowledgeMastery_StoresAndUpdatesLastUpdated()
    {
        var state = new StudentStateModel(Guid.NewGuid());
        var nodeId = Guid.NewGuid();
        var before = state.LastUpdated;

        var mastery = new KnowledgeMastery(
            nodeId,
            MasteryLevel.Proficient,
            0.8,
            2,
            DateTimeOffset.UtcNow,
            Array.Empty<RetrievalEvent>(),
            Array.Empty<KnowledgeGap>(),
            0.9,
            EvidenceVector.Empty);

        state.UpdateKnowledgeMastery(mastery);

        state.GetKnowledgeMastery(nodeId).Level.Should().Be(MasteryLevel.Proficient);
        state.LastUpdated.Should().BeAfter(before);
    }

    [Fact]
    public void InitializeMasteryForNodes_AddsMissingNodesOnly()
    {
        var state = new StudentStateModel(Guid.NewGuid());
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();

        state.UpdateKnowledgeMastery(KnowledgeMastery.CreateNew(nodeA));
        state.InitializeMasteryForNodes(new[] { nodeA, nodeB });

        state.KnowledgeMasteries.Should().ContainKeys(nodeA, nodeB);
        state.KnowledgeMasteries[nodeA].DomainNodeId.Should().Be(nodeA);
    }

    [Fact]
    public void GetRecentEvents_ReturnsMostRecentAcrossNodes()
    {
        var state = new StudentStateModel(Guid.NewGuid());
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();

        var older = new RetrievalEvent(DateTimeOffset.UtcNow.AddDays(-2), true, null, 0.7, 100, "A");
        var newer = new RetrievalEvent(DateTimeOffset.UtcNow.AddDays(-1), false, GapType.Conceptual, 0.4, 120, "B");

        state.UpdateKnowledgeMastery(new KnowledgeMastery(
            nodeA,
            MasteryLevel.Developing,
            0.5,
            1,
            DateTimeOffset.UtcNow,
            new[] { older },
            Array.Empty<KnowledgeGap>(),
            0.6,
            EvidenceVector.Empty));

        state.UpdateKnowledgeMastery(new KnowledgeMastery(
            nodeB,
            MasteryLevel.Novice,
            0.2,
            1,
            DateTimeOffset.UtcNow,
            new[] { newer },
            Array.Empty<KnowledgeGap>(),
            0.4,
            EvidenceVector.Empty));

        var recent = state.GetRecentEvents(1).ToList();

        recent.Should().HaveCount(1);
        recent[0].Timestamp.Should().Be(newer.Timestamp);
    }

    [Fact]
    public void EvidenceVector_OverallScore_AccountsForMisconceptionPenalty()
    {
        var evidence = new EvidenceVector(
            ConceptualUnderstanding: 0.8,
            ProceduralSkill: 0.6,
            ApplicationAbility: 0.4,
            TransferCapability: 0.2,
            MisconceptionIndex: 0.5,
            ResponseLatency: 0,
            ErrorPattern: 0,
            ImprovementRate: 0);

        var overall = evidence.OverallScore;

        overall.Should().BeApproximately(((0.8 + 0.6 + 0.4 + 0.2) / 4.0) - 0.1, 0.0001);
    }
}
