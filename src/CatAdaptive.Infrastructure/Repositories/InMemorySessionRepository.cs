using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Aggregates;

namespace CatAdaptive.Infrastructure.Repositories;

public sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly Dictionary<Guid, AdaptiveSession> _sessions = new();

    public Task<AdaptiveSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _sessions.TryGetValue(id, out var session);
        return Task.FromResult(session);
    }

    public Task SaveAsync(AdaptiveSession session, CancellationToken ct = default)
    {
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AdaptiveSession>> GetByLearnerIdAsync(Guid learnerId, CancellationToken ct = default)
    {
        var sessions = _sessions.Values
            .Where(s => s.Learner.Id == learnerId)
            .ToList();
        return Task.FromResult<IReadOnlyList<AdaptiveSession>>(sessions);
    }
}
