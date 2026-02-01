using System.Windows;
using System.Windows.Controls;

namespace CatAdaptive.App.Views;

public partial class UploadView : UserControl
{
    public UploadView()
    {
        InitializeComponent();
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && DataContext is ViewModels.UploadViewModel vm)
            {
                vm.SelectedFilePath = files[0];
            }
        }
    }

    private void OnFileDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && files[0].EndsWith(".pptx", System.StringComparison.OrdinalIgnoreCase))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }
}
