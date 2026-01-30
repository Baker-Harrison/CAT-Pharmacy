# LessonsViewModel

## Purpose

`LessonsViewModel` manages lesson list loading, lesson selection, quiz submission, and section-level progress updates. It is the primary state holder for `LessonsView`.

## Key State

- `Lessons` (`ObservableCollection<LessonPlan>`) - list data
- `QuizQuestions` (`ObservableCollection<LessonQuizQuestionViewModel>`) - detail quiz inputs
- `SelectedLesson` - switches between list and detail views
- `IsLoading` - drives the loading state
- `IsGeneratingLessons` - shows the generation banner
- `IsSubmittingQuiz` - disables quiz submission
- `StatusMessage` - validation and error messaging

## Commands

- `LoadLessonsCommand` (`LoadLessonsAsync`)
  - Clears and reloads the lesson list from `LearningFlowService`.
- `OpenLessonCommand` (`OpenLesson`)
  - Sets `SelectedLesson`, clears status, and builds quiz question view models.
- `BackToListCommand` (`BackToList`)
  - Clears selection and quiz data.
- `SubmitQuizCommand` (`SubmitQuizAsync`)
  - Validates responses, submits answers, refreshes lessons, and toggles state flags.

## Section Progress

`UpdateSectionProgressAsync` is called by the view’s scroll tracking:
- Ignores invalid indexes and redundant progress values.
- Persists progress via `ILessonPlanRepository.UpdateSectionProgressAsync`.
- Updates `SelectedLesson` locally with `LessonPlan.WithSectionProgress`.

## Dependencies

- `LearningFlowService` - orchestration for lesson retrieval and quiz submission
- `ILessonPlanRepository` - persistence for section progress
