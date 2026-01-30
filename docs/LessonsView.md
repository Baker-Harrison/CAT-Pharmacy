# LessonsView

## Purpose

`LessonsView` is the WPF UserControl that powers the lesson list and lesson detail experiences. It exposes loading, empty, and generation states and includes section-level progress indicators while reading.

## Key Files

```
src/CatAdaptive.App/Views/LessonsView.xaml
src/CatAdaptive.App/Views/LessonsView.xaml.cs
src/CatAdaptive.App/ViewModels/LessonsViewModel.cs
```

## View States

- **List view** when `SelectedLesson` is null.
- **Detail view** when a lesson is selected.
- **Loading** when `IsLoading` is true.
- **Generating banner** when `IsGeneratingLessons` is true.
- **Empty state** when there are no lessons.

## Lesson List

Each card shows:
- Title and summary
- Progress bar bound to `ProgressPercent`
- Score / remediation status (if present)
- Action button to open the lesson

## Lesson Detail

- Title + summary
- Section content rendered in a scrollable layout
- Knowledge check with quiz questions and text inputs
- Submit button bound to `SubmitQuizCommand`

## Section Progress Tracking

- Each section has a progress pill that displays `ReadPercent` and a read indicator.
- Progress updates are driven by scroll events in `LessonsView.xaml.cs`.
- Scroll events are debounced (500ms) and call `LessonsViewModel.UpdateSectionProgressAsync`.
- A section is considered read at **90%** scroll.

## Binding & Commands

- `LoadLessonsCommand` → refresh lesson list
- `OpenLessonCommand` → select a lesson and populate quiz questions
- `BackToListCommand` → return to list and clear detail state
- `SubmitQuizCommand` → validate and submit quiz responses

## Styling & Resources

Styles are defined in `LessonsView.xaml` as view-local resources (see `docs/LessonsViewStyling.md` for the key tokens).
