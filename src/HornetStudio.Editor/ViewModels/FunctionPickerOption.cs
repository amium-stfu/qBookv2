namespace HornetStudio.Editor.ViewModels;

public sealed class FunctionPickerOption
{
    public string Reference { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;

    public override string ToString()
        => string.IsNullOrWhiteSpace(DisplayText)
            ? Reference
            : DisplayText;
}