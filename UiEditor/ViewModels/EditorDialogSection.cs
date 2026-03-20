using System.Collections.ObjectModel;

namespace UiEditor.ViewModels;

public sealed class EditorDialogSection
{
    public EditorDialogSection(string title)
    {
        Title = title;
    }

    public string Title { get; }

    public ObservableCollection<EditorDialogField> Fields { get; } = [];
}
