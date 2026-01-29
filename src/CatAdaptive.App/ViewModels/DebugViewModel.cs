using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using CatAdaptive.Application.Abstractions;

namespace CatAdaptive.App.ViewModels;

public partial class DebugViewModel : ObservableObject
{
    private readonly IConfiguration _configuration;
    private readonly IKnowledgeUnitRepository _knowledgeUnitRepository;
    private readonly IItemRepository _itemRepository;
    private readonly ILessonPlanRepository _lessonPlanRepository;
    private readonly AdaptiveSessionViewModel _adaptiveSessionViewModel;

    [ObservableProperty]
    private string _systemInfo = "Loading...";

    [ObservableProperty]
    private string _repositoryStats = "Loading...";

    [ObservableProperty]
    private string _currentSessionDetails = "Loading...";

    public DebugViewModel(
        IConfiguration configuration,
        IKnowledgeUnitRepository knowledgeUnitRepository,
        IItemRepository itemRepository,
        ILessonPlanRepository lessonPlanRepository,
        AdaptiveSessionViewModel adaptiveSessionViewModel)
    {
        _configuration = configuration;
        _knowledgeUnitRepository = knowledgeUnitRepository;
        _itemRepository = itemRepository;
        _lessonPlanRepository = lessonPlanRepository;
        _adaptiveSessionViewModel = adaptiveSessionViewModel;

        // Load initial data
        Refresh();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadSystemInfoAsync();
        await LoadRepositoryStatsAsync();
        LoadSessionDetails();
    }

    private void Refresh()
    {
        _ = RefreshAsync();
    }

    private Task LoadSystemInfoAsync()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown";
        var useGemini = _configuration.GetValue<bool>("Gemini:UseGemini");
        var modelName = _configuration.GetValue<string>("Gemini:ModelName") ?? "N/A";

        SystemInfo = $"App Version: {version}\nGemini API Enabled: {useGemini}\nModel Name: {modelName}";
                     
        return Task.CompletedTask;
    }

    private async Task LoadRepositoryStatsAsync()
    {
        try
        {
            var units = await _knowledgeUnitRepository.GetAllAsync();
            var items = await _itemRepository.GetAllAsync();
            var lessons = await _lessonPlanRepository.GetAllAsync();

            RepositoryStats = $"Knowledge Units: {units.Count}\nItems: {items.Count}\nLesson Plans: {lessons.Count}";
        }
        catch (Exception ex)
        {
            RepositoryStats = $"Error loading stats: {ex.Message}";
        }
    }

    private void LoadSessionDetails()
    {
        if (_adaptiveSessionViewModel.IsSessionActive)
        {
            CurrentSessionDetails = $"Active Session: Yes\nLearner: {_adaptiveSessionViewModel.LearnerName}\nQuestions Answered: {_adaptiveSessionViewModel.QuestionsAnswered}\nCurrent Theta: {_adaptiveSessionViewModel.CurrentTheta:F2}\nSE: {_adaptiveSessionViewModel.StandardError:F2}";
        }
        else
        {
            CurrentSessionDetails = "No active session.";
        }
    }
}