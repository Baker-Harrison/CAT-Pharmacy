using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

/// <summary>
/// Repository for Learner Model operations.
/// </summary>
public interface ILearnerModelRepository
{
    /// <summary>
    /// Gets the learner model for a specific learner.
    /// </summary>
    Task<LearnerModel?> GetByLearnerAsync(Guid learnerId, CancellationToken ct = default);

    /// <summary>
    /// Saves the learner model.
    /// </summary>
    Task SaveAsync(LearnerModel learnerModel, CancellationToken ct = default);

    /// <summary>
    /// Creates a new learner model.
    /// </summary>
    Task<LearnerModel> CreateAsync(Guid learnerId, CancellationToken ct = default);

    /// <summary>
    /// Updates mastery for a specific node.
    /// </summary>
    Task UpdateMasteryAsync(Guid learnerId, Guid nodeId, RetrievalEvent retrievalEvent, CancellationToken ct = default);
}
