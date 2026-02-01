using CatAdaptive.Domain.Models;
using FluentAssertions;

namespace CatAdaptive.Domain.Tests;

public sealed class ItemTemplateTests
{
    [Fact]
    public void Create_ThrowsWhenStemIsEmpty()
    {
        var act = () => ItemTemplate.Create(
            stem: " ",
            choices: new[] { ItemChoice.Create("A", true) },
            format: ItemFormat.MultipleChoice,
            parameter: new ItemParameter(0),
            knowledgeUnitIds: new[] { Guid.NewGuid() },
            topic: "T",
            subtopic: "S",
            explanation: "E");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ThrowsForMultipleChoiceWithoutChoices()
    {
        var act = () => ItemTemplate.Create(
            stem: "Stem",
            choices: Array.Empty<ItemChoice>(),
            format: ItemFormat.MultipleChoice,
            parameter: new ItemParameter(0),
            knowledgeUnitIds: new[] { Guid.NewGuid() },
            topic: "T",
            subtopic: "S",
            explanation: "E");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_TrimsFieldsAndSetsDefaults()
    {
        var item = ItemTemplate.Create(
            stem: "  Stem  ",
            choices: new[] { ItemChoice.Create("A", true), ItemChoice.Create("B", false) },
            format: ItemFormat.MultipleChoice,
            parameter: new ItemParameter(0),
            knowledgeUnitIds: Array.Empty<Guid>(),
            topic: "  Topic ",
            subtopic: "  Sub ",
            explanation: "  Explain ");

        item.Stem.Should().Be("Stem");
        item.Topic.Should().Be("Topic");
        item.Subtopic.Should().Be("Sub");
        item.Explanation.Should().Be("Explain");
        item.BloomLevel.Should().Be("Apply");
    }
}
