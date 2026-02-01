using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CatAdaptive.App.Messages;
using CatAdaptive.App.Models;
using System.Windows;
using System.Windows.Media;

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

    [ObservableProperty]
    [NotifyPropertyChangedRecipients]
    private bool _isDarkModeEnabled; // New property for theme toggle

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
        SetTheme(false); // Default to light mode on startup

        WeakReferenceMessenger.Default.Register<NotificationMessage>(this, (_, m) => {
            Notifications.Add(m.Notification);
        });
    }

    // Method called when IsDarkModeEnabled changes
    partial void OnIsDarkModeEnabledChanged(bool value)
    {
        SetTheme(value);
    }

    private void SetTheme(bool isDark)
    {
        var app = Application.Current;
        var mergedDictionaries = app.Resources.MergedDictionaries;

        // Remove existing theme dictionary if present (to avoid duplicates)
        var existingTheme = mergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Theme"));
        if (existingTheme != null) {
            mergedDictionaries.Remove(existingTheme);
        }

        // Load new theme dictionary
        var themeUri = isDark ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative) : new Uri("Themes/LightTheme.xaml", UriKind.Relative);
        mergedDictionaries.Add(new ResourceDictionary() { Source = themeUri });
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
