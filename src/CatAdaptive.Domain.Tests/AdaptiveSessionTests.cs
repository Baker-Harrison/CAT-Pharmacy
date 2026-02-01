using CatAdaptive.Domain.Aggregates;
using CatAdaptive.Domain.Models;
using FluentAssertions;

namespace CatAdaptive.Domain.Tests;

public sealed class AdaptiveSessionTests
{
    [Fact]
    public void AdvanceToNextItem_SelectsHighestInformationItem()
    {
        var itemLow = BuildItem("Low", new ItemParameter(Difficulty: 0.0, Discrimination: 0.5));
        var itemHigh = BuildItem("High", new ItemParameter(Difficulty: 0.0, Discrimination: 2.0));
        var session = new AdaptiveSession(
            Guid.NewGuid(),
            new LearnerProfile(Guid.NewGuid(), "Test", 0.0),
            new[] { itemLow, itemHigh },
            TerminationCriteria.Default());

        var selected = session.AdvanceToNextItem();

        selected.Should().NotBeNull();
        selected!.Id.Should().Be(itemHigh.Id);
    }

    [Fact]
    public void RecordResponse_UpdatesAbilityAndCompletesWhenMaxItemsReached()
    {
        var item = BuildItem("Item", new ItemParameter(Difficulty: 0.0, Discrimination: 1.5));
        var session = new AdaptiveSession(
            Guid.NewGuid(),
            new LearnerProfile(Guid.NewGuid(), "Test", 0.0),
            new[] { item },
            new TerminationCriteria(TargetStandardError: 0.0, MaxItems: 1, MasteryTheta: null, MaxStallCount: 3));

        session.AdvanceToNextItem();
        var response = session.RecordResponse(isCorrect: true, TimeSpan.FromSeconds(10), "A");

        response.IsCorrect.Should().BeTrue();
        session.Responses.Should().HaveCount(1);
        session.ActiveItem.Should().BeNull();
        session.IsComplete.Should().BeTrue();
        session.CurrentAbility.Theta.Should().BeInRange(-3.0, 3.0);
    }

    [Fact]
    public void RecordResponse_ThrowsWithoutActiveItem()
    {
        var session = new AdaptiveSession(
            Guid.NewGuid(),
            new LearnerProfile(Guid.NewGuid(), "Test", 0.0),
            new[] { BuildItem("Item", new ItemParameter(0.0)) },
            TerminationCriteria.Default());

        var act = () => session.RecordResponse(true, TimeSpan.FromSeconds(1), "A");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RecordResponse_CompletesWhenMasteryThetaReached()
    {
        var item = BuildItem("Item", new ItemParameter(Difficulty: -2.5, Discrimination: 2.0));
        var session = new AdaptiveSession(
            Guid.NewGuid(),
            new LearnerProfile(Guid.NewGuid(), "Test", 0.0),
            new[] { item },
            new TerminationCriteria(TargetStandardError: 0.000001, MaxItems: 99, MasteryTheta: -3.0, MaxStallCount: 3));

        session.AdvanceToNextItem();
        session.RecordResponse(isCorrect: true, TimeSpan.FromSeconds(5), "A");

        session.IsComplete.Should().BeTrue();
    }

    private static ItemTemplate BuildItem(string title, ItemParameter parameter)
    {
        return ItemTemplate.Create(
            stem: $"Stem {title}",
            choices: new[]
            {
                ItemChoice.Create("A", true),
                ItemChoice.Create("B", false)
            },
            format: ItemFormat.MultipleChoice,
            parameter: parameter,
            knowledgeUnitIds: new[] { Guid.NewGuid() },
            topic: title,
            subtopic: "",
            explanation: "explanation");
    }
}
