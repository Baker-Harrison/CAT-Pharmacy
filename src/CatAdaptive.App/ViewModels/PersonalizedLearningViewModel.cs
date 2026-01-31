using System.Collections.ObjectModel;
using CatAdaptive.Application.Services;
using CatAdaptive.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.App.ViewModels;

/// <summary>
/// ViewModel for personalized learning experience.
/// </summary>
public sealed partial class PersonalizedLearningViewModel : ObservableObject
{
    private readonly PersonalizedLearningOrchestrator _orchestrator;
    private readonly StudentStateService _studentStateService;
    private readonly ILogger<PersonalizedLearningViewModel> _logger;

    [ObservableProperty]
    private LearningSession? _currentSession;

    [ObservableProperty]
    private LearningModule? _currentModule;

    [ObservableProperty]
    private PersonalizedModuleContent? _currentContent;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingMessage = string.Empty;

    [ObservableProperty]
    private StudentStateModel? _studentState;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private ObservableCollection<ModuleProgressItem> _moduleProgress = new();

    [ObservableProperty]
    private LearningPhase _currentPhase = LearningPhase.NotStarted;

    [ObservableProperty]
    private string _currentFeedback = string.Empty;

    [ObservableProperty]
    private double _overallProgress;

    public Guid StudentId { get; private set; }

    public PersonalizedLearningViewModel(
        PersonalizedLearningOrchestrator orchestrator,
        StudentStateService studentStateService,
        ILogger<PersonalizedLearningViewModel> logger)
    {
        _orchestrator = orchestrator;
        _studentStateService = studentStateService;
        _logger = logger;
    }

    /// <summary>
    /// Initializes the ViewModel with a student ID.
    /// </summary>
    public async Task InitializeAsync(Guid studentId, CancellationToken ct = default)
    {
        StudentId = studentId;
        StudentState = await _studentStateService.GetOrCreateStudentStateAsync(studentId, ct);
        _logger.LogInformation("Initialized PersonalizedLearningViewModel for student {StudentId}", studentId);
    }

    /// <summary>
    /// Starts a new personalized learning session.
    /// </summary>
    [RelayCommand]
    private async Task StartLearningAsync()
    {
        IsLoading = true;
        LoadingMessage = "Creating your personalized learning path...";
        ErrorMessage = null;

        try
        {
            var goals = new LearningGoals(MaxModules: 5, MaxMinutes: 60);
            CurrentSession = await _orchestrator.StartPersonalizedSessionAsync(StudentId, goals);
            
            // Update module progress display
            UpdateModuleProgress();
            
            CurrentPhase = LearningPhase.ModuleIntro;
            
            // Load first module content
            await LoadCurrentModuleContentAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start learning session");
            ErrorMessage = $"Failed to start session: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads content for the current module.
    /// </summary>
    private async Task LoadCurrentModuleContentAsync()
    {
        if (CurrentSession == null) return;

        IsLoading = true;
        LoadingMessage = "Personalizing your content...";

        try
        {
            CurrentModule = CurrentSession.LearningPath.GetCurrentModule();
            
            if (CurrentModule != null)
            {
                CurrentContent = await _orchestrator.GetCurrentModuleContentAsync(CurrentSession);
                CurrentPhase = LearningPhase.Learning;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load module content");
            ErrorMessage = $"Failed to load content: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Submits a response to the current content.
    /// </summary>
    [RelayCommand]
    private async Task SubmitResponseAsync(string response)
    {
        if (CurrentSession == null || CurrentModule == null) return;

        IsLoading = true;
        LoadingMessage = "Analyzing your response...";

        try
        {
            var interaction = new StudentInteraction(
                NodeId: CurrentModule.DomainNodeId,
                Response: response,
                ExpectedAnswer: null,
                CorrectnesScore: 0.0, // Will be evaluated by AI
                Confidence: 0.7,
                ResponseTimeSeconds: 30,
                Difficulty: CurrentModule.Difficulty,
                BloomsLevel: BloomsLevel.Understand,
                ContentType: "response",
                AttemptNumber: 1);

            var adaptiveResponse = await _orchestrator.ProcessInteractionAsync(
                CurrentSession, interaction);

            CurrentFeedback = adaptiveResponse.Feedback;
            
            // Update student state
            StudentState = await _studentStateService.GetOrCreateStudentStateAsync(StudentId);
            
            // Update progress
            UpdateOverallProgress();

            if (adaptiveResponse.ShouldAdvance)
            {
                CurrentPhase = LearningPhase.Assessment;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process response");
            ErrorMessage = $"Failed to process response: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Completes the current module.
    /// </summary>
    [RelayCommand]
    private async Task CompleteModuleAsync()
    {
        if (CurrentSession == null || CurrentModule == null) return;

        IsLoading = true;
        LoadingMessage = "Completing module...";

        try
        {
            var result = await _orchestrator.CompleteModuleAsync(
                CurrentSession, CurrentModule.ModuleId);

            // Update module progress
            var progressItem = ModuleProgress.FirstOrDefault(m => m.ModuleId == CurrentModule.ModuleId);
            if (progressItem != null)
            {
                progressItem.IsCompleted = true;
                progressItem.Passed = result.Passed;
            }

            if (result.RecommendedNextModule.HasValue)
            {
                // Move to next module
                CurrentPhase = LearningPhase.ModuleIntro;
                await LoadCurrentModuleContentAsync();
            }
            else
            {
                // Session complete
                CurrentPhase = LearningPhase.SessionComplete;
            }

            UpdateOverallProgress();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete module");
            ErrorMessage = $"Failed to complete module: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Exits the learning session.
    /// </summary>
    [RelayCommand]
    private void ExitSession()
    {
        CurrentSession = null;
        CurrentModule = null;
        CurrentContent = null;
        CurrentPhase = LearningPhase.NotStarted;
        ModuleProgress.Clear();
        OverallProgress = 0;
    }

    private void UpdateModuleProgress()
    {
        if (CurrentSession == null) return;

        ModuleProgress.Clear();
        foreach (var module in CurrentSession.LearningPath.Modules)
        {
            ModuleProgress.Add(new ModuleProgressItem
            {
                ModuleId = module.ModuleId,
                Title = module.Title,
                IsCompleted = module.Status == ModuleStatus.Completed,
                IsCurrent = module.ModuleId == CurrentModule?.ModuleId,
                Passed = false
            });
        }
    }

    private void UpdateOverallProgress()
    {
        if (CurrentSession == null || !CurrentSession.LearningPath.Modules.Any())
        {
            OverallProgress = 0;
            return;
        }

        var completed = ModuleProgress.Count(m => m.IsCompleted);
        OverallProgress = (double)completed / ModuleProgress.Count * 100;
    }
}

/// <summary>
/// Learning phase enumeration.
/// </summary>
public enum LearningPhase
{
    NotStarted,
    ModuleIntro,
    Learning,
    Practice,
    Assessment,
    ModuleComplete,
    SessionComplete
}

/// <summary>
/// Module progress item for display.
/// </summary>
public sealed class ModuleProgressItem : ObservableObject
{
    public Guid ModuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    
    private bool _isCompleted;
    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetProperty(ref _isCompleted, value);
    }
    
    private bool _isCurrent;
    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }
    
    private bool _passed;
    public bool Passed
    {
        get => _passed;
        set => SetProperty(ref _passed, value);
    }
}
