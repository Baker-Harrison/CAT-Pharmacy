using System.Windows.Controls;
using CatAdaptive.App.ViewModels;

namespace CatAdaptive.App.Views;

public partial class LessonsView : UserControl
{
    public LessonsView()
    {
        InitializeComponent();
        Loaded += LessonsView_Loaded;
    }

    private void LessonsView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LessonsViewModel viewModel)
        {
            _ = viewModel.LoadStatsAsync();
        }
    }
}
