using System.Collections.Concurrent;
using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Infrastructure.Repositories;

/// <summary>
/// JSON-based repository for student state models.
/// </summary>
public sealed class JsonStudentStateRepository : IStudentStateRepository
{
    private readonly string _dataDirectory;
    private readonly ILogger<JsonStudentStateRepository> _logger;
    private readonly ConcurrentDictionary<Guid, StudentStateModel> _cache = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonStudentStateRepository(string dataDirectory, ILogger<JsonStudentStateRepository> logger)
    {
        _dataDirectory = dataDirectory;
        _logger = logger;
        Directory.CreateDirectory(_dataDirectory);
    }

    public async Task<StudentStateModel?> GetByStudentAsync(Guid studentId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(studentId, out var cached))
            return cached;

        var filePath = GetFilePath(studentId);

        if (!File.Exists(filePath))
        {
            _logger.LogInformation("Student state not found for {StudentId}", studentId);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            var state = JsonSerializer.Deserialize<StudentStateModel>(json, JsonOptions);
            
            if (state != null)
            {
                _cache.TryAdd(studentId, state);
            }
            
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load student state for {StudentId}", studentId);
            return null;
        }
    }

    public async Task SaveAsync(StudentStateModel model, CancellationToken ct = default)
    {
        var filePath = GetFilePath(model.StudentId);

        try
        {
            var json = JsonSerializer.Serialize(model, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);
            
            _cache.AddOrUpdate(model.StudentId, model, (_, _) => model);
            
            _logger.LogInformation("Saved student state for {StudentId}", model.StudentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save student state for {StudentId}", model.StudentId);
            throw;
        }
    }

    public async Task UpdateMasteryAsync(
        Guid studentId, 
        Guid nodeId, 
        KnowledgeMastery mastery, 
        CancellationToken ct = default)
    {
        var state = await GetByStudentAsync(studentId, ct);
        
        if (state == null)
        {
            _logger.LogWarning("Cannot update mastery - student {StudentId} not found", studentId);
            return;
        }

        state.UpdateKnowledgeMastery(mastery);
        await SaveAsync(state, ct);
    }

    public async Task<IReadOnlyList<StudentStateModel>> GetAllAsync(CancellationToken ct = default)
    {
        var states = new List<StudentStateModel>();
        var pattern = "student-state-*.json";

        foreach (var file in Directory.GetFiles(_dataDirectory, pattern))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var state = JsonSerializer.Deserialize<StudentStateModel>(json, JsonOptions);
                
                if (state != null)
                {
                    states.Add(state);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load student state from {File}", file);
            }
        }

        return states;
    }

    private string GetFilePath(Guid studentId) =>
        Path.Combine(_dataDirectory, $"student-state-{studentId}.json");
}
