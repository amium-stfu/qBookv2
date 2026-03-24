namespace Amium.UiEditor.ViewModels;

public sealed class AttachItemEditorRow : ObservableObject
{
    private bool _isAttached;

    public string RelativePath { get; init; } = string.Empty;

    public string DisplayLabel => RelativePath;

    public bool IsAttached
    {
        get => _isAttached;
        set => SetProperty(ref _isAttached, value);
    }
}