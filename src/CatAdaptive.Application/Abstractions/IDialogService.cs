namespace CatAdaptive.Application.Abstractions;

public interface IDialogService
{
    string? OpenFile(string filter, string title);
    string? SaveFile(string filter, string title, string defaultFileName);
}
