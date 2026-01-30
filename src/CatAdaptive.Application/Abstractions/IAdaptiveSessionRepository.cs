using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

/// <summary>
/// Repository for Adaptive Session operations.
/// </summary>
public interface IAdaptiveSessionRepository
{
    /// <summary>
    /// Gets an adaptive session by ID.
    /// </summary>
    Task<AdaptiveSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Gets active sessions for a learner.
    /// </summary>
    Task<IReadOnlyList<AdaptiveSession>> GetActiveSessionsAsync(Guid learnerId, CancellationToken ct = default);

    /// <summary>
    /// Gets completed sessions for a learner.
    /// </summary>
    Task<IReadOnlyList<AdaptiveSession>> GetCompletedSessionsAsync(Guid learnerId, int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// Saves an adaptive session.
    /// </summary>
    Task SaveAsync(AdaptiveSession session, CancellationToken ct = default);

    /// <summary>
    /// Creates a new adaptive session.
    /// </summary>
    Task<AdaptiveSession> CreateAsync(Guid learnerId, CancellationToken ct = default);

    /// <summary>
    /// Gets the most recent session for a learner.
    /// </summary>
    Task<AdaptiveSession?> GetMostRecentAsync(Guid learnerId, CancellationToken ct = default);
}
