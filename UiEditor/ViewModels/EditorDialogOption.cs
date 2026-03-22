namespace Amium.UiEditor.ViewModels;

public sealed class EditorDialogOption
{
    public EditorDialogOption(string value, string? label = null)
    {
        Value = value;
        Label = string.IsNullOrWhiteSpace(label) ? value : label;
    }

    public string Value { get; }

    public string Label { get; }
}
