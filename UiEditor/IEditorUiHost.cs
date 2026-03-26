namespace Amium.EditorUi;

public interface IEditorUiHost
{
    bool IsEditMode { get; }

    string? PrimaryTextBrush { get; }

    void OpenItemEditor(object item, double x, double y);

    bool DeleteItem(object item);

    void RefreshPageBindings(string pageName);
}