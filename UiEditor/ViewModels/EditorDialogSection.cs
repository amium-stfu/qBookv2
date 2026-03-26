using System.Collections.ObjectModel;

namespace Amium.UiEditor.ViewModels;

public sealed class EditorDialogSection : ObservableObject
{
    private bool _isExpanded;

    public EditorDialogSection(string title, bool isExpanded = false)
    {
        Title = title;
        _isExpanded = isExpanded;
    }

    public string Title { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetProperty(ref _isExpanded, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(ToggleGlyph));
        }
    }

    public string ToggleGlyph => IsExpanded ? "▼" : "▶";

    public ObservableCollection<EditorDialogField> Fields { get; } = [];
}
