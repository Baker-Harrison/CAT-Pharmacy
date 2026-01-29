using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Infrastructure.Repositories;

public sealed class JsonItemRepository : IItemRepository
{
    private readonly string _filePath;
    private List<ItemTemplate> _items = new();
    private bool _loaded;

    public JsonItemRepository(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "item-bank.json");
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loaded) return;
        if (File.Exists(_filePath))
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            _items = JsonSerializer.Deserialize<List<ItemTemplate>>(json, JsonRepositoryDefaults.CamelCase) ?? new();
        }
        _loaded = true;
    }

    public async Task<IReadOnlyList<ItemTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _items.ToList();
    }

    public async Task<IReadOnlyList<ItemTemplate>> GetByTopicAsync(string topic, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _items.Where(i => i.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<ItemTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _items.FirstOrDefault(i => i.Id == id);
    }

    public async Task AddAsync(ItemTemplate item, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _items.Add(item);
    }

    public async Task AddRangeAsync(IEnumerable<ItemTemplate> items, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _items.AddRange(items);
    }

    public async Task ReplaceAllAsync(IEnumerable<ItemTemplate> items, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _items = items?.ToList() ?? new List<ItemTemplate>();
    }

    public async Task UpdateAsync(ItemTemplate item, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        var index = _items.FindIndex(i => i.Id == item.Id);
        if (index >= 0)
        {
            _items[index] = item;
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _items.RemoveAll(i => i.Id == id);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(_items, JsonRepositoryDefaults.CamelCase);
        await File.WriteAllTextAsync(_filePath, json, ct);
    }

}
