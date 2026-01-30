using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CatAdaptive.App.Messages;
using CatAdaptive.App.Models;
using CatAdaptive.Application.Services;
using CatAdaptive.Application.Abstractions;

namespace CatAdaptive.App.ViewModels;

public partial class UploadViewModel : ObservableObject
{
    private readonly LearningFlowService _learningFlowService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string? _selectedFilePath;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private int _knowledgeUnitsCreated;

    [ObservableProperty]
    private int _itemsGenerated;

    [ObservableProperty]
    private int _lessonsGenerated;

    [ObservableProperty]
    private bool _isSuccess;

    public UploadViewModel(LearningFlowService learningFlowService, IDialogService dialogService)
    {
        _learningFlowService = learningFlowService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var filePath = _dialogService.OpenFile("PowerPoint Files (*.pptx)|*.pptx", "Select a PowerPoint file");

        if (!string.IsNullOrEmpty(filePath))
        {
            SelectedFilePath = filePath;
            StatusMessage = null;
            IsSuccess = false;
            KnowledgeUnitsCreated = 0;
            ItemsGenerated = 0;
            LessonsGenerated = 0;
        }
    }

    [RelayCommand]
    private async Task ProcessFileAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            StatusMessage = "Please select a file first.";
            return;
        }

        if (!File.Exists(SelectedFilePath))
        {
            StatusMessage = "Selected file does not exist.";
            return;
        }

        IsProcessing = true;
        StatusMessage = "Processing...";
        IsSuccess = false;

        try
        {
            var result = await _learningFlowService.IngestAsync(SelectedFilePath);
            KnowledgeUnitsCreated = result.KnowledgeUnitsCreated;
            ItemsGenerated = result.ItemsGenerated;
            LessonsGenerated = result.LessonsGenerated;
            StatusMessage = $"Success! Created {result.KnowledgeUnitsCreated} knowledge units, {result.ItemsGenerated} items, and {result.LessonsGenerated} lessons.";
            IsSuccess = true;
            WeakReferenceMessenger.Default.Send(new NotificationMessage(
                new Notification("Content processed. Lessons are ready in the Lessons view.", NotificationType.Success, TimeSpan.FromSeconds(6))));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
