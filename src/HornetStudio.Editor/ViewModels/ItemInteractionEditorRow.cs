using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HornetStudio.Editor.Models;
namespace HornetStudio.Editor.ViewModels;

public sealed class ItemInteractionEditorRow : ObservableObject
{
    private string _eventName = "BodyLeftClick";
    private string _actionName = "OpenValueEditor";
    private string _targetPath = "this";
    private string _functionName = string.Empty;
    private string _argument = string.Empty;
    private string _setValueSummary = string.Empty;
    private string _setValueValidationMessage = string.Empty;
    private SetValueTargetKind _setValueTargetKind;
    private FunctionPickerOption? _selectedRunFunctionOption;
    private SetValueInlineOperationOption? _selectedSetValueOperation;
    private string _setValueLiteralArgument = string.Empty;
    private string _setValueSeparator = string.Empty;
    private string _setValueSourcePath = string.Empty;
    private bool _isSynchronizingSetValueInlineState;

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
                RaisePropertyChanged(nameof(IsSetValueAction));
                RaisePropertyChanged(nameof(ShowsSetValueEditor));
                RaisePropertyChanged(nameof(ShowsSetValueLiteralEditor));
                RaisePropertyChanged(nameof(ShowsSetValueSeparatorEditor));
                RaisePropertyChanged(nameof(ShowsSetValueSourceEditor));
                RefreshSetValueInlineState();
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
        set
        {
            if (SetProperty(ref _argument, value ?? string.Empty))
            {
                RaisePropertyChanged(nameof(ShowsSetValueEditor));

                if (!_isSynchronizingSetValueInlineState)
                {
                    RefreshSetValueInlineState();
                }
            }
        }
    }

    public string SetValueSummary
    {
        get => _setValueSummary;
        set => SetProperty(ref _setValueSummary, value ?? string.Empty);
    }

    public string SetValueValidationMessage
    {
        get => _setValueValidationMessage;
        set
        {
            if (SetProperty(ref _setValueValidationMessage, value ?? string.Empty))
            {
                RaisePropertyChanged(nameof(HasSetValueValidationError));
            }
        }
    }

    public bool HasSetValueValidationError => !string.IsNullOrWhiteSpace(SetValueValidationMessage);

    public SetValueTargetKind SetValueTargetKind
    {
        get => _setValueTargetKind;
        set
        {
            if (SetProperty(ref _setValueTargetKind, value))
            {
                RaisePropertyChanged(nameof(SetValueLiteralWatermark));
                RefreshSetValueInlineState();
            }
        }
    }

    public bool IsPythonFunctionAction => string.Equals(ActionName, "InvokePythonFunction", System.StringComparison.OrdinalIgnoreCase);

    public bool IsSetValueAction => string.Equals(ActionName, "SetValue", System.StringComparison.OrdinalIgnoreCase);

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

    public bool ShowsRunFunctionPicker => IsRunFunctionAction;

    public bool ShowsSetValueFunctionPicker => IsSetValueAction;

    public bool ShowsArgumentEditor => !IsSetValueAction;

    public bool ShowsSetValueEditor => IsSetValueAction;

    public bool ShowsFunctionPlaceholder => !ShowsFunctionPicker && !IsSetValueAction;

    public bool ShowsSetValueLiteralEditor
        => IsSetValueAction
            && SelectedSetValueOperation?.UsesSourceItem != true
            && SelectedSetValueOperation?.Kind is not SetValueOperationKind.SetTrue
            && SelectedSetValueOperation?.Kind is not SetValueOperationKind.SetFalse;

    public bool ShowsSetValueSeparatorEditor
        => IsSetValueAction
            && SelectedSetValueOperation?.Kind == SetValueOperationKind.AppendText
            && SetValueTargetKind == SetValueTargetKind.String;

    public bool ShowsSetValueSourceEditor => IsSetValueAction && SelectedSetValueOperation?.UsesSourceItem == true;

    public string SetValueLiteralWatermark => SetValueTargetKind switch
    {
        SetValueTargetKind.Numeric => "12.5",
        SetValueTargetKind.Boolean => "true",
        _ => "Value"
    };

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

    public SetValueInlineOperationOption? SelectedSetValueOperation
    {
        get => _selectedSetValueOperation;
        set
        {
            if (!SetProperty(ref _selectedSetValueOperation, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(ShowsSetValueLiteralEditor));
            RaisePropertyChanged(nameof(ShowsSetValueSeparatorEditor));
            RaisePropertyChanged(nameof(ShowsSetValueSourceEditor));
            SyncArgumentFromInlineSetValueState();
        }
    }

    public string SetValueLiteralArgument
    {
        get => _setValueLiteralArgument;
        set
        {
            if (!SetProperty(ref _setValueLiteralArgument, value ?? string.Empty))
            {
                return;
            }

            SyncArgumentFromInlineSetValueState();
        }
    }

    public string SetValueSeparator
    {
        get => _setValueSeparator;
        set
        {
            if (!SetProperty(ref _setValueSeparator, value ?? string.Empty))
            {
                return;
            }

            SyncArgumentFromInlineSetValueState();
        }
    }

    public string SetValueSourcePath
    {
        get => _setValueSourcePath;
        set
        {
            var normalizedValue = value ?? string.Empty;
            if (!SetProperty(ref _setValueSourcePath, normalizedValue))
            {
                return;
            }

            EnsureCurrentSetValueSourceOption();
            SyncArgumentFromInlineSetValueState();
        }
    }

    public ObservableCollection<string> EventOptions { get; } = [];

    public ObservableCollection<string> ActionOptions { get; } = [];

    public ObservableCollection<string> TargetOptions { get; } = [];

    public ObservableCollection<string> FunctionOptions { get; } = [];

    public ObservableCollection<FunctionPickerOption> RunFunctionOptions { get; } = [];

    public ObservableCollection<SetValueInlineOperationOption> SetValueOperationOptions { get; } = [];

    public ObservableCollection<string> SetValueSourceOptions { get; } = [];

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

    public void SetSetValueSourceOptions(IEnumerable<string> options)
    {
        SetValueSourceOptions.Clear();
        foreach (var option in options.Where(static option => !string.IsNullOrWhiteSpace(option)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            SetValueSourceOptions.Add(option);
        }

        EnsureCurrentSetValueSourceOption();
    }

    private void RefreshSetValueInlineState()
    {
        var selectedKind = _selectedSetValueOperation?.Kind;

        SetValueOperationOptions.Clear();
        foreach (var option in SetValueOperationCodec.GetInlineOperationOptions(SetValueTargetKind))
        {
            SetValueOperationOptions.Add(option);
        }

        var parsed = SetValueOperationCodec.Parse(Argument);
        var inlineOperation = parsed.IsValid
            ? SetValueOperationCodec.ToInlineEditorOperation(parsed.Operation, SetValueTargetKind)
            : new SetValueOperation
            {
                Kind = SetValueOperationKind.SetLiteral,
                LiteralValue = Argument,
                IsLegacyLiteral = true
            };

        _isSynchronizingSetValueInlineState = true;
        _setValueLiteralArgument = inlineOperation.LiteralValue ?? string.Empty;
        _setValueSeparator = inlineOperation.Separator ?? string.Empty;
        _setValueSourcePath = inlineOperation.SourcePath ?? string.Empty;
        var preferredKind = SetValueOperationOptions.Any(option => option.Kind == inlineOperation.Kind)
            ? inlineOperation.Kind
            : selectedKind is not null && SetValueOperationOptions.Any(option => option.Kind == selectedKind)
                ? selectedKind.Value
                : SetValueOperationOptions.FirstOrDefault()?.Kind ?? SetValueOperationKind.SetLiteral;
        _selectedSetValueOperation = SetValueOperationOptions.FirstOrDefault(option => option.Kind == preferredKind);
        EnsureCurrentSetValueSourceOption();
        _isSynchronizingSetValueInlineState = false;

        if (SetValueTargetKind == SetValueTargetKind.Boolean
            && string.IsNullOrWhiteSpace(Argument)
            && _selectedSetValueOperation is not null)
        {
            SyncArgumentFromInlineSetValueState();
        }

        RaisePropertyChanged(nameof(SetValueLiteralArgument));
        RaisePropertyChanged(nameof(SetValueSeparator));
        RaisePropertyChanged(nameof(SetValueSourcePath));
        RaisePropertyChanged(nameof(SelectedSetValueOperation));
        RaisePropertyChanged(nameof(ShowsSetValueLiteralEditor));
        RaisePropertyChanged(nameof(ShowsSetValueSeparatorEditor));
        RaisePropertyChanged(nameof(ShowsSetValueSourceEditor));
    }

    private void SyncArgumentFromInlineSetValueState()
    {
        if (_isSynchronizingSetValueInlineState || !IsSetValueAction || SelectedSetValueOperation is null)
        {
            return;
        }

        var serializedArgument = SetValueOperationCodec.Serialize(new SetValueOperation
        {
            Kind = SelectedSetValueOperation.Kind,
            LiteralValue = SetValueLiteralArgument,
            Separator = SelectedSetValueOperation.Kind == SetValueOperationKind.AppendText ? SetValueSeparator : string.Empty,
            SourcePath = SetValueSourcePath,
            IsLegacyLiteral = false
        });

        if (string.Equals(Argument, serializedArgument, StringComparison.Ordinal))
        {
            return;
        }

        _isSynchronizingSetValueInlineState = true;
        Argument = serializedArgument;
        _isSynchronizingSetValueInlineState = false;
    }

    private void EnsureCurrentSetValueSourceOption()
    {
        if (string.IsNullOrWhiteSpace(SetValueSourcePath)
            || SetValueSourceOptions.Contains(SetValueSourcePath, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        SetValueSourceOptions.Add(SetValueSourcePath);
    }
}
