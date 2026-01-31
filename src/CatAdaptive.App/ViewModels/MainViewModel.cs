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

    private readonly UploadViewModel _uploadViewModel;
    private readonly LessonsViewModel _lessonsViewModel;
    private readonly PersonalizedLearningViewModel _personalizedLearningViewModel;
    private readonly DebugViewModel _debugViewModel;

    public MainViewModel(
        UploadViewModel uploadViewModel,
        LessonsViewModel lessonsViewModel,
        PersonalizedLearningViewModel personalizedLearningViewModel,
        DebugViewModel debugViewModel)
    {
        _uploadViewModel = uploadViewModel;
        _lessonsViewModel = lessonsViewModel;
        _personalizedLearningViewModel = personalizedLearningViewModel;
        _debugViewModel = debugViewModel;

        CurrentView = _uploadViewModel;

        WeakReferenceMessenger.Default.Register<NotificationMessage>(this, (_, m) => {
            Notifications.Add(m.Notification);
        });

        WeakReferenceMessenger.Default.Register<NavigateToLessonsMessage>(this, (_, _) => {
            NavigateToLessons();
        });

        WeakReferenceMessenger.Default.Register<NavigateToAdaptiveLessonMessage>(this, (_, _) => {
            NavigateToAdaptiveLesson();
        });
    }

    private void NavigateToPage(object viewModel, string pageTitle, Action? beforeNavigate = null)
    {
        beforeNavigate?.Invoke();
        CurrentView = viewModel;
        CurrentPage = pageTitle;
    }

    [RelayCommand]
    private void NavigateToUpload()
    {
        NavigateToPage(_uploadViewModel, "Upload Content");
    }

    [RelayCommand]
    private void NavigateToLessons()
    {
        if (_personalizedLearningViewModel.CurrentPhase != LearningPhase.NotStarted)
        {
            NavigateToPage(_personalizedLearningViewModel, "Personalized Learning");
        }
        else
        {
            NavigateToPage(_lessonsViewModel, "Lessons");
        }
    }

    [RelayCommand]
    private void NavigateToAdaptiveLesson()
    {
        NavigateToPage(_personalizedLearningViewModel, "Personalized Learning");
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
