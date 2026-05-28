using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using HornetStudio.Editor.Models;
using HornetStudio.Editor.ViewModels;
using HornetStudio.Editor.Widgets.Common;

namespace HornetStudio.Editor.Widgets.Workflow;

/// <summary>
/// Represents one editable or preserved workflow step row inside the workflow editor dialog.
/// </summary>
public sealed class FunctionStepEditorRow : ObservableObject
{
    private FunctionStepType _stepType;
    private string _target = string.Empty;
    private string _value = string.Empty;
    private string _valueFrom = string.Empty;
    private string _setValueSummary = string.Empty;
    private string _setValueValidationMessage = string.Empty;
    private SetValueTargetKind _setValueTargetKind;
    private SetValueInlineOperationOption? _selectedSetValueOperation;
    private string _setValueLiteralArgument = string.Empty;
    private string _setValueSeparator = string.Empty;
    private string _setValueSourcePath = string.Empty;
    private bool _isSynchronizingSetValueInlineState;
    private string _millisecondsText = string.Empty;
    private string _targetLog = string.Empty;
    private string _levelText = MonitorLogLevel.Info.ToString();
    private string _text = string.Empty;
    private FunctionStepType _newThenStepType = FunctionStepType.Log;
    private FunctionStepType _newElseStepType = FunctionStepType.Log;
    private FunctionStepType _newWhileStepType = FunctionStepType.Log;
    private bool _requiresPositiveDelay;
    private bool _isUpdatingWhileDelayGuard;

    private FunctionStepEditorRow(FunctionStepType stepType, FunctionStepDefinition? preservedStep)
    {
        _stepType = stepType;
        PreservedStep = preservedStep;
        ConditionEditor = new BooleanConditionEditorViewModel();
        ConditionEditor.PropertyChanged += OnConditionEditorPropertyChanged;
        ThenRows.CollectionChanged += OnBranchRowsChanged;
        ElseRows.CollectionChanged += OnBranchRowsChanged;
        WhileRows.CollectionChanged += OnBranchRowsChanged;
    }

    /// <summary>
    /// Gets the selectable editable workflow step types for the MVP editor.
    /// </summary>
    public static IReadOnlyList<FunctionStepType> EditableStepTypes { get; } =
    [
        FunctionStepType.SetValue,
        FunctionStepType.Delay,
        FunctionStepType.Log,
        FunctionStepType.IfThenElse,
        FunctionStepType.While
    ];

    /// <summary>
    /// Gets or sets the step type represented by this row.
    /// </summary>
    public FunctionStepType StepType
    {
        get => _stepType;
        set
        {
            if (!SetProperty(ref _stepType, value))
            {
                return;
            }

            RaiseComputedPropertiesChanged();
        }
    }

    /// <summary>
    /// Gets or sets the SetValue target path.
    /// </summary>
    public string Target
    {
        get => _target;
        set
        {
            if (!SetProperty(ref _target, value ?? string.Empty))
            {
                return;
            }

            RaiseComputedPropertiesChanged();
        }
    }

    /// <summary>
    /// Gets or sets the SetValue literal value written to the target when <see cref="ValueFrom"/> is not configured.
    /// Setting a non-empty literal value clears <see cref="ValueFrom"/>.
    /// </summary>
    public string Value
    {
        get => _value;
        set
        {
            if (!SetProperty(ref _value, value ?? string.Empty))
            {
                return;
            }

            if (StepType == FunctionStepType.SetValue && !_isSynchronizingSetValueInlineState)
            {
                RefreshSetValueInlineState();
            }

            RaiseComputedPropertiesChanged();
        }
    }

    /// <summary>
    /// Gets or sets the SetValue argument source item path read at runtime.
    /// When non-empty, the current value of this item is written to the target instead of <see cref="Value"/>.
    /// </summary>
    public string ValueFrom
    {
        get => _valueFrom;
        set
        {
            if (!SetProperty(ref _valueFrom, value ?? string.Empty))
            {
                return;
            }

            if (StepType == FunctionStepType.SetValue && !_isSynchronizingSetValueInlineState)
            {
                RefreshSetValueInlineState();
            }

            RaiseComputedPropertiesChanged();
        }
    }

    /// <summary>
    /// Gets the watermark text for the SetValue argument text box.
    /// Shows the configured source path when <see cref="ValueFrom"/> is set.
    /// </summary>
    public string ValueWatermark => string.IsNullOrWhiteSpace(ValueFrom) ? "Value" : $"From: {ValueFrom}";

    /// <summary>
    /// Gets or sets the user-facing SetValue operation summary.
    /// </summary>
    public string SetValueSummary
    {
        get => _setValueSummary;
        set => SetProperty(ref _setValueSummary, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the SetValue validation message.
    /// </summary>
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

    /// <summary>
    /// Gets a value indicating whether the SetValue operation is currently invalid.
    /// </summary>
    public bool HasSetValueValidationError => !string.IsNullOrWhiteSpace(SetValueValidationMessage);

    /// <summary>
    /// Gets or sets the detected SetValue target kind.
    /// </summary>
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

    /// <summary>
    /// Gets a value indicating whether the inline literal editor should be shown for SetValue.
    /// </summary>
    public bool ShowsSetValueLiteralEditor
        => ShowsSetValueFields
            && SelectedSetValueOperation?.UsesSourceItem != true
            && SelectedSetValueOperation?.Kind is not SetValueOperationKind.SetTrue
            && SelectedSetValueOperation?.Kind is not SetValueOperationKind.SetFalse;

    /// <summary>
    /// Gets a value indicating whether the inline separator editor should be shown for SetValue.
    /// </summary>
    public bool ShowsSetValueSeparatorEditor
        => ShowsSetValueFields
            && SelectedSetValueOperation?.Kind == SetValueOperationKind.AppendText
            && SetValueTargetKind == SetValueTargetKind.String;

    /// <summary>
    /// Gets a value indicating whether the inline source picker should be shown for SetValue.
    /// </summary>
    public bool ShowsSetValueSourceEditor => ShowsSetValueFields && SelectedSetValueOperation?.UsesSourceItem == true;

    /// <summary>
    /// Gets the inline SetValue literal watermark for the detected target kind.
    /// </summary>
    public string SetValueLiteralWatermark => SetValueTargetKind switch
    {
        SetValueTargetKind.Numeric => "12.5",
        SetValueTargetKind.Boolean => "true",
        _ => "Value"
    };

    /// <summary>
    /// Gets or sets the selected inline SetValue operation.
    /// </summary>
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
            SyncValueFromInlineSetValueState();
        }
    }

    /// <summary>
    /// Gets or sets the inline SetValue literal argument.
    /// </summary>
    public string SetValueLiteralArgument
    {
        get => _setValueLiteralArgument;
        set
        {
            if (!SetProperty(ref _setValueLiteralArgument, value ?? string.Empty))
            {
                return;
            }

            SyncValueFromInlineSetValueState();
        }
    }

    /// <summary>
    /// Gets or sets the inline SetValue separator.
    /// </summary>
    public string SetValueSeparator
    {
        get => _setValueSeparator;
        set
        {
            if (!SetProperty(ref _setValueSeparator, value ?? string.Empty))
            {
                return;
            }

            SyncValueFromInlineSetValueState();
        }
    }

    /// <summary>
    /// Gets or sets the inline SetValue source item path.
    /// </summary>
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
            SyncValueFromInlineSetValueState();
        }
    }

    /// <summary>
    /// Gets or sets the Delay milliseconds text.
    /// </summary>
    public string MillisecondsText
    {
        get => _millisecondsText;
        set
        {
            if (!SetProperty(ref _millisecondsText, value ?? string.Empty))
            {
                return;
            }

            RaiseComputedPropertiesChanged();
        }
    }

    /// <summary>
    /// Gets or sets the Log target path.
    /// </summary>
    public string TargetLog
    {
        get => _targetLog;
        set
        {
            if (!SetProperty(ref _targetLog, value ?? string.Empty))
            {
                return;
            }

            RaiseComputedPropertiesChanged();
        }
    }

    /// <summary>
    /// Gets or sets the Log level text.
    /// </summary>
    public string LevelText
    {
        get => _levelText;
        set
        {
            if (!SetProperty(ref _levelText, string.IsNullOrWhiteSpace(value) ? MonitorLogLevel.Info.ToString() : value.Trim()))
            {
                return;
            }

            RaiseComputedPropertiesChanged();
        }
    }

    /// <summary>
    /// Gets or sets the Log message text.
    /// </summary>
    public string Text
    {
        get => _text;
        set
        {
            if (!SetProperty(ref _text, value ?? string.Empty))
            {
                return;
            }

            RaiseComputedPropertiesChanged();
        }
    }

    /// <summary>
    /// Gets the shared condition editor for IfThenElse steps.
    /// </summary>
    public BooleanConditionEditorViewModel ConditionEditor { get; }

    /// <summary>
    /// Gets the editable Then branch rows.
    /// </summary>
    public ObservableCollection<FunctionStepEditorRow> ThenRows { get; } = [];

    /// <summary>
    /// Gets the editable Else branch rows.
    /// </summary>
    public ObservableCollection<FunctionStepEditorRow> ElseRows { get; } = [];

    /// <summary>
    /// Gets the editable While body rows.
    /// </summary>
    public ObservableCollection<FunctionStepEditorRow> WhileRows { get; } = [];

    /// <summary>
    /// Gets the available inline SetValue operations.
    /// </summary>
    public ObservableCollection<SetValueInlineOperationOption> SetValueOperationOptions { get; } = [];

    /// <summary>
    /// Gets the available SetValue source item options.
    /// </summary>
    public ObservableCollection<string> SetValueSourceOptions { get; } = [];

    /// <summary>
    /// Gets or sets the selected step type for newly added Then branch rows.
    /// </summary>
    public FunctionStepType NewThenStepType
    {
        get => _newThenStepType;
        set => SetProperty(ref _newThenStepType, value);
    }

    /// <summary>
    /// Gets or sets the selected step type for newly added Else branch rows.
    /// </summary>
    public FunctionStepType NewElseStepType
    {
        get => _newElseStepType;
        set => SetProperty(ref _newElseStepType, value);
    }

    /// <summary>
    /// Gets or sets the selected step type for newly added While body rows.
    /// </summary>
    public FunctionStepType NewWhileStepType
    {
        get => _newWhileStepType;
        set => SetProperty(ref _newWhileStepType, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this row is the required positive Delay guard for a While body.
    /// </summary>
    public bool RequiresPositiveDelay
    {
        get => _requiresPositiveDelay;
        set
        {
            if (!SetProperty(ref _requiresPositiveDelay, value))
            {
                return;
            }

            RaiseComputedPropertiesChanged();
        }
    }

    /// <summary>
    /// Gets the preserved original step when the row is not editable in the MVP editor.
    /// </summary>
    public FunctionStepDefinition? PreservedStep { get; }

    /// <summary>
    /// Gets a value indicating whether this row is preserved read-only content.
    /// </summary>
    public bool IsPreserved => PreservedStep is not null;

    /// <summary>
    /// Gets a value indicating whether the SetValue fields should be shown.
    /// </summary>
    public bool ShowsSetValueFields => !IsPreserved && StepType == FunctionStepType.SetValue;

    /// <summary>
    /// Gets a value indicating whether the Delay fields should be shown.
    /// </summary>
    public bool ShowsDelayFields => !IsPreserved && StepType == FunctionStepType.Delay;

    /// <summary>
    /// Gets a value indicating whether the Log fields should be shown.
    /// </summary>
    public bool ShowsLogFields => !IsPreserved && StepType == FunctionStepType.Log;

    /// <summary>
    /// Gets a value indicating whether the IfThenElse fields should be shown.
    /// </summary>
    public bool ShowsIfThenElseFields => !IsPreserved && StepType == FunctionStepType.IfThenElse;

    /// <summary>
    /// Gets a value indicating whether the While fields should be shown.
    /// </summary>
    public bool ShowsWhileFields => !IsPreserved && StepType == FunctionStepType.While;

    /// <summary>
    /// Gets the current summary shown for the row.
    /// </summary>
    public string Summary => BuildSummary();

    /// <summary>
    /// Gets the compact condition summary shown for IfThenElse rows.
    /// </summary>
    public string ConditionSummary => BuildConditionSummary();

    /// <summary>
    /// Gets the condition dialog button text for IfThenElse rows.
    /// </summary>
    public string ConditionButtonText => string.IsNullOrWhiteSpace(ConditionEditor.FormulaText) ? "Add Condition" : "Condition";

    /// <summary>
    /// Gets the compact Then branch summary.
    /// </summary>
    public string ThenSummary => $"Then: {ThenRows.Count}";

    /// <summary>
    /// Gets a value indicating whether the Then branch contains rows.
    /// </summary>
    public bool HasThenRows => ThenRows.Count > 0;

    /// <summary>
    /// Gets the compact Else branch summary.
    /// </summary>
    public string ElseSummary => $"Else: {ElseRows.Count}";

    /// <summary>
    /// Gets a value indicating whether the Else branch contains rows.
    /// </summary>
    public bool HasElseRows => ElseRows.Count > 0;

    /// <summary>
    /// Gets the compact While body summary.
    /// </summary>
    public string WhileSummary => $"Body: {WhileRows.Count}";

    /// <summary>
    /// Gets a value indicating whether the While body contains rows.
    /// </summary>
    public bool HasWhileRows => WhileRows.Count > 0;

    /// <summary>
    /// Creates one editable row from a supported workflow step.
    /// </summary>
    /// <param name="step">The workflow step to convert.</param>
    /// <returns>The created editor row.</returns>
    public static FunctionStepEditorRow FromStep(FunctionStepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step switch
        {
            FunctionSetValueStepDefinition setValue => CreateSetValueRow(setValue),
            FunctionDelayStepDefinition delay => new FunctionStepEditorRow(FunctionStepType.Delay, preservedStep: null)
            {
                MillisecondsText = delay.Milliseconds.ToString(CultureInfo.InvariantCulture)
            },
            FunctionLogStepDefinition log => new FunctionStepEditorRow(FunctionStepType.Log, preservedStep: null)
            {
                TargetLog = log.TargetLog,
                LevelText = log.Level.ToString(),
                Text = log.Text
            },
            FunctionIfThenElseStepDefinition conditional => CreateConditionalRow(conditional),
            FunctionWhileStepDefinition loop => CreateWhileRow(loop),
            _ => new FunctionStepEditorRow(step.Type, FunctionEditorDefinitionConverter.CloneStep(step))
        };
    }

    /// <summary>
    /// Creates one new editable row for the selected type.
    /// </summary>
    /// <param name="stepType">The step type to initialize.</param>
    /// <returns>The created editor row.</returns>
    public static FunctionStepEditorRow CreateNew(FunctionStepType stepType)
    {
        var row = new FunctionStepEditorRow(stepType, preservedStep: null);
        if (stepType == FunctionStepType.SetValue)
        {
            row.RefreshSetValueInlineState();
        }
        else if (stepType == FunctionStepType.Delay)
        {
            row.MillisecondsText = "1000";
        }
        else if (stepType == FunctionStepType.While)
        {
            var delayRow = CreateNew(FunctionStepType.Delay);
            delayRow.MillisecondsText = "100";
            delayRow.RequiresPositiveDelay = true;
            row.WhileRows.Add(delayRow);
        }

        return row;
    }

    /// <summary>
    /// Creates one detached condition editor clone for dialog-based editing.
    /// </summary>
    /// <returns>The cloned condition editor state.</returns>
    public BooleanConditionEditorViewModel CreateConditionEditorClone()
    {
        return new BooleanConditionEditorViewModel(
            ConditionEditor.FormulaText,
            ConditionEditor.Variables.Select(static variable => new BooleanConditionVariableDefinition
            {
                Name = variable.Name,
                SourcePath = variable.SourcePath
            }));
    }

    /// <summary>
    /// Applies one edited condition state back to the row.
    /// </summary>
    /// <param name="formulaText">The committed formula text.</param>
    /// <param name="variables">The committed variables.</param>
    public void ApplyCondition(string? formulaText, IEnumerable<BooleanConditionVariableDefinition>? variables)
    {
        ConditionEditor.FormulaText = formulaText?.Trim() ?? string.Empty;

        for (var index = ConditionEditor.Variables.Count - 1; index >= 0; index--)
        {
            ConditionEditor.RemoveVariable(ConditionEditor.Variables[index]);
        }

        foreach (var variable in variables ?? Array.Empty<BooleanConditionVariableDefinition>())
        {
            ConditionEditor.AddVariable(variable.Name, variable.SourcePath);
        }

        RaiseComputedPropertiesChanged();
    }

    /// <summary>
    /// Replaces the available SetValue source options and keeps the current source visible.
    /// </summary>
    /// <param name="options">The available source options.</param>
    public void SetSetValueSourceOptions(IEnumerable<string> options)
    {
        SetValueSourceOptions.Clear();
        foreach (var option in options.Where(static option => !string.IsNullOrWhiteSpace(option)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            SetValueSourceOptions.Add(option);
        }

        EnsureCurrentSetValueSourceOption();
    }

    /// <summary>
    /// Refreshes the inline SetValue editor state from the persisted row values.
    /// </summary>
    public void RefreshSetValueInlineState()
    {
        var selectedKind = _selectedSetValueOperation?.Kind;

        SetValueOperationOptions.Clear();
        foreach (var option in SetValueOperationCodec.GetInlineOperationOptions(SetValueTargetKind))
        {
            SetValueOperationOptions.Add(option);
        }

        var persistedOperation = BuildPersistedSetValueOperation();
        var parsed = persistedOperation is null
            ? SetValueOperationCodec.Parse(Value)
            : new SetValueOperationParseResult
            {
                IsValid = true,
                IsStructured = true,
                Operation = persistedOperation
            };

        var inlineOperation = parsed.IsValid
            ? SetValueOperationCodec.ToInlineEditorOperation(parsed.Operation, SetValueTargetKind)
            : new SetValueOperation
            {
                Kind = SetValueOperationKind.SetLiteral,
                LiteralValue = Value,
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

        SetValueSummary = parsed.IsValid
            ? SetValueOperationCodec.GetSummary(parsed.Operation, SetValueTargetKind)
            : SetValueOperationCodec.GetSummary(Value, SetValueTargetKind);

        if (SetValueTargetKind == SetValueTargetKind.Boolean
            && string.IsNullOrWhiteSpace(Value)
            && string.IsNullOrWhiteSpace(ValueFrom)
            && _selectedSetValueOperation is not null)
        {
            SyncValueFromInlineSetValueState();
        }

        RaisePropertyChanged(nameof(SetValueLiteralArgument));
        RaisePropertyChanged(nameof(SetValueSeparator));
        RaisePropertyChanged(nameof(SetValueSourcePath));
        RaisePropertyChanged(nameof(SelectedSetValueOperation));
        RaisePropertyChanged(nameof(ShowsSetValueLiteralEditor));
        RaisePropertyChanged(nameof(ShowsSetValueSeparatorEditor));
        RaisePropertyChanged(nameof(ShowsSetValueSourceEditor));
    }

    private string BuildSummary()
    {
        if (IsPreserved)
        {
            return PreservedStep switch
            {
                FunctionIfThenElseStepDefinition conditional => $"IfThenElse - nested editing is not available in this editor yet. Then: {conditional.Then.Count}, Else: {conditional.Else.Count}.",
                FunctionWhileStepDefinition loop => $"While - nested editing is not available in this editor yet. Body: {loop.Steps.Count}.",
                { } preserved => $"{preserved.Type} - this step is preserved and cannot be edited here.",
                _ => "Preserved step"
            };
        }

        return StepType switch
        {
            FunctionStepType.SetValue => $"Set {BuildInlineText(Target, "target")} -> {BuildInlineText(SetValueSummary, "value")}",
            FunctionStepType.Delay => RequiresPositiveDelay
                ? $"Delay {BuildInlineText(MillisecondsText, "1")} ms (loop guard)"
                : $"Delay {BuildInlineText(MillisecondsText, "0")} ms",
            FunctionStepType.Log => $"Log {BuildInlineText(LevelText, MonitorLogLevel.Info.ToString())} -> {BuildInlineText(TargetLog, "target")} : {BuildExcerpt(Text, 48)}",
            FunctionStepType.IfThenElse => $"{ConditionSummary} {ThenSummary} {ElseSummary}",
            FunctionStepType.While => $"{ConditionSummary} {WhileSummary}",
            _ => StepType.ToString()
        };
    }

    private string BuildConditionSummary()
    {
        if (string.IsNullOrWhiteSpace(ConditionEditor.FormulaText))
        {
            return "No condition configured";
        }

        return $"Condition: {BuildExcerpt(ConditionEditor.FormulaText.Trim(), 72)}";
    }

    private void RaiseComputedPropertiesChanged()
    {
        RaisePropertyChanged(nameof(ShowsSetValueFields));
        RaisePropertyChanged(nameof(ShowsSetValueLiteralEditor));
        RaisePropertyChanged(nameof(ShowsSetValueSeparatorEditor));
        RaisePropertyChanged(nameof(ShowsSetValueSourceEditor));
        RaisePropertyChanged(nameof(ShowsDelayFields));
        RaisePropertyChanged(nameof(ShowsLogFields));
        RaisePropertyChanged(nameof(ShowsIfThenElseFields));
        RaisePropertyChanged(nameof(ShowsWhileFields));
        RaisePropertyChanged(nameof(ValueWatermark));
        RaisePropertyChanged(nameof(SetValueLiteralWatermark));
        RaisePropertyChanged(nameof(Summary));
        RaisePropertyChanged(nameof(ConditionSummary));
        RaisePropertyChanged(nameof(ConditionButtonText));
        RaisePropertyChanged(nameof(ThenSummary));
        RaisePropertyChanged(nameof(HasThenRows));
        RaisePropertyChanged(nameof(ElseSummary));
        RaisePropertyChanged(nameof(HasElseRows));
        RaisePropertyChanged(nameof(WhileSummary));
        RaisePropertyChanged(nameof(HasWhileRows));
    }

    private static FunctionStepEditorRow CreateSetValueRow(FunctionSetValueStepDefinition setValue)
    {
        var row = new FunctionStepEditorRow(FunctionStepType.SetValue, preservedStep: null)
        {
            Target = setValue.Target
        };

        row.ApplySetValueDefinition(setValue.Value, setValue.ValueFrom);
        return row;
    }

    private static FunctionStepEditorRow CreateConditionalRow(FunctionIfThenElseStepDefinition conditional)
    {
        var row = new FunctionStepEditorRow(FunctionStepType.IfThenElse, preservedStep: null);
        row.ConditionEditor.FormulaText = conditional.Condition;
        foreach (var variable in conditional.Variables)
        {
            row.ConditionEditor.AddVariable(variable.Name, variable.SourcePath);
        }

        foreach (var nestedStep in conditional.Then)
        {
            row.ThenRows.Add(FromStep(nestedStep));
        }

        foreach (var nestedStep in conditional.Else)
        {
            row.ElseRows.Add(FromStep(nestedStep));
        }

        return row;
    }

    private static FunctionStepEditorRow CreateWhileRow(FunctionWhileStepDefinition loop)
    {
        var row = new FunctionStepEditorRow(FunctionStepType.While, preservedStep: null);
        row.ConditionEditor.FormulaText = loop.Condition;
        foreach (var variable in loop.Variables)
        {
            row.ConditionEditor.AddVariable(variable.Name, variable.SourcePath);
        }

        foreach (var nestedStep in loop.Steps)
        {
            row.WhileRows.Add(FromStep(nestedStep));
        }

        EnsureWhileDelayGuard(row);
        return row;
    }

    private void OnConditionEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BooleanConditionEditorViewModel.FormulaText)
            or nameof(BooleanConditionEditorViewModel.FormulaStatusMessage)
            or nameof(BooleanConditionEditorViewModel.VariableButtons))
        {
            RaiseComputedPropertiesChanged();
        }
    }

    private void OnBranchRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<FunctionStepEditorRow>())
            {
                item.PropertyChanged += OnNestedRowPropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<FunctionStepEditorRow>())
            {
                item.PropertyChanged -= OnNestedRowPropertyChanged;
            }
        }

        if (StepType == FunctionStepType.While)
        {
            EnsureWhileDelayGuard(this);
        }

        RaiseComputedPropertiesChanged();
    }

    private void OnNestedRowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (StepType != FunctionStepType.While || _isUpdatingWhileDelayGuard)
        {
            return;
        }

        if (e.PropertyName is nameof(StepType)
            or nameof(MillisecondsText)
            or nameof(RequiresPositiveDelay))
        {
            EnsureWhileDelayGuard(this);
            RaiseComputedPropertiesChanged();
        }
    }

    private static void EnsureWhileDelayGuard(FunctionStepEditorRow row)
    {
        if (row.StepType != FunctionStepType.While)
        {
            return;
        }

        row._isUpdatingWhileDelayGuard = true;
        try
        {
            FunctionStepEditorRow? firstPositiveDelay = null;
            foreach (var nestedRow in row.WhileRows)
            {
                nestedRow.RequiresPositiveDelay = false;
                if (nestedRow.StepType != FunctionStepType.Delay)
                {
                    continue;
                }

                if (!int.TryParse(nestedRow.MillisecondsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds) || milliseconds <= 0)
                {
                    continue;
                }

                firstPositiveDelay ??= nestedRow;
            }

            if (firstPositiveDelay is null)
            {
                var delayRow = CreateNew(FunctionStepType.Delay);
                delayRow.MillisecondsText = "100";
                delayRow.RequiresPositiveDelay = true;
                row.WhileRows.Insert(0, delayRow);
                return;
            }

            firstPositiveDelay.RequiresPositiveDelay = true;
        }
        finally
        {
            row._isUpdatingWhileDelayGuard = false;
        }
    }

    private static string BuildInlineText(string? value, string fallback)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return BuildExcerpt(text, 40);
    }

    private void ApplySetValueDefinition(string? value, string? valueFrom)
    {
        _isSynchronizingSetValueInlineState = true;
        _value = value ?? string.Empty;
        _valueFrom = valueFrom ?? string.Empty;
        _isSynchronizingSetValueInlineState = false;

        RefreshSetValueInlineState();
        RaisePropertyChanged(nameof(Value));
        RaisePropertyChanged(nameof(ValueFrom));
        RaiseComputedPropertiesChanged();
    }

    private SetValueOperation? BuildPersistedSetValueOperation()
    {
        if (!string.IsNullOrWhiteSpace(ValueFrom))
        {
            return new SetValueOperation
            {
                Kind = SetValueOperationKind.SetFromItem,
                SourcePath = ValueFrom,
                IsLegacyLiteral = false
            };
        }

        var parsed = SetValueOperationCodec.Parse(Value);
        return parsed.IsValid ? parsed.Operation : null;
    }

    private void SyncValueFromInlineSetValueState()
    {
        if (_isSynchronizingSetValueInlineState || StepType != FunctionStepType.SetValue || SelectedSetValueOperation is null)
        {
            return;
        }

        var serializedValue = SetValueOperationCodec.Serialize(new SetValueOperation
        {
            Kind = SelectedSetValueOperation.Kind,
            LiteralValue = SetValueLiteralArgument,
            Separator = SelectedSetValueOperation.Kind == SetValueOperationKind.AppendText ? SetValueSeparator : string.Empty,
            SourcePath = SetValueSourcePath,
            IsLegacyLiteral = false
        });

        if (string.Equals(_value, serializedValue, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(_valueFrom))
        {
            return;
        }

        _isSynchronizingSetValueInlineState = true;
        _value = serializedValue;
        _valueFrom = string.Empty;
        _isSynchronizingSetValueInlineState = false;

        SetValueSummary = SetValueOperationCodec.GetSummary(serializedValue, SetValueTargetKind);
        RaisePropertyChanged(nameof(Value));
        RaisePropertyChanged(nameof(ValueFrom));
        RaiseComputedPropertiesChanged();
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

    private static string BuildExcerpt(string? value, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..Math.Max(0, maxLength - 3)] + "...";
    }
}

/// <summary>
/// Converts between persisted workflow definitions and workflow editor row state.
/// </summary>
public static class FunctionEditorDefinitionConverter
{
    /// <summary>
    /// Creates editor rows from one persisted workflow definition.
    /// </summary>
    /// <param name="definition">The workflow definition to convert.</param>
    /// <returns>The editor rows in workflow order.</returns>
    public static IReadOnlyList<FunctionStepEditorRow> CreateRows(FunctionDefinition? definition)
    {
        return definition?.Steps.Select(FunctionStepEditorRow.FromStep).ToArray() ?? Array.Empty<FunctionStepEditorRow>();
    }

    /// <summary>
    /// Builds one workflow definition from editor rows.
    /// </summary>
    /// <param name="workflowName">The workflow display name.</param>
    /// <param name="rows">The editor rows.</param>
    /// <param name="definition">The created workflow definition.</param>
    /// <param name="errorMessage">The validation error message when conversion fails.</param>
    /// <returns><see langword="true"/> when conversion succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryBuildDefinition(string workflowName, IEnumerable<FunctionStepEditorRow> rows, out FunctionDefinition? definition, out string errorMessage)
    {
        definition = null;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(workflowName))
        {
            errorMessage = "Workflow name is required.";
            return false;
        }

        if (!TryBuildRows(rows?.ToArray() ?? Array.Empty<FunctionStepEditorRow>(), "Workflow step", out var stepList, out errorMessage))
        {
            return false;
        }

        definition = new FunctionDefinition
        {
            Name = workflowName.Trim(),
            Steps = stepList
        };
        return true;
    }

    /// <summary>
    /// Creates a deep clone of a workflow step definition.
    /// </summary>
    /// <param name="step">The workflow step to clone.</param>
    /// <returns>The cloned step.</returns>
    public static FunctionStepDefinition CloneStep(FunctionStepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step switch
        {
            FunctionSetValueStepDefinition setValue => new FunctionSetValueStepDefinition
            {
                Target = setValue.Target,
                Value = setValue.Value,
                ValueFrom = setValue.ValueFrom
            },
            FunctionDelayStepDefinition delay => new FunctionDelayStepDefinition
            {
                Milliseconds = delay.Milliseconds
            },
            FunctionLogStepDefinition log => new FunctionLogStepDefinition
            {
                TargetLog = log.TargetLog,
                Level = log.Level,
                Text = log.Text
            },
            FunctionIfThenElseStepDefinition conditional => new FunctionIfThenElseStepDefinition
            {
                Condition = conditional.Condition,
                Variables = conditional.Variables.Select(static variable => new BooleanConditionVariableDefinition
                {
                    Name = variable.Name,
                    SourcePath = variable.SourcePath
                }).ToList(),
                Then = conditional.Then.Select(CloneStep).ToList(),
                Else = conditional.Else.Select(CloneStep).ToList()
            },
            FunctionWhileStepDefinition loop => new FunctionWhileStepDefinition
            {
                Condition = loop.Condition,
                Variables = loop.Variables.Select(static variable => new BooleanConditionVariableDefinition
                {
                    Name = variable.Name,
                    SourcePath = variable.SourcePath
                }).ToList(),
                Steps = loop.Steps.Select(CloneStep).ToList()
            },
            _ => throw new InvalidOperationException($"Unsupported workflow step type '{step.GetType().Name}'.")
        };
    }

    private static bool TryBuildRows(IEnumerable<FunctionStepEditorRow> rows, string rowPrefix, out List<FunctionStepDefinition> steps, out string errorMessage)
    {
        steps = [];
        errorMessage = string.Empty;
        var orderedRows = rows.ToArray();
        for (var index = 0; index < orderedRows.Length; index++)
        {
            var row = orderedRows[index];
            if (row.IsPreserved)
            {
                if (row.PreservedStep is null)
                {
                    errorMessage = $"{rowPrefix} {index + 1} could not be preserved.";
                    return false;
                }

                steps.Add(CloneStep(row.PreservedStep));
                continue;
            }

            if (!TryBuildStep(row, $"{rowPrefix} {index + 1}", out var step, out errorMessage))
            {
                return false;
            }

            steps.Add(step!);
        }

        return true;
    }

    private static bool TryBuildStep(FunctionStepEditorRow row, string prefix, out FunctionStepDefinition? step, out string errorMessage)
    {
        step = null;
        errorMessage = string.Empty;

        switch (row.StepType)
        {
            case FunctionStepType.SetValue:
                if (string.IsNullOrWhiteSpace(row.Target))
                {
                    errorMessage = $"{prefix}: target is required for SetValue.";
                    return false;
                }

                var setValueSourcePath = row.SelectedSetValueOperation?.UsesSourceItem == true
                    ? row.SetValueSourcePath?.Trim() ?? string.Empty
                    : string.Empty;
                var setValueValue = row.Value ?? string.Empty;
                if (row.SelectedSetValueOperation?.UsesSourceItem == true)
                {
                    setValueValue = SetValueOperationCodec.Serialize(new SetValueOperation
                    {
                        Kind = SetValueOperationKind.SetFromItem,
                        SourcePath = setValueSourcePath,
                        IsLegacyLiteral = false
                    });
                }

                step = new FunctionSetValueStepDefinition
                {
                    Target = row.Target.Trim(),
                    Value = setValueValue,
                    ValueFrom = string.Empty
                };
                return true;

            case FunctionStepType.Delay:
                if (!int.TryParse(row.MillisecondsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds)
                    || milliseconds < 0
                    || (row.RequiresPositiveDelay && milliseconds < 1))
                {
                    errorMessage = row.RequiresPositiveDelay
                        ? $"{prefix}: milliseconds must be an integer greater than or equal to 1 for a While delay guard."
                        : $"{prefix}: milliseconds must be a non-negative integer for Delay.";
                    return false;
                }

                step = new FunctionDelayStepDefinition
                {
                    Milliseconds = milliseconds
                };
                return true;

            case FunctionStepType.Log:
                if (string.IsNullOrWhiteSpace(row.TargetLog))
                {
                    errorMessage = $"{prefix}: target log is required for Log.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(row.Text))
                {
                    errorMessage = $"{prefix}: text is required for Log.";
                    return false;
                }

                if (!Enum.TryParse<MonitorLogLevel>(row.LevelText, ignoreCase: true, out var level))
                {
                    errorMessage = $"{prefix}: log level '{row.LevelText}' is not supported.";
                    return false;
                }

                step = new FunctionLogStepDefinition
                {
                    TargetLog = row.TargetLog.Trim(),
                    Level = level,
                    Text = row.Text
                };
                return true;

            case FunctionStepType.IfThenElse:
                if (string.IsNullOrWhiteSpace(row.ConditionEditor.FormulaText))
                {
                    errorMessage = $"{prefix}: condition is required for IfThenElse.";
                    return false;
                }

                if (!row.ConditionEditor.TryBuildVariables(out var variables, out var conditionVariableError))
                {
                    errorMessage = $"{prefix}: {conditionVariableError}";
                    return false;
                }

                if (row.ThenRows.Count == 0)
                {
                    errorMessage = $"{prefix}: Then branch requires at least one step.";
                    return false;
                }

                if (!TryBuildRows(row.ThenRows, $"{prefix} then step", out var thenSteps, out errorMessage))
                {
                    return false;
                }

                if (!TryBuildRows(row.ElseRows, $"{prefix} else step", out var elseSteps, out errorMessage))
                {
                    return false;
                }

                step = new FunctionIfThenElseStepDefinition
                {
                    Condition = row.ConditionEditor.FormulaText.Trim(),
                    Variables = variables,
                    Then = thenSteps,
                    Else = elseSteps
                };
                return true;

            case FunctionStepType.While:
                if (string.IsNullOrWhiteSpace(row.ConditionEditor.FormulaText))
                {
                    errorMessage = $"{prefix}: condition is required for While.";
                    return false;
                }

                if (!row.ConditionEditor.TryBuildVariables(out var whileVariables, out var whileConditionVariableError))
                {
                    errorMessage = $"{prefix}: {whileConditionVariableError}";
                    return false;
                }

                if (row.WhileRows.Count == 0)
                {
                    errorMessage = $"{prefix}: While body requires at least one step.";
                    return false;
                }

                if (!TryBuildRows(row.WhileRows, $"{prefix} while step", out var whileSteps, out errorMessage))
                {
                    return false;
                }

                if (!whileSteps.OfType<FunctionDelayStepDefinition>().Any(static step => step.Milliseconds > 0))
                {
                    errorMessage = $"{prefix}: While body requires at least one positive Delay step.";
                    return false;
                }

                step = new FunctionWhileStepDefinition
                {
                    Condition = row.ConditionEditor.FormulaText.Trim(),
                    Variables = whileVariables,
                    Steps = whileSteps
                };
                return true;

            default:
                errorMessage = $"{prefix}: step type '{row.StepType}' is not supported in this editor.";
                return false;
        }
    }
}