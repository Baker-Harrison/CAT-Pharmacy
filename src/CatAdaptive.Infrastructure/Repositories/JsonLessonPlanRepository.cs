using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Infrastructure.Repositories;

public sealed class JsonLessonPlanRepository : BaseJsonRepository<LessonPlan>, ILessonPlanRepository
{
    public JsonLessonPlanRepository(string dataDirectory) 
        : base(dataDirectory, "lesson-plans.json")
    {
    }

    protected override Guid GetId(LessonPlan item) => item.Id;

    public override async Task UpdateAsync(LessonPlan lesson, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        var index = _items.FindIndex(l => l.Id == lesson.Id);
        if (index >= 0)
        {
            _items[index] = lesson;
            return;
        }
        _items.Add(lesson);
    }

    public async Task UpdateSectionProgressAsync(Guid lessonId, Guid sectionId, double readPercent, bool isRead, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        var lesson = _items.FirstOrDefault(l => l.Id == lessonId);
        if (lesson != null)
        {
            var updatedLesson = lesson.WithSectionProgress(sectionId, readPercent, isRead);
            await UpdateAsync(updatedLesson, ct);
        }
    }
}