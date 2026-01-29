using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

public interface IPptxParser
{
    Task<IReadOnlyList<KnowledgeUnit>> ParseAsync(string filePath, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeUnit>> ParseAsync(Stream stream, string fileName, CancellationToken ct = default);
}
