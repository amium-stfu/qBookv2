using System.Collections.ObjectModel;

namespace Amium.UiEditor.ViewModels;

public sealed class ItemInteractionEditorRow : ObservableObject
{
    private string _eventName = "BodyLeftClick";
    private string _actionName = "OpenValueEditor";
    private string _targetPath = "this";
    private string _functionName = string.Empty;
    private string _argument = string.Empty;

    public string EventName
    {
        get => _eventName;
        set => SetProperty(ref _eventName, string.IsNullOrWhiteSpace(value) ? "BodyLeftClick" : value);
    }

    public string ActionName
    {
        get => _actionName;
        set
        {
            if (SetProperty(ref _actionName, string.IsNullOrWhiteSpace(value) ? "OpenValueEditor" : value))
            {
                RaisePropertyChanged(nameof(IsPythonFunctionAction));
                RaisePropertyChanged(nameof(IsStandardInteractionAction));
            }
        }
    }

    public string TargetPath
    {
        get => _targetPath;
        set => SetProperty(ref _targetPath, string.IsNullOrWhiteSpace(value) ? "this" : value);
    }

    public string FunctionName
    {
        get => _functionName;
        set => SetProperty(ref _functionName, value ?? string.Empty);
    }

    public string Argument
    {
        get => _argument;
        set => SetProperty(ref _argument, value ?? string.Empty);
    }

    public bool IsPythonFunctionAction => string.Equals(ActionName, "InvokePythonClientFunction", System.StringComparison.OrdinalIgnoreCase)
                                          || string.Equals(ActionName, "InvokePythonFunction", System.StringComparison.OrdinalIgnoreCase);

    public bool IsStandardInteractionAction => !IsPythonFunctionAction;

    public ObservableCollection<string> EventOptions { get; } = [];

    public ObservableCollection<string> ActionOptions { get; } = [];

    public ObservableCollection<string> TargetOptions { get; } = [];

    public ObservableCollection<string> FunctionOptions { get; } = [];
}