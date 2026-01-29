using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CatAdaptive.Domain.Aggregates;
using CatAdaptive.Domain.Models;
using AppServices = CatAdaptive.Application.Services;

namespace CatAdaptive.App.ViewModels;

public partial class AdaptiveSessionViewModel : ObservableObject
{
    private readonly AppServices.AdaptiveTestService _testService;
    private AdaptiveSession? _session;
    private DateTime _itemStartTime;

    [ObservableProperty]
    private string _learnerName = string.Empty;

    [ObservableProperty]
    private string? _selectedTopic;

    [ObservableProperty]
    private bool _isSessionActive;

    [ObservableProperty]
    private string? _currentStem;

    [ObservableProperty]
    private ObservableCollection<ChoiceViewModel> _currentChoices = new();

    [ObservableProperty]
    private ChoiceViewModel? _selectedChoice;

    [ObservableProperty]
    private int _questionsAnswered;

    [ObservableProperty]
    private double _currentTheta;

    [ObservableProperty]
    private double _standardError;

    [ObservableProperty]
    private string? _feedbackMessage;

    [ObservableProperty]
    private bool _showFeedback;

    [ObservableProperty]
    private bool _sessionComplete;

    [ObservableProperty]
    private AppServices.SessionReport? _finalReport;

    public AdaptiveSessionViewModel(AppServices.AdaptiveTestService testService)
    {
        _testService = testService;
    }

    [RelayCommand]
    private async Task StartSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(LearnerName))
        {
            FeedbackMessage = "Please enter your name.";
            ShowFeedback = true;
            return;
        }

        try
        {
            var learner = LearnerProfile.Create(LearnerName);
            _session = await _testService.StartSessionAsync(learner, SelectedTopic);

            IsSessionActive = true;
            SessionComplete = false;
            QuestionsAnswered = 0;
            CurrentTheta = _session.CurrentAbility.Theta;
            StandardError = _session.CurrentAbility.StandardError;
            FinalReport = null;

            LoadNextItem();
        }
        catch (Exception ex)
        {
            FeedbackMessage = $"Error starting session: {ex.Message}";
            ShowFeedback = true;
        }
    }

    [RelayCommand]
    private async Task SubmitAnswerAsync()
    {
        if (_session == null || SelectedChoice == null) return;

        var responseTime = DateTime.Now - _itemStartTime;
        var isCorrect = SelectedChoice.IsCorrect;

        var response = await _testService.SubmitResponseAsync(
            _session,
            isCorrect,
            responseTime,
            SelectedChoice.Text);

        QuestionsAnswered++;
        CurrentTheta = _session.CurrentAbility.Theta;
        StandardError = _session.CurrentAbility.StandardError;

        FeedbackMessage = isCorrect ? "Correct!" : "Incorrect.";
        ShowFeedback = true;

        if (_session.IsComplete)
        {
            EndSession();
        }
        else
        {
            await Task.Delay(1000);
            ShowFeedback = false;
            LoadNextItem();
        }
    }

    private void LoadNextItem()
    {
        if (_session == null) return;

        var item = _testService.GetNextItem(_session);
        if (item == null)
        {
            EndSession();
            return;
        }

        CurrentStem = item.Stem;
        CurrentChoices.Clear();
        foreach (var choice in item.Choices)
        {
            CurrentChoices.Add(new ChoiceViewModel(choice.Id, choice.Text, choice.IsCorrect));
        }
        SelectedChoice = null;
        _itemStartTime = DateTime.Now;
    }

    private void EndSession()
    {
        if (_session == null) return;

        SessionComplete = true;
        IsSessionActive = false;
        FinalReport = _testService.GenerateReport(_session);
        FeedbackMessage = $"Session complete! Final ability estimate: {FinalReport.FinalAbility:F2} (SE: {FinalReport.StandardError:F2})";
        ShowFeedback = true;
    }

    [RelayCommand]
    private void ResetSession()
    {
        _session = null;
        IsSessionActive = false;
        SessionComplete = false;
        CurrentStem = null;
        CurrentChoices.Clear();
        SelectedChoice = null;
        QuestionsAnswered = 0;
        CurrentTheta = 0;
        StandardError = 1.0;
        FeedbackMessage = null;
        ShowFeedback = false;
        FinalReport = null;
    }
}

public partial class ChoiceViewModel : ObservableObject
{
    public Guid Id { get; }
    public string Text { get; }
    public bool IsCorrect { get; }

    public ChoiceViewModel(Guid id, string text, bool isCorrect)
    {
        Id = id;
        Text = text;
        IsCorrect = isCorrect;
    }
}
