using System.Text.Json;

namespace CatAdaptive.Infrastructure.Repositories;

public abstract class BaseJsonRepository<T>
{
    private readonly string _filePath;
    protected List<T> _items = new();
    private bool _loaded;

    protected BaseJsonRepository(string dataDirectory, string fileName)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, fileName);
    }

    protected abstract Guid GetId(T item);

    protected async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loaded) return;
        if (File.Exists(_filePath))
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            _items = JsonSerializer.Deserialize<List<T>>(json, JsonRepositoryDefaults.CamelCase) ?? new();
        }
        _loaded = true;
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _items.ToList();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _items.FirstOrDefault(i => GetId(i) == id);
    }

    public virtual async Task AddAsync(T item, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _items.Add(item);
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> items, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _items.AddRange(items);
    }

    public virtual async Task ReplaceAllAsync(IEnumerable<T> items, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _items = items?.ToList() ?? new List<T>();
    }

    public virtual async Task UpdateAsync(T item, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        var id = GetId(item);
        var index = _items.FindIndex(i => GetId(i) == id);
        if (index >= 0)
        {
            _items[index] = item;
        }
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _items.RemoveAll(i => GetId(i) == id);
    }

    public virtual async Task SaveChangesAsync(CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(_items, JsonRepositoryDefaults.CamelCase);
        await File.WriteAllTextAsync(_filePath, json, ct);
    }
}
