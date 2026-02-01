using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

/// <summary>
/// Repository for student state models.
/// </summary>
public interface IStudentStateRepository
{
    Task<StudentStateModel?> GetByStudentAsync(Guid studentId, CancellationToken ct = default);
    Task SaveAsync(StudentStateModel model, CancellationToken ct = default);
    Task UpdateMasteryAsync(Guid studentId, Guid nodeId, KnowledgeMastery mastery, CancellationToken ct = default);
    Task<IReadOnlyList<StudentStateModel>> GetAllAsync(CancellationToken ct = default);
}
