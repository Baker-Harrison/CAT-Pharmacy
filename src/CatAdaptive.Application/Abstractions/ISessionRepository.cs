using CatAdaptive.Domain.Aggregates;

namespace CatAdaptive.Application.Abstractions;

public interface ISessionRepository
{
    Task<AdaptiveSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(AdaptiveSession session, CancellationToken ct = default);
    Task<IReadOnlyList<AdaptiveSession>> GetByLearnerIdAsync(Guid learnerId, CancellationToken ct = default);
}
