using CatAdaptive.Application.Abstractions;
using Microsoft.Win32;

namespace CatAdaptive.App.Services;

public class DialogService : IDialogService
{
    public string? OpenFile(string filter, string title)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveFile(string filter, string title, string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            Title = title,
            FileName = defaultFileName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
