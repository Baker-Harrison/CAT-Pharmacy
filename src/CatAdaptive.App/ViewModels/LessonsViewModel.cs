using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CatAdaptive.App.Messages;
using CatAdaptive.Application.Abstractions;

namespace CatAdaptive.App.ViewModels;

/// <summary>
/// ViewModel for the Lessons page - serves as entry point to the personalized learning flow.
/// </summary>
public sealed partial class LessonsViewModel : ObservableObject
{
    private readonly ILessonPlanRepository _lessonPlanRepository;
    private readonly IAIContentGraphRepository _contentGraphRepository;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasLessons;

    [ObservableProperty]
    private bool _hasContent;

    [ObservableProperty]
    private int _completedLessonsCount;

    [ObservableProperty]
    private double _averageScore;

    [ObservableProperty]
    private string? _statusMessage;

    public LessonsViewModel(ILessonPlanRepository lessonPlanRepository, IAIContentGraphRepository contentGraphRepository)
    {
        _lessonPlanRepository = lessonPlanRepository;
        _contentGraphRepository = contentGraphRepository;
    }

    /// <summary>
    /// Loads lesson statistics for the dashboard display.
    /// </summary>
    [RelayCommand]
    public async Task LoadStatsAsync()
    {
        IsLoading = true;
        try
        {
            var contentGraph = await _contentGraphRepository.GetDefaultAsync();
            HasContent = contentGraph?.Nodes.Count > 0;

            var lessons = await _lessonPlanRepository.GetAllAsync();
            HasLessons = lessons.Count > 0;
            CompletedLessonsCount = lessons.Count(l => l.ProgressPercent >= 100);
            AverageScore = lessons.Any(l => l.LastScorePercent.HasValue)
                ? lessons.Where(l => l.LastScorePercent.HasValue).Average(l => l.LastScorePercent!.Value)
                : 0;

            StatusMessage = !HasContent
                ? "Upload lecture slides first to begin learning."
                : HasLessons
                    ? $"{lessons.Count} lesson(s) available. Start learning now!"
                    : "Content uploaded. Start personalized learning!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading stats: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Starts the personalized learning flow by navigating to the lesson flow view.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasContent))]
    private void StartAdaptiveLesson()
    {
        WeakReferenceMessenger.Default.Send(new NavigateToAdaptiveLessonMessage());
    }
}
