using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
namespace HornetStudio.Editor.ViewModels;

public sealed class ItemInteractionEditorRow : ObservableObject
{
    private string _eventName = "BodyLeftClick";
    private string _actionName = "OpenValueEditor";
    private string _targetPath = "this";
    private string _functionName = string.Empty;
    private string _argument = string.Empty;
    private FunctionPickerOption? _selectedRunFunctionOption;

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
                RaisePropertyChanged(nameof(IsRunFunctionAction));
                RaisePropertyChanged(nameof(IsDialogInteractionAction));
                RaisePropertyChanged(nameof(ShowsTargetSelection));
                RaisePropertyChanged(nameof(UsesComboTargetSelection));
                RaisePropertyChanged(nameof(UsesBrowseTargetSelection));
                RaisePropertyChanged(nameof(ShowsFunctionPicker));
                RaisePropertyChanged(nameof(ShowsLegacyFunctionPicker));
                RaisePropertyChanged(nameof(ShowsArgumentEditor));
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
                normalizedValue = ShowsTargetSelection ? "this" : string.Empty;
            }

            SetProperty(ref _targetPath, normalizedValue);
        }
    }

    public string FunctionName
    {
        get => _functionName;
        set
        {
            if (SetProperty(ref _functionName, value ?? string.Empty))
            {
                RaisePropertyChanged(nameof(SelectedRunFunctionOption));
            }
        }
    }

    public string Argument
    {
        get => _argument;
        set => SetProperty(ref _argument, value ?? string.Empty);
    }

    public bool IsPythonFunctionAction => string.Equals(ActionName, "InvokePythonFunction", System.StringComparison.OrdinalIgnoreCase);

    public bool IsRunFunctionAction
        => string.Equals(ActionName, "RunFunction", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(ActionName, "StopFunction", System.StringComparison.OrdinalIgnoreCase);

    public bool IsDialogInteractionAction
        => string.Equals(ActionName, "OpenDialog", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(ActionName, "CloseDialog", System.StringComparison.OrdinalIgnoreCase);

    public bool ShowsTargetSelection => !IsRunFunctionAction;

    public bool UsesComboTargetSelection => IsPythonFunctionAction || IsDialogInteractionAction;

    public bool UsesBrowseTargetSelection => ShowsTargetSelection && !UsesComboTargetSelection;

    public bool ShowsFunctionPicker => IsPythonFunctionAction || IsRunFunctionAction;

    public bool ShowsLegacyFunctionPicker => IsPythonFunctionAction;

    public bool ShowsArgumentEditor => true;

    public FunctionPickerOption? SelectedRunFunctionOption
    {
        get => _selectedRunFunctionOption;
        set
        {
            if (!SetProperty(ref _selectedRunFunctionOption, value))
            {
                return;
            }

            var reference = value?.Reference ?? string.Empty;
            if (!string.Equals(FunctionName, reference, System.StringComparison.Ordinal))
            {
                FunctionName = reference;
            }
        }
    }

    public ObservableCollection<string> EventOptions { get; } = [];

    public ObservableCollection<string> ActionOptions { get; } = [];

    public ObservableCollection<string> TargetOptions { get; } = [];

    public ObservableCollection<string> FunctionOptions { get; } = [];

    public ObservableCollection<FunctionPickerOption> RunFunctionOptions { get; } = [];

    public void SetRunFunctionOptions(IEnumerable<FunctionPickerOption> options, string? selectedReference)
    {
        RunFunctionOptions.Clear();
        foreach (var option in options)
        {
            RunFunctionOptions.Add(option);
        }

        var selected = RunFunctionOptions.FirstOrDefault(option => HornetStudio.Editor.Functions.FunctionRegistry.ReferencesEqual(option.Reference, selectedReference));
        if (selected is null && !string.IsNullOrWhiteSpace(selectedReference))
        {
            selected = new FunctionPickerOption
            {
                Reference = selectedReference.Trim(),
                DisplayText = $"Missing / {selectedReference.Trim()}"
            };
            RunFunctionOptions.Add(selected);
        }

        _selectedRunFunctionOption = selected;
        RaisePropertyChanged(nameof(SelectedRunFunctionOption));
    }
}
