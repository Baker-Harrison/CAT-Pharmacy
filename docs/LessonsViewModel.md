# LessonsViewModel Architecture Documentation

## Overview

The `LessonsViewModel` is the core business logic component for the LessonsView, implementing the MVVM pattern to manage lesson data, user interactions, and application state. It orchestrates the flow between lesson listing, detailed viewing, and quiz submission.

## Class Structure

### LessonsViewModel

The main view model responsible for:
- Managing lesson collections
- Handling view state transitions
- Coordinating with the LearningFlowService
- Managing quiz submission workflow

```csharp
public partial class LessonsViewModel : ObservableObject
{
    private readonly LearningFlowService _learningFlowService;
    
    // Collections
    public ObservableCollection<LessonPlan> Lessons { get; }
    public ObservableCollection<LessonQuizQuestionViewModel> QuizQuestions { get; }
    
    // Observable Properties
    [ObservableProperty] private LessonPlan? _selectedLesson;
    [ObservableProperty] private bool _isGeneratingLessons;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSubmittingQuiz;
    [ObservableProperty] private string? _statusMessage;
}
```

### LessonQuizQuestionViewModel

A sealed view model for individual quiz questions:
- Wraps a `LessonQuizQuestion` domain object
- Manages user response text
- Provides formatted display properties

```csharp
public sealed partial class LessonQuizQuestionViewModel : ObservableObject
{
    public LessonQuizQuestion Question { get; }
    [ObservableProperty] private string _responseText = string.Empty;
    public string TypeDisplay { get; } // Uppercase formatted type
}
```

## Properties

### Collections

#### Lessons
- **Type**: `ObservableCollection<LessonPlan>`
- **Purpose**: Stores the list of available lessons
- **Updates**: Cleared and repopulated in `LoadLessonsAsync()`
- **UI Binding**: Used by ItemsControl in lesson list view

#### QuizQuestions
- **Type**: `ObservableCollection<LessonQuizQuestionViewModel>`
- **Purpose**: Holds view models for quiz questions in the selected lesson
- **Lifecycle**: Cleared and recreated when opening a lesson
- **UI Binding**: ItemsControl in lesson detail view

### State Properties

#### SelectedLesson
- **Type**: `LessonPlan?`
- **Purpose**: Tracks the currently selected lesson
- **UI Impact**: Controls view switching (list vs detail)
- **Navigation**: Set by `OpenLessonCommand`, cleared by `BackToListCommand`

#### IsGeneratingLessons
- **Type**: `bool`
- **Purpose**: Indicates new lessons are being generated
- **Trigger**: Set during quiz submission in `SubmitQuizAsync()`
- **UI Impact**: Shows generation banner in lesson list

#### IsLoading
- **Type**: `bool`
- **Purpose**: Tracks initial lesson loading state
- **Trigger**: Set in `LoadLessonsAsync()`
- **UI Impact**: Shows loading spinner, hides content

#### IsSubmittingQuiz
- **Type**: `bool`
- **Purpose**: Prevents duplicate quiz submissions
- **Trigger**: Set during `SubmitQuizAsync()`
- **UI Impact**: Disables submit button, shows loading state

#### StatusMessage
- **Type**: `string?`
- **Purpose**: Displays error messages to the user
- **Content**: Validation errors or submission failures
- **UI Impact**: Red error banner when not null

## Commands

### LoadLessonsCommand
```csharp
[RelayCommand]
public async Task LoadLessonsAsync()
```
- **Purpose**: Refresh the lesson list from the service
- **Flow**:
  1. Set `IsLoading = true`
  2. Clear existing lessons
  3. Fetch from `LearningFlowService`
  4. Populate collection
  5. Set `IsLoading = false`
- **Error Handling**: Try/finally ensures loading state is always cleared

### OpenLessonCommand
```csharp
[RelayCommand]
private void OpenLesson(LessonPlan lesson)
```
- **Purpose**: Switch to detail view for a specific lesson
- **Parameters**: `LessonPlan` to open
- **Actions**:
  1. Set `SelectedLesson`
  2. Clear previous quiz questions
  3. Clear any status messages
  4. Create view models for quiz questions

### BackToListCommand
```csharp
[RelayCommand]
private void BackToList()
```
- **Purpose**: Return from detail view to lesson list
- **Actions**:
  1. Clear `SelectedLesson`
  2. Clear quiz questions
  3. Clear status messages

### SubmitQuizCommand
```csharp
[RelayCommand]
private async Task SubmitQuizAsync()
```
- **Purpose**: Submit quiz answers and trigger lesson generation
- **Validation**:
  - Ensures `SelectedLesson` exists
  - Validates all questions have responses
- **Flow**:
  1. Set loading states (`IsSubmittingQuiz`, `IsGeneratingLessons`)
  2. Create answer objects from responses
  3. Submit to `LearningFlowService`
  4. Navigate back to list
  5. Reload lessons to show new content
- **Error Handling**: Catches exceptions and displays error messages

## Data Flow

### Lesson Loading Flow
```
User Action → LoadLessonsCommand → IsLoading=true → Service Call → Populate Lessons → IsLoading=false
```

### Lesson Viewing Flow
```
Lesson Selection → OpenLessonCommand → SelectedLesson=set → Create Quiz VMs → UI Updates
```

### Quiz Submission Flow
```
Submit Click → Validation → IsSubmittingQuiz=true → Service Call → BackToList → LoadLessons → States Reset
```

## Service Integration

### LearningFlowService Dependency
- **Injection**: Constructor injection
- **Usage**:
  - `GetLessonsAsync()`: Fetch available lessons
  - `SubmitQuizAsync()`: Submit answers and trigger generation
- **Decoupling**: ViewModel only knows about service interface

## Error Handling Strategies

### Input Validation
- Quiz submission requires all fields populated
- Clear error messages guide user action
- Submit button state prevents invalid submissions

### Service Errors
- Try/catch blocks around service calls
- Error messages displayed in UI
- Loading states properly reset on failure

### State Consistency
- Finally blocks ensure state cleanup
- Defensive programming with null checks
- Observable properties trigger UI updates

## Performance Considerations

### ObservableCollection Usage
- Efficient for UI updates
- Bulk operations (Clear/Add) minimize notifications
- Consider using.AddRange for large datasets

### Async Operations
- Non-blocking UI during service calls
- Proper async/await patterns
- Cancellation tokens could be added

### Memory Management
- Clear collections when switching views
- View models are lightweight
- No memory leaks observed

## Testing Considerations

### Unit Test Scenarios
1. **LoadLessonsAsync**: Verify loading states and population
2. **OpenLesson**: Check quiz question creation
3. **SubmitQuiz**: Test validation and error handling
4. **BackToList**: Ensure proper cleanup

### Mock Requirements
- `LearningFlowService` interface for mocking
- Test data for lessons and questions
- Observable property verification

## Extensibility

### Future Enhancements
1. **Search/Filter**: Add search properties and commands
2. **Pagination**: Implement page-based loading
3. **Caching**: Add local storage integration
4. **Progress Tracking**: Save reading progress
5. **Offline Mode**: Support offline lesson access

### Modification Points
- Service interface for different backends
- Question types for new quiz formats
- UI state management for additional views

## Best Practices Applied

### MVVM Pattern
- Clean separation of concerns
- No UI code in ViewModel
- Observable properties for data binding
- Commands for user actions

### Async Programming
- Async suffix for async methods
- Proper exception handling
- Loading state management

### Code Organization
- Partial classes for generated code
- Sealed classes where appropriate
- Clear property and method naming
- Minimal constructor logic

## Conclusion

The LessonsViewModel demonstrates a well-structured MVVM implementation with proper state management, error handling, and service integration. Its design supports testing, extension, and maintenance while providing a responsive user experience through careful async operation handling.
