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
    private readonly ContentIngestionService _contentIngestionService;
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
    private bool _isSuccess;

    public UploadViewModel(ContentIngestionService contentIngestionService, IDialogService dialogService)
    {
        _contentIngestionService = contentIngestionService;
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
            var result = await _contentIngestionService.IngestAsync(SelectedFilePath);
            KnowledgeUnitsCreated = result.KnowledgeUnitsCreated;
            ItemsGenerated = result.ItemsGenerated;
            StatusMessage = $"Success! Created {result.KnowledgeUnitsCreated} knowledge units and {result.ItemsGenerated} items.";
            IsSuccess = true;
            WeakReferenceMessenger.Default.Send(new NotificationMessage(
                new Notification("Content processed successfully.", NotificationType.Success, TimeSpan.FromSeconds(6))));
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

    [RelayCommand]
    private void ClearFile()
    {
        SelectedFilePath = null;
        StatusMessage = null;
        IsSuccess = false;
        KnowledgeUnitsCreated = 0;
        ItemsGenerated = 0;
    }
}
