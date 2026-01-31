using System.Collections.ObjectModel;
using CatAdaptive.Domain.Models;
using CatAdaptive.Infrastructure.Repositories;
using FluentAssertions;

namespace CatAdaptive.Infrastructure.Tests;

public sealed class JsonItemRepositoryTests : IDisposable
{
    private readonly string _testDataDirectory;
    private readonly JsonItemRepository _repository;

    public JsonItemRepositoryTests()
    {
        _testDataDirectory = Path.Combine(Path.GetTempPath(), $"CatAdaptiveTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDirectory);
        _repository = new JsonItemRepository(_testDataDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDirectory))
        {
            Directory.Delete(_testDataDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GetAllAsync_WithNoData_ReturnsEmptyList()
    {
        // Act
        var items = await _repository.GetAllAsync();

        // Assert
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_AddsItemToRepository()
    {
        // Arrange
        var item = ItemTemplate.Create(
            stem: "Test question stem?",
            choices: new[] { ItemChoice.Create("Option A", true), ItemChoice.Create("Option B", false) },
            format: ItemFormat.MultipleChoice,
            parameter: new ItemParameter(1.0, 0.0, 0.0),
            knowledgeUnitIds: new[] { Guid.NewGuid() },
            topic: "Pharmacology",
            subtopic: "Receptors",
            explanation: "This is the explanation");

        // Act
        await _repository.AddAsync(item);
        await _repository.SaveChangesAsync();

        // Assert
        var items = await _repository.GetAllAsync();
        items.Should().ContainSingle()
             .Which.Id.Should().Be(item.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingItem_ReturnsItem()
    {
        // Arrange
        var item = ItemTemplate.Create(
            stem: "Test question stem?",
            choices: new[] { ItemChoice.Create("Option A", true), ItemChoice.Create("Option B", false) },
            format: ItemFormat.MultipleChoice,
            parameter: new ItemParameter(1.0, 0.0, 0.0),
            knowledgeUnitIds: new[] { Guid.NewGuid() },
            topic: "Pharmacology",
            subtopic: "Receptors",
            explanation: "This is the explanation");

        await _repository.AddAsync(item);
        await _repository.SaveChangesAsync();

        // Act
        var retrieved = await _repository.GetByIdAsync(item.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(item.Id);
        retrieved.Stem.Should().Be("Test question stem?");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingItem_ReturnsNull()
    {
        // Arrange
        var itemId = Guid.NewGuid();

        // Act
        var retrieved = await _repository.GetByIdAsync(itemId);

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesItem()
    {
        // Arrange
        var item = ItemTemplate.Create(
            stem: "Test question stem?",
            choices: new[] { ItemChoice.Create("Option A", true), ItemChoice.Create("Option B", false) },
            format: ItemFormat.MultipleChoice,
            parameter: new ItemParameter(1.0, 0.0, 0.0),
            knowledgeUnitIds: new[] { Guid.NewGuid() },
            topic: "Pharmacology",
            subtopic: "Receptors",
            explanation: "This is the explanation");

        await _repository.AddAsync(item);
        await _repository.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(item.Id);
        await _repository.SaveChangesAsync();

        // Assert
        var retrieved = await _repository.GetByIdAsync(item.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingItem()
    {
        // Arrange
        var item = ItemTemplate.Create(
            stem: "Original stem?",
            choices: new[] { ItemChoice.Create("Option A", true), ItemChoice.Create("Option B", false) },
            format: ItemFormat.MultipleChoice,
            parameter: new ItemParameter(1.0, 0.0, 0.0),
            knowledgeUnitIds: new[] { Guid.NewGuid() },
            topic: "Pharmacology",
            subtopic: "Receptors",
            explanation: "This is the explanation");

        await _repository.AddAsync(item);
        await _repository.SaveChangesAsync();

        var updatedItem = item with { Stem = "Updated stem?" };

        // Act
        await _repository.UpdateAsync(updatedItem);
        await _repository.SaveChangesAsync();

        // Assert
        var retrieved = await _repository.GetByIdAsync(item.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Stem.Should().Be("Updated stem?");
    }

    [Fact]
    public async Task ReplaceAllAsync_ReplacesAllItems()
    {
        // Arrange
        var item1 = ItemTemplate.Create(
            stem: "First question?",
            choices: new[] { ItemChoice.Create("Option A", true), ItemChoice.Create("Option B", false) },
            format: ItemFormat.MultipleChoice,
            parameter: new ItemParameter(1.0, 0.0, 0.0),
            knowledgeUnitIds: new[] { Guid.NewGuid() },
            topic: "Pharmacology",
            subtopic: "Receptors",
            explanation: "Explanation 1");

        var item2 = ItemTemplate.Create(
            stem: "Second question?",
            choices: new[] { ItemChoice.Create("Option A", true), ItemChoice.Create("Option B", false) },
            format: ItemFormat.MultipleChoice,
            parameter: new ItemParameter(1.0, 0.0, 0.0),
            knowledgeUnitIds: new[] { Guid.NewGuid() },
            topic: "Pharmacology",
            subtopic: "Receptors",
            explanation: "Explanation 2");

        await _repository.AddAsync(item1);
        await _repository.SaveChangesAsync();

        var newItems = new List<ItemTemplate> { item2 };

        // Act
        await _repository.ReplaceAllAsync(newItems);
        await _repository.SaveChangesAsync();

        // Assert
        var items = await _repository.GetAllAsync();
        items.Should().ContainSingle()
             .Which.Id.Should().Be(item2.Id);
    }
}
