using System.Collections.ObjectModel;

namespace HornetStudio.Editor.ViewModels;

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
                RaisePropertyChanged(nameof(IsDialogInteractionAction));
                RaisePropertyChanged(nameof(UsesComboTargetSelection));
                RaisePropertyChanged(nameof(UsesBrowseTargetSelection));
                RaisePropertyChanged(nameof(ShowsFunctionPicker));
            }
        }
    }

    public string TargetPath
    {
        get => _targetPath;
        set
        {
            var normalizedValue = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                normalizedValue = UsesBrowseTargetSelection ? "this" : string.Empty;
            }

            SetProperty(ref _targetPath, normalizedValue);
        }
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

    public bool IsPythonFunctionAction => string.Equals(ActionName, "InvokePythonFunction", System.StringComparison.OrdinalIgnoreCase);

    public bool IsDialogInteractionAction
        => string.Equals(ActionName, "OpenDialog", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(ActionName, "CloseDialog", System.StringComparison.OrdinalIgnoreCase);

    public bool UsesComboTargetSelection => IsPythonFunctionAction || IsDialogInteractionAction;

    public bool UsesBrowseTargetSelection => !UsesComboTargetSelection;

    public bool ShowsFunctionPicker => IsPythonFunctionAction;

    public ObservableCollection<string> EventOptions { get; } = [];

    public ObservableCollection<string> ActionOptions { get; } = [];

    public ObservableCollection<string> TargetOptions { get; } = [];

    public ObservableCollection<string> FunctionOptions { get; } = [];
}
