using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Infrastructure.Repositories;

public sealed class JsonKnowledgeUnitRepository : IKnowledgeUnitRepository
{
    private readonly string _filePath;
    private List<KnowledgeUnit> _units = new();
    private bool _loaded;

    public JsonKnowledgeUnitRepository(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "knowledge-units.json");
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loaded) return;
        if (File.Exists(_filePath))
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            _units = JsonSerializer.Deserialize<List<KnowledgeUnit>>(json, JsonRepositoryDefaults.CamelCase) ?? new();
        }
        _loaded = true;
    }

    public async Task<IReadOnlyList<KnowledgeUnit>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _units.ToList();
    }

    public async Task<IReadOnlyList<KnowledgeUnit>> GetByTopicAsync(string topic, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _units.Where(u => u.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<KnowledgeUnit?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _units.FirstOrDefault(u => u.Id == id);
    }

    public async Task AddAsync(KnowledgeUnit unit, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _units.Add(unit);
    }

    public async Task AddRangeAsync(IEnumerable<KnowledgeUnit> units, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _units.AddRange(units);
    }

    public async Task ReplaceAllAsync(IEnumerable<KnowledgeUnit> units, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _units = units?.ToList() ?? new List<KnowledgeUnit>();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _units.RemoveAll(u => u.Id == id);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(_units, JsonRepositoryDefaults.CamelCase);
        await File.WriteAllTextAsync(_filePath, json, ct);
    }

}
