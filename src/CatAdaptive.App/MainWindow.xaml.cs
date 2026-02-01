using System.Windows;
using System.Windows.Controls;
using CatAdaptive.App.ViewModels;

namespace CatAdaptive.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void NavigateToUpload(object sender, RoutedEventArgs e)
    {
        ViewModel?.NavigateToUploadCommand.Execute(null);
    }

    private void NavigateToLessons(object sender, RoutedEventArgs e)
    {
        ViewModel?.NavigateToLessonsCommand.Execute(null);
    }

    private void NavigateToLearning(object sender, RoutedEventArgs e)
    {
        ViewModel?.NavigateToAdaptiveLessonCommand.Execute(null);
    }

    private void NavigateToDebug(object sender, RoutedEventArgs e)
    {
        ViewModel?.NavigateToDebugCommand.Execute(null);
    }
}
