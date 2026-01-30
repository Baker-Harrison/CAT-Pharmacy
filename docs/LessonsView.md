# LessonsView Documentation

## Overview

The `LessonsView` is a comprehensive WPF UserControl that displays adaptive learning lessons to users. It features two primary modes: a lesson list view and a detailed lesson view, with smooth transitions between them. The view supports various states including loading, empty, and content generation states.

## Architecture

### File Structure
```
src/CatAdaptive.App/
├── Views/
│   └── LessonsView.xaml          # Main UI definition
│   └── LessonsView.xaml.cs       # Code-behind (minimal)
├── ViewModels/
│   └── LessonsViewModel.cs       # Business logic and state management
└── Converters/
    ├── BoolConverters.cs         # Boolean to visibility converters
    └── ZeroToCollapsedConverter.cs # Collection state converter
```

## UI Components

### 1. Resource Dictionary

The view defines a comprehensive set of resources for consistent styling:

#### Color Palette
- **PrimaryTextBrush** (#1E293B): Main text color
- **SecondaryTextBrush** (#64748B): Subtle text and labels
- **PrimaryBlueBrush** (#2563EB): Primary action color
- **SurfaceBrush** (White): Card and input backgrounds
- **BorderBrush** (#E2E8F0): Subtle borders
- **ErrorTextBrush** (#DC2626): Error messages
- **WarningBackgroundBrush** (#FEFCE8): Generation status banner

#### Typography Styles
- **HeaderTextStyle**: 28pt, Bold - Page titles
- **SubHeaderTextStyle**: 14pt - Descriptive text
- **SectionHeaderStyle**: 20pt, SemiBold - Content sections

#### Component Styles
- **CardStyle**: Elevated cards with drop shadows
- **PrimaryButtonStyle**: Blue filled buttons with hover states
- **OutlineButtonStyle**: Transparent buttons with blue borders
- **ModernProgressBar**: Rounded progress indicators
- **InputTextBoxStyle**: Styled text inputs with focus states
- **ViewTransitionStyle**: Fade and slide animations

### 2. Lesson List View

Displayed when `SelectedLesson` is null. Includes:

#### Header Section
- Page title "My Lessons"
- Subtitle explaining lesson generation

#### Generation Banner
- Visible when `IsGeneratingLessons` is true
- Shows rotating spinner icon and status message
- Yellow/amber color scheme for visibility

#### Content States

##### Loading State
- Centered rotating spinner animation
- "Loading your learning plan..." message
- Triggered by `IsLoading` property

##### Empty State
- Displayed when lesson collection is empty
- Book emoji (📚) for visual appeal
- Clear messaging about next steps
- "Refresh Lessons" button for manual retry

##### Lesson List
- Card-based layout for each lesson
- Each card contains:
  - Lesson title and remediation badge
  - Summary description
  - Progress bar with percentage
  - Score display
  - "Open Lesson" action button

### 3. Lesson Detail View

Displayed when a lesson is selected. Features:

#### Navigation
- Back button with arrow icon
- Returns to lesson list view

#### Content Sections
- Lesson title and summary
- Multiple content sections with:
  - Section headings
  - Body text with proper line height
  - Interactive prompt cards with light bulb icons

#### Knowledge Check
- Section header for quiz area
- Question cards for each quiz item:
  - Question type badge (e.g., "FILL IN THE BLANK")
  - Question prompt
  - Text input area for responses
- Error message display for validation
- Submit button with loading state

## Data Binding

### ViewModel Properties

#### LessonsViewModel
```csharp
// Collections
public ObservableCollection<LessonPlan> Lessons { get; }
public ObservableCollection<LessonQuizQuestionViewModel> QuizQuestions { get; }

// State Properties
[ObservableProperty] private LessonPlan? _selectedLesson;
[ObservableProperty] private bool _isGeneratingLessons;
[ObservableProperty] private bool _isLoading;
[ObservableProperty] private bool _isSubmittingQuiz;
[ObservableProperty] private string? _statusMessage;
```

#### LessonQuizQuestionViewModel
```csharp
public LessonQuizQuestion Question { get; }
[ObservableProperty] private string _responseText = string.Empty;
public string TypeDisplay { get; } // Uppercase formatted type
```

### Commands
- **LoadLessonsCommand**: Refreshes the lesson list
- **OpenLessonCommand**: Opens a specific lesson
- **BackToListCommand**: Returns to lesson list
- **SubmitQuizCommand**: Submits quiz answers

## Converters

### BoolConverters.cs
- `BoolToVisibilityConverter`: Bool → Visible/Collapsed
- `InverseBoolToVisibilityConverter`: !Bool → Visible/Collapsed
- `InverseBoolConverter`: !Bool
- `NullToVisibilityConverter`: Null → Collapsed
- `NullToCollapsedConverter`: Null → Visible
- `ZeroToVisibilityConverter`: Count > 0 → Visible

### ZeroToCollapsedConverter.cs
- `ZeroToCollapsedConverter`: Count == 0 → Visible (for empty states)

## Animations

### View Transitions
The `ViewTransitionStyle` provides smooth transitions:
- **Fade In**: Opacity 0 → 1 over 0.4s
- **Slide Up**: Y translation 20px → 0 over 0.4s
- **Easing**: Quadratic ease-out for natural movement

### Loading Spinner
Rotating animation using `RotateTransform`:
- Continuous 360° rotation
- 1-second duration
- Infinite repeat

## User Experience Features

### Responsive Design
- MaxWidth constraints for readability
- ScrollViewer for content overflow
- Proper spacing and margins

### Visual Feedback
- Button hover states
- Input field focus indicators
- Loading states for async operations
- Error message display

### Accessibility
- Semantic color usage
- Clear typography hierarchy
- Sufficient contrast ratios
- Keyboard navigation support

## Error Handling

### Validation
- Quiz submission requires all questions answered
- Clear error messages displayed in red banners
- Submit button disabled during submission

### State Management
- Proper cleanup when navigating between views
- Loading states prevent duplicate operations
- Error states are recoverable

## Performance Considerations

### Virtualization
- ScrollViewer enables efficient rendering
- ItemsControl with data templates for list items

### Resource Management
- Shared styles reduce memory footprint
- Proper cleanup in view transitions

### Async Operations
- Non-blocking UI during lesson loading
- Background quiz submission

## Future Enhancements

### Potential Improvements
1. **Search/Filter**: Add lesson search functionality
2. **Pagination**: Handle large lesson collections
3. **Offline Support**: Cache lessons for offline viewing
4. **Progress Persistence**: Save reading progress
5. **Accessibility**: Enhanced screen reader support
6. **Themes**: Support for dark/light mode switching
7. **Animations**: Micro-interactions for better feedback
8. **Printing**: Optimized print styles for lessons

### Extensibility
- Modular component design allows easy feature additions
- ViewModel separation enables testing
- Resource-based styling supports theming

## Troubleshooting

### Common Issues
1. **Missing Resources**: Ensure all converters are registered in App.xaml
2. **Animation Issues**: Check that ViewTransitionStyle is defined before use
3. **Binding Errors**: Verify property names match ViewModel exactly
4. **Performance**: Monitor memory usage with large lesson collections

### Debug Tips
- Use Visual Studio XAML editor for design-time validation
- Check Output window for binding errors
- Verify converter registrations in App.xaml
- Test with various data states (empty, single, multiple items)

## Conclusion

The LessonsView provides a robust, modern interface for adaptive learning content. Its component-based architecture, comprehensive styling system, and thoughtful UX patterns create an engaging learning experience while maintaining code quality and extensibility.
