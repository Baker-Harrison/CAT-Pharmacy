using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Infrastructure.Repositories;

/// <summary>
/// JSON-based repository for Learner Model persistence.
/// </summary>
public sealed class JsonLearnerModelRepository : ILearnerModelRepository
{
    private readonly string _dataDirectory;
    private readonly ILogger<JsonLearnerModelRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonLearnerModelRepository(ILogger<JsonLearnerModelRepository> logger)
    {
        _logger = logger;
        _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatAdaptive", "data");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        Directory.CreateDirectory(_dataDirectory);
    }

    public async Task<LearnerModel?> GetByLearnerAsync(Guid learnerId, CancellationToken ct = default)
    {
        try
        {
            var filePath = GetFilePath(learnerId);
            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath, ct);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var data = JsonSerializer.Deserialize<LearnerModelData>(json, _jsonOptions);
            if (data == null)
                return null;

            // Reconstruct the learner model
            var learnerModel = new LearnerModel(learnerId);
            
            // Add mastery states
            foreach (var mastery in data.Masteries)
            {
                // Use reflection to set the private field
                var field = typeof(LearnerModel).GetField("_masteryByNodeId", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var dict = field?.GetValue(learnerModel) as Dictionary<Guid, LearnerMastery>;
                if (dict != null)
                {
                    dict[mastery.NodeId] = mastery;
                }
            }

            _logger.LogInformation("Loaded Learner Model for learner {LearnerId} with {MasteryCount} masteries", 
                learnerId, data.Masteries.Count);

            return learnerModel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Learner Model for learner {LearnerId}", learnerId);
            return null;
        }
    }

    public async Task SaveAsync(LearnerModel learnerModel, CancellationToken ct = default)
    {
        try
        {
            var filePath = GetFilePath(learnerModel.LearnerId);
            var data = new LearnerModelData(
                learnerModel.LearnerId,
                learnerModel.Masteries.Values.ToList());

            var json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);

            _logger.LogInformation("Saved Learner Model for learner {LearnerId} with {MasteryCount} masteries", 
                learnerModel.LearnerId, data.Masteries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Learner Model for learner {LearnerId}", learnerModel.LearnerId);
            throw;
        }
    }

    public async Task<LearnerModel> CreateAsync(Guid learnerId, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating new Learner Model for learner {LearnerId}", learnerId);
        
        var learnerModel = new LearnerModel(learnerId);
        await SaveAsync(learnerModel, ct);
        
        return learnerModel;
    }

    public async Task UpdateMasteryAsync(Guid learnerId, Guid nodeId, RetrievalEvent retrievalEvent, CancellationToken ct = default)
    {
        var learnerModel = await GetByLearnerAsync(learnerId, ct) 
            ?? await CreateAsync(learnerId, ct);

        learnerModel.UpdateFromRetrieval(nodeId, retrievalEvent);
        await SaveAsync(learnerModel, ct);

        _logger.LogInformation("Updated mastery for node {NodeId} for learner {LearnerId}", nodeId, learnerId);
    }

    private string GetFilePath(Guid learnerId)
    {
        return Path.Combine(_dataDirectory, $"learner-model-{learnerId}.json");
    }

    private record LearnerModelData(
        Guid LearnerId,
        List<LearnerMastery> Masteries);
}
