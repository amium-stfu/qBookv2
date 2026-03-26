using System.Collections.ObjectModel;

namespace Amium.UiEditor.ViewModels;

public sealed class ItemInteractionEditorRow : ObservableObject
{
    private string _eventName = "BodyLeftClick";
    private string _actionName = "OpenValueEditor";
    private string _targetPath = "this";
    private string _argument = string.Empty;

    public string EventName
    {
        get => _eventName;
        set => SetProperty(ref _eventName, string.IsNullOrWhiteSpace(value) ? "BodyLeftClick" : value);
    }

    public string ActionName
    {
        get => _actionName;
        set => SetProperty(ref _actionName, string.IsNullOrWhiteSpace(value) ? "OpenValueEditor" : value);
    }

    public string TargetPath
    {
        get => _targetPath;
        set => SetProperty(ref _targetPath, string.IsNullOrWhiteSpace(value) ? "this" : value);
    }

    public string Argument
    {
        get => _argument;
        set => SetProperty(ref _argument, value ?? string.Empty);
    }

    public ObservableCollection<string> EventOptions { get; } = [];

    public ObservableCollection<string> ActionOptions { get; } = [];

    public ObservableCollection<string> TargetOptions { get; } = [];
}