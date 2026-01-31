using CatAdaptive.Domain.Models;
using FluentAssertions;

namespace CatAdaptive.Domain.Tests;

public sealed class KnowledgeUnitTests
{
    [Fact]
    public void Create_WithValidData_CreatesKnowledgeUnit()
    {
        // Arrange
        var topic = "Pharmacology";
        var subtopic = "Receptors";
        var sourceSlideId = "slide-001";
        var summary = "Drug-receptor interactions";
        var keyPoints = new[] { "Receptors are proteins", "Drugs bind to receptors" };
        var objectives = new[] { "Understand receptor types" };

        // Act
        var unit = KnowledgeUnit.Create(topic, subtopic, sourceSlideId, summary, keyPoints, objectives);

        // Assert
        unit.Should().NotBeNull();
        unit.Topic.Should().Be(topic);
        unit.Subtopic.Should().Be(subtopic);
        unit.SourceSlideId.Should().Be(sourceSlideId);
        unit.Summary.Should().Be(summary);
        unit.KeyPoints.Should().HaveCount(2);
        unit.LearningObjectives.Should().HaveCount(1);
        unit.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithEmptyTopic_UsesDefaultValue()
    {
        // Arrange
        var keyPoints = new[] { "Point 1" };

        // Act
        var unit = KnowledgeUnit.Create("", "", "slide-1", "", keyPoints);

        // Assert
        unit.Topic.Should().Be("General");
    }

    [Fact]
    public void Create_WithWhitespaceTopic_TrimAndUseDefault()
    {
        // Arrange
        var keyPoints = new[] { "Point 1" };

        // Act
        var unit = KnowledgeUnit.Create("   ", "", "slide-1", "", keyPoints);

        // Assert
        unit.Topic.Should().Be("General");
    }

    [Fact]
    public void Create_WithNullKeyPoints_CreatesEmptyList()
    {
        // Arrange
        IEnumerable<string>? keyPoints = null;

        // Act
        var unit = KnowledgeUnit.Create("Topic", "", "slide-1", "", keyPoints);

        // Assert
        unit.KeyPoints.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithNullObjectives_CreatesEmptyList()
    {
        // Arrange
        var keyPoints = new[] { "Point 1" };

        // Act
        var unit = KnowledgeUnit.Create("Topic", "", "slide-1", "", keyPoints, null);

        // Assert
        unit.LearningObjectives.Should().BeEmpty();
    }

    [Fact]
    public void Create_TrimsWhitespaceFromValues()
    {
        // Arrange
        var topic = "  Pharmacology  ";
        var summary = "  Summary text  ";
        var keyPoints = new[] { "  Point 1  " };

        // Act
        var unit = KnowledgeUnit.Create(topic, "", "slide-1", summary, keyPoints);

        // Assert
        unit.Topic.Should().Be("Pharmacology");
        unit.Summary.Should().Be("Summary text");
        unit.KeyPoints[0].Should().Be("Point 1");
    }

    [Fact]
    public void Create_FiltersEmptyKeyPoints()
    {
        // Arrange
        var keyPoints = new[] { "Point 1", "", "   ", "Point 2" };

        // Act
        var unit = KnowledgeUnit.Create("Topic", "", "slide-1", "", keyPoints);

        // Assert
        unit.KeyPoints.Should().HaveCount(2);
        unit.KeyPoints.Should().Contain("Point 1");
        unit.KeyPoints.Should().Contain("Point 2");
    }
}
