using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Infrastructure.Repositories;

/// <summary>
/// JSON-based repository for Adaptive Session persistence.
/// </summary>
public sealed class JsonAdaptiveSessionRepository : IAdaptiveSessionRepository
{
    private readonly string _dataDirectory;
    private readonly string _sessionsDirectory;
    private readonly ILogger<JsonAdaptiveSessionRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonAdaptiveSessionRepository(ILogger<JsonAdaptiveSessionRepository> logger)
    {
        _logger = logger;
        _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatAdaptive", "data");
        _sessionsDirectory = Path.Combine(_dataDirectory, "sessions");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        Directory.CreateDirectory(_sessionsDirectory);
    }

    public async Task<AdaptiveSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            var filePath = GetFilePath(sessionId);
            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath, ct);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var data = JsonSerializer.Deserialize<AdaptiveSessionData>(json, _jsonOptions);
            if (data == null)
                return null;

            return ReconstructSession(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Adaptive Session {SessionId}", sessionId);
            return null;
        }
    }

    public async Task<IReadOnlyList<AdaptiveSession>> GetActiveSessionsAsync(Guid learnerId, CancellationToken ct = default)
    {
        try
        {
            var sessions = new List<AdaptiveSession>();
            var learnerDir = Path.Combine(_sessionsDirectory, learnerId.ToString());
            
            if (!Directory.Exists(learnerDir))
                return sessions;

            var files = Directory.GetFiles(learnerDir, "*.json");
            
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var data = JsonSerializer.Deserialize<AdaptiveSessionData>(json, _jsonOptions);
                    if (data != null && data.State != AdaptiveSessionState.Completed)
                    {
                        var session = ReconstructSession(data);
                        if (session != null)
                            sessions.Add(session);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load session from file {File}", file);
                }
            }

            return sessions.OrderByDescending(s => s.StartedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active sessions for learner {LearnerId}", learnerId);
            return Array.Empty<AdaptiveSession>();
        }
    }

    public async Task<IReadOnlyList<AdaptiveSession>> GetCompletedSessionsAsync(Guid learnerId, int limit = 50, CancellationToken ct = default)
    {
        try
        {
            var sessions = new List<AdaptiveSession>();
            var learnerDir = Path.Combine(_sessionsDirectory, learnerId.ToString());
            
            if (!Directory.Exists(learnerDir))
                return sessions;

            var files = Directory.GetFiles(learnerDir, "*.json");
            
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var data = JsonSerializer.Deserialize<AdaptiveSessionData>(json, _jsonOptions);
                    if (data != null && data.State == AdaptiveSessionState.Completed)
                    {
                        var session = ReconstructSession(data);
                        if (session != null)
                            sessions.Add(session);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load session from file {File}", file);
                }
            }

            return sessions.OrderByDescending(s => s.CompletedAt ?? s.StartedAt).Take(limit).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get completed sessions for learner {LearnerId}", learnerId);
            return Array.Empty<AdaptiveSession>();
        }
    }

    public async Task SaveAsync(AdaptiveSession session, CancellationToken ct = default)
    {
        try
        {
            var filePath = GetFilePath(session.Id, session.LearnerId);
            var data = SerializeSession(session);
            
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);

            _logger.LogInformation("Saved Adaptive Session {SessionId} for learner {LearnerId}", 
                session.Id, session.LearnerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Adaptive Session {SessionId}", session.Id);
            throw;
        }
    }

    public async Task<AdaptiveSession> CreateAsync(Guid learnerId, CancellationToken ct = default)
    {
        var session = new AdaptiveSession(learnerId);
        await SaveAsync(session, ct);
        
        _logger.LogInformation("Created new Adaptive Session {SessionId} for learner {LearnerId}", 
            session.Id, learnerId);
        
        return session;
    }

    public async Task<AdaptiveSession?> GetMostRecentAsync(Guid learnerId, CancellationToken ct = default)
    {
        var sessions = await GetActiveSessionsAsync(learnerId, ct);
        if (sessions.Count > 0)
            return sessions.First();

        // If no active sessions, get the most recent completed
        var completed = await GetCompletedSessionsAsync(learnerId, 1, ct);
        return completed.FirstOrDefault();
    }

    private string GetFilePath(Guid sessionId, Guid? learnerId = null)
    {
        if (learnerId.HasValue)
        {
            var learnerDir = Path.Combine(_sessionsDirectory, learnerId.Value.ToString());
            Directory.CreateDirectory(learnerDir);
            return Path.Combine(learnerDir, $"{sessionId}.json");
        }
        
        return Path.Combine(_sessionsDirectory, $"{sessionId}.json");
    }

    private AdaptiveSessionData SerializeSession(AdaptiveSession session)
    {
        return new AdaptiveSessionData(
            session.Id,
            session.LearnerId,
            session.State,
            session.StartedAt,
            session.CompletedAt,
            session.TargetNode,
            session.PreQuiz,
            session.Lesson,
            session.Result,
            session.RecommendedNextNodes.ToList());
    }

    private AdaptiveSession? ReconstructSession(AdaptiveSessionData data)
    {
        try
        {
            var session = new AdaptiveSession(data.LearnerId);
            
            // Use reflection to set private fields
            var idField = typeof(AdaptiveSession).GetField("<Id>k__BackingField", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            idField?.SetValue(session, data.Id);

            var stateField = typeof(AdaptiveSession).GetField("_state", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            stateField?.SetValue(session, data.State);

            var startedAtField = typeof(AdaptiveSession).GetField("<StartedAt>k__BackingField", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            startedAtField?.SetValue(session, data.StartedAt);

            var completedAtField = typeof(AdaptiveSession).GetField("<CompletedAt>k__BackingField", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            completedAtField?.SetValue(session, data.CompletedAt);

            // Set complex properties through methods
            if (data.TargetNode != null)
            {
                var startMethod = typeof(AdaptiveSession).GetMethod("StartWithTarget");
                startMethod?.Invoke(session, new[] { data.TargetNode });
            }

            if (data.PreQuiz != null)
            {
                var setPreQuizMethod = typeof(AdaptiveSession).GetMethod("SetPreQuiz");
                setPreQuizMethod?.Invoke(session, new[] { data.PreQuiz });
            }

            if (data.Lesson != null)
            {
                var setLessonMethod = typeof(AdaptiveSession).GetMethod("SetLesson");
                setLessonMethod?.Invoke(session, new[] { data.Lesson });
            }

            if (data.Result != null)
            {
                var completeMethod = typeof(AdaptiveSession).GetMethod("CompleteWithResult");
                completeMethod?.Invoke(session, new[] { data.Result });
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconstruct session {SessionId}", data.Id);
            return null;
        }
    }

    private record AdaptiveSessionData(
        Guid Id,
        Guid LearnerId,
        AdaptiveSessionState State,
        DateTimeOffset StartedAt,
        DateTimeOffset? CompletedAt,
        TargetNode? TargetNode,
        DiagnosticPreQuiz? PreQuiz,
        AdaptiveLesson? Lesson,
        AdaptiveSessionResult? Result,
        List<Guid> RecommendedNextNodes);
}
