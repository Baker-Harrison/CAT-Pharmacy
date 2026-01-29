using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Infrastructure.Repositories;

public sealed class JsonLessonPlanRepository : ILessonPlanRepository
{
    private readonly string _filePath;
    private List<LessonPlan> _lessons = new();
    private bool _loaded;

    public JsonLessonPlanRepository(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "lesson-plans.json");
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loaded) return;
        if (File.Exists(_filePath))
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            _lessons = JsonSerializer.Deserialize<List<LessonPlan>>(json, JsonRepositoryDefaults.DefaultCaseInsensitive) ?? new();
        }
        _loaded = true;
    }

    public async Task<IReadOnlyList<LessonPlan>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _lessons.ToList();
    }

    public async Task<LessonPlan?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _lessons.FirstOrDefault(l => l.Id == id);
    }

    public async Task AddRangeAsync(IEnumerable<LessonPlan> lessons, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _lessons.AddRange(lessons);
    }

    public async Task ReplaceAllAsync(IEnumerable<LessonPlan> lessons, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _lessons = lessons?.ToList() ?? new List<LessonPlan>();
    }

    public async Task UpdateAsync(LessonPlan lesson, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        var index = _lessons.FindIndex(l => l.Id == lesson.Id);
        if (index >= 0)
        {
            _lessons[index] = lesson;
            return;
        }
        _lessons.Add(lesson);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(_lessons, JsonRepositoryDefaults.CamelCase);
        await File.WriteAllTextAsync(_filePath, json, ct);
    }
}
