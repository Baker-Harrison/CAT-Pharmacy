# Section Progress Tracking

## Summary

Lessons track reading progress at the section level. Progress is derived from scroll position and persisted so users can resume where they left off.

## How It Works

- Each `LessonPlan` has a `SectionProgresses` collection and `ProgressPercent`.
- As the user scrolls a section, `LessonsView.xaml.cs` debounces events and calls `LessonsViewModel.UpdateSectionProgressAsync`.
- A section is considered read at **90%** scroll.
- Progress is persisted via `ILessonPlanRepository.UpdateSectionProgressAsync` and reflected in the UI.

## Key Files

- `src/CatAdaptive.Domain/Models/LessonPlan.cs`
- `src/CatAdaptive.App/Views/LessonsView.xaml`
- `src/CatAdaptive.App/Views/LessonsView.xaml.cs`
- `src/CatAdaptive.App/ViewModels/LessonsViewModel.cs`
- `src/CatAdaptive.Infrastructure/Repositories/JsonLessonPlanRepository.cs`

## UI Indicators

- Read status and percent are displayed beside section headers.
- `BoolToColorConverter` controls read/unread color contrast.
