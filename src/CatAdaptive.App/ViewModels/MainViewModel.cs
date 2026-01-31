using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CatAdaptive.App.Messages;
using CatAdaptive.App.Models;

namespace CatAdaptive.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentPage = "Upload Content";

    public ObservableCollection<Notification> Notifications { get; } = new();

    [ObservableProperty]
    private string _currentNavigation = "Upload"; // Default to Upload

    private readonly UploadViewModel _uploadViewModel;
    private readonly LessonsViewModel _lessonsViewModel;
    private readonly AdaptiveSessionViewModel _adaptiveSessionViewModel;
    private readonly DebugViewModel _debugViewModel;

    public MainViewModel(
        UploadViewModel uploadViewModel,
        LessonsViewModel lessonsViewModel,
        AdaptiveSessionViewModel adaptiveSessionViewModel,
        DebugViewModel debugViewModel)
    {
        _uploadViewModel = uploadViewModel;
        _lessonsViewModel = lessonsViewModel;
        _adaptiveSessionViewModel = adaptiveSessionViewModel;
        _debugViewModel = debugViewModel;

        CurrentView = _uploadViewModel;

        WeakReferenceMessenger.Default.Register<NotificationMessage>(this, (_, m) => {
            Notifications.Add(m.Notification);
        });
    }

    private void NavigateToPage(object viewModel, string pageTitle, Action? beforeNavigate = null)
    {
        beforeNavigate?.Invoke();
        CurrentView = viewModel;
        CurrentPage = pageTitle;

        // Update current navigation state
        if (viewModel is UploadViewModel) CurrentNavigation = "Upload";
        else if (viewModel is LessonsViewModel) CurrentNavigation = "Lessons";
        else if (viewModel is AdaptiveSessionViewModel) CurrentNavigation = "Cat";
        else if (viewModel is DebugViewModel) CurrentNavigation = "Debug";
    }

    [RelayCommand]
    private void NavigateToUpload()
    {
        NavigateToPage(_uploadViewModel, "Upload Content");
    }

    [RelayCommand]
    private void NavigateToLessons()
    {
        NavigateToPage(_lessonsViewModel, "Lessons", () => _lessonsViewModel.LoadLessonsCommand.Execute(null));
    }

    [RelayCommand]
    private void NavigateToCat()
    {
        NavigateToPage(_adaptiveSessionViewModel, "CAT");
    }

    [RelayCommand]
    private void NavigateToDebug()
    {
        NavigateToPage(_debugViewModel, "System Debug");
    }

    [RelayCommand]
    private void RemoveNotification(Notification notification)
    {
        Notifications.Remove(notification);
    }
}
