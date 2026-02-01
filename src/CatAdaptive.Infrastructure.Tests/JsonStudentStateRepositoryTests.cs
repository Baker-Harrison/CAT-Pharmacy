using CatAdaptive.Domain.Models;
using CatAdaptive.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CatAdaptive.Infrastructure.Tests;

public sealed class JsonStudentStateRepositoryTests : IDisposable
{
    private readonly string _dataDirectory;

    public JsonStudentStateRepositoryTests()
    {
        _dataDirectory = Path.Combine(Path.GetTempPath(), "catadaptive-studentstate-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_dataDirectory);
    }

    [Fact]
    public async Task SaveAndGetByStudentAsync_RoundTripsState()
    {
        var repository = new JsonStudentStateRepository(_dataDirectory, Mock.Of<ILogger<JsonStudentStateRepository>>());
        var studentId = Guid.NewGuid();
        var state = new StudentStateModel(studentId);

        await repository.SaveAsync(state);

        var loaded = await repository.GetByStudentAsync(studentId);

        loaded.Should().NotBeNull();
        loaded!.StudentId.Should().Be(studentId);
    }

    [Fact]
    public async Task UpdateMasteryAsync_UpdatesExistingState()
    {
        var repository = new JsonStudentStateRepository(_dataDirectory, Mock.Of<ILogger<JsonStudentStateRepository>>());
        var studentId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var state = new StudentStateModel(studentId);
        await repository.SaveAsync(state);

        var mastery = new KnowledgeMastery(
            nodeId,
            MasteryLevel.Proficient,
            0.8,
            1,
            DateTimeOffset.UtcNow,
            Array.Empty<RetrievalEvent>(),
            Array.Empty<KnowledgeGap>(),
            0.9,
            EvidenceVector.Empty);

        await repository.UpdateMasteryAsync(studentId, nodeId, mastery);

        var loaded = await repository.GetByStudentAsync(studentId);
        loaded!.GetKnowledgeMastery(nodeId).Level.Should().Be(MasteryLevel.Proficient);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllSavedStates()
    {
        var repository = new JsonStudentStateRepository(_dataDirectory, Mock.Of<ILogger<JsonStudentStateRepository>>());
        await repository.SaveAsync(new StudentStateModel(Guid.NewGuid()));
        await repository.SaveAsync(new StudentStateModel(Guid.NewGuid()));

        var all = await repository.GetAllAsync();

        all.Should().HaveCount(2);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }
}
