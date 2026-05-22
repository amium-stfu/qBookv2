using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HornetStudio.Editor.Helpers;
using HornetStudio.Editor.Models;
using HornetStudio.Editor.ViewModels;
using HornetStudio.Host.Python.Client;

namespace HornetStudio.Editor.Widgets;

public partial class MonitorEditorDialogWindow : Window
{
    private TextBox? _formulaEditorTextBox;
    private readonly MainWindowViewModel? _mainWindowViewModel;
    private readonly FolderItemModel _ownerItem = new();
    private IReadOnlyList<string> _sourceOptions = Array.Empty<string>();
    private IReadOnlyList<string> _targetLogOptions = Array.Empty<string>();

    public MonitorEditorDialogWindow()
    {
        ViewModel = new MonitorEditorDialogViewModel(null, new FolderItemModel(), null, Array.Empty<string>());
        DataContext = ViewModel;
        InitializeComponent();
        _formulaEditorTextBox = this.FindControl<TextBox>("FormulaEditorTextBox");
    }

    public MonitorEditorDialogWindow(MainWindowViewModel? mainWindowViewModel, FolderItemModel ownerItem, MonitorDefinition? definition, IEnumerable<string> sourceOptions, IEnumerable<string> targetLogOptions)
        : this()
    {
        _mainWindowViewModel = mainWindowViewModel;
        _ownerItem = ownerItem;
        _sourceOptions = sourceOptions.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        _targetLogOptions = targetLogOptions.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).Prepend(string.Empty).ToArray();
        ViewModel = new MonitorEditorDialogViewModel(mainWindowViewModel, ownerItem, definition, _targetLogOptions);
        DataContext = ViewModel;
    }

    public MonitorEditorDialogViewModel ViewModel { get; private set; }

    public static Task<MonitorDefinition?> ShowAsync(Window owner, MainWindowViewModel? mainWindowViewModel, FolderItemModel ownerItem, MonitorDefinition? definition, IEnumerable<string> sourceOptions, IEnumerable<string> targetLogOptions)
    {
        var dialog = new MonitorEditorDialogWindow(mainWindowViewModel, ownerItem, definition, sourceOptions, targetLogOptions);
        return dialog.ShowDialog<MonitorDefinition?>(owner);
    }

    private async void OnPickSourceClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new TargetTreeSelectionDialogWindow(_mainWindowViewModel, _sourceOptions, ViewModel.SourcePath, _ownerItem.FolderName);
        await dialog.ShowDialog(this);
        if (!string.IsNullOrWhiteSpace(dialog.CommittedSelection))
        {
            ViewModel.SourcePath = dialog.CommittedSelection;
        }

        e.Handled = true;
    }

    private async void OnPickTargetLogClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new TargetTreeSelectionDialogWindow(_mainWindowViewModel, _targetLogOptions, string.Empty, _ownerItem.FolderName);
        await dialog.ShowDialog(this);
        if (dialog.CommittedSelection is not null)
        {
            ViewModel.AddAction(dialog.CommittedSelection);
        }

        e.Handled = true;
    }

    private async void OnPickActionTargetLogClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: MonitorActionEntryViewModel action })
        {
            return;
        }

        var options = GetActionTargetOptions(action);
        var selectedValue = action.IsWriteLogAction ? action.TargetLog : action.TargetPath;
        var dialog = new TargetTreeSelectionDialogWindow(_mainWindowViewModel, options, selectedValue, _ownerItem.FolderName);
        await dialog.ShowDialog(this);
        if (dialog.CommittedSelection is not null)
        {
            if (action.IsWriteLogAction)
            {
                action.TargetLog = dialog.CommittedSelection;
            }
            else
            {
                action.TargetPath = dialog.CommittedSelection;
            }
        }

        e.Handled = true;
    }

    private IReadOnlyList<string> GetActionTargetOptions(MonitorActionEntryViewModel action)
    {
        return action.SelectedActionType switch
        {
            nameof(MonitorActionType.WriteLog) => _targetLogOptions,
            nameof(MonitorActionType.InvokeFunction) => PythonClientRuntimeRegistry.GetRegisteredTargetPaths()
                .Select(ApplicationExplorerRuntime.ToPersistedInteractionTargetPath)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            _ => _sourceOptions
        };
    }

    private void OnAddVariableClicked(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddVariable();
        e.Handled = true;
    }

    private void OnAddActionClicked(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddAction();
        e.Handled = true;
    }

    private async void OnPickVariableSourceClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: CustomSignalVariableEntryViewModel variable })
        {
            return;
        }

        var dialog = new TargetTreeSelectionDialogWindow(_mainWindowViewModel, _sourceOptions, variable.SourcePath, _ownerItem.FolderName);
        await dialog.ShowDialog(this);
        if (!string.IsNullOrWhiteSpace(dialog.CommittedSelection))
        {
            variable.SourcePath = dialog.CommittedSelection;
        }

        e.Handled = true;
    }

    private void OnRemoveVariableClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: CustomSignalVariableEntryViewModel variable })
        {
            return;
        }

        ViewModel.RemoveVariable(variable);
        e.Handled = true;
    }

    private void OnRemoveActionClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: MonitorActionEntryViewModel action })
        {
            return;
        }

        ViewModel.RemoveAction(action);
        e.Handled = true;
    }

    private void OnInsertFormulaTokenClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: FormulaInsertButtonDefinition token })
        {
            return;
        }

        var caretIndex = _formulaEditorTextBox?.CaretIndex ?? ViewModel.FormulaText.Length;
        var nextCaretIndex = ViewModel.InsertFormulaToken(token.Token, caretIndex, token.CaretBacktrack);
        if (_formulaEditorTextBox is not null)
        {
            _formulaEditorTextBox.Focus();
            _formulaEditorTextBox.CaretIndex = Math.Clamp(nextCaretIndex, 0, _formulaEditorTextBox.Text?.Length ?? 0);
        }

        e.Handled = true;
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryBuildDefinition(out var definition, out var errorMessage))
        {
            ViewModel.ErrorMessage = errorMessage;
            return;
        }

        Close(definition);
        e.Handled = true;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close((MonitorDefinition?)null);
        e.Handled = true;
    }
}

public sealed class MonitorEditorDialogViewModel : ObservableObject
{
    private static readonly IReadOnlyList<string> ModeOptions = Enum.GetNames<MonitorRuleMode>();
    private static readonly IReadOnlyList<string> LogLevelOptions = Enum.GetNames<MonitorLogLevel>();
    private readonly FolderItemModel _ownerItem;
    private readonly string _originalName;
    private string _selectedMode = MonitorRuleMode.Default.ToString();
    private string _name = string.Empty;
    private string _sourcePath = string.Empty;
    private string _refreshRateMsText = "1000";
    private string _timeoutMsText = string.Empty;
    private string _lowerLimit = string.Empty;
    private string _upperLimit = string.Empty;
    private string _inhibitMsText = string.Empty;
    private string _formulaText = string.Empty;
    private string _eventIdText = "0";
    private string _eventText = string.Empty;
    private string _selectedLogLevel = MonitorLogLevel.Warning.ToString();
    private string _errorMessage = string.Empty;
    private string _formulaStatusMessage = string.Empty;
    private string _formulaStatusBrush = "#5E6777";

    public MonitorEditorDialogViewModel(MainWindowViewModel? mainWindowViewModel, FolderItemModel ownerItem, MonitorDefinition? definition, IEnumerable<string> targetLogOptions)
    {
        _ownerItem = ownerItem;
        _originalName = definition?.Name ?? string.Empty;
        DialogBackground = mainWindowViewModel?.DialogBackground ?? "#E3E5EE";
        SectionBackground = mainWindowViewModel?.EditorDialogSectionContentBackground ?? "#EEF3F8";
        BorderColor = mainWindowViewModel?.CardBorderBrush ?? "#CBD5E1";
        ParameterHoverColor = mainWindowViewModel?.ParameterHoverColor ?? "#93C5FD";
        PrimaryTextBrush = mainWindowViewModel?.PrimaryTextBrush ?? "#111827";
        SecondaryTextBrush = mainWindowViewModel?.SecondaryTextBrush ?? "#5E6777";
        SectionContentBackground = mainWindowViewModel?.EditorDialogSectionContentBackground ?? "#EEF3F8";
        EditorBackground = mainWindowViewModel?.ParameterEditBackgrundColor ?? "#FFFFFF";
        EditorForeground = mainWindowViewModel?.ParameterEditForeColor ?? "#111827";
        ButtonBackground = mainWindowViewModel?.EditPanelButtonBackground ?? "#F8FAFC";
        ButtonBorderBrush = mainWindowViewModel?.EditPanelButtonBorderBrush ?? "#CBD5E1";
        ButtonForeground = mainWindowViewModel?.PrimaryTextBrush ?? "#111827";
        TargetLogOptions = targetLogOptions.ToArray();
        Variables.CollectionChanged += OnVariablesCollectionChanged;
        Actions.CollectionChanged += OnActionsCollectionChanged;
        OperatorButtons = CreateOperatorButtons();

        if (definition is null)
        {
            Name = GenerateNextRuleName();
            EventIdText = GenerateNextEventId().ToString(CultureInfo.InvariantCulture);
            UpdateFormulaValidation();
            return;
        }

        Name = definition.Name;
        SourcePath = definition.SourcePath;
        RefreshRateMsText = definition.RefreshRateMs.ToString(CultureInfo.InvariantCulture);
        TimeoutMsText = definition.TimeoutMs?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        SelectedMode = definition.Mode.ToString();
        LowerLimit = definition.LowerLimit;
        UpperLimit = definition.UpperLimit;
        InhibitMsText = definition.InhibitMs > 0 ? definition.InhibitMs.ToString(CultureInfo.InvariantCulture) : string.Empty;
        FormulaText = definition.CustomFormula;
        EventIdText = definition.EventId.ToString(CultureInfo.InvariantCulture);
        EventText = definition.EventText;
        SelectedLogLevel = definition.LogLevel.ToString();
        foreach (var variable in definition.CustomVariables)
        {
            AddVariable(variable.Name, variable.SourcePath);
        }

        foreach (var action in definition.Actions)
        {
            AddAction(action.Trigger.ToString(), action.ActionType.ToString(), action.TargetLog, action.TargetPath, action.FunctionName, action.Argument);
        }

        if (Actions.Count == 0 && !string.IsNullOrWhiteSpace(definition.TargetLog))
        {
            AddAction(definition.TargetLog);
        }

        UpdateFormulaValidation();
    }

    public string DialogBackground { get; }
    
    public string SectionBackground { get; }

    public string BorderColor { get; }

    public string ParameterHoverColor { get; }

    public string PrimaryTextBrush { get; }

    public string SecondaryTextBrush { get; }

    public string SectionContentBackground { get; }
    
    public string EditorBackground { get; }
    
    public string EditorForeground { get; }
    
    public string ButtonBackground { get; }
    
    public string ButtonBorderBrush { get; }
    
    public string ButtonForeground { get; }
    
    public string ValidationErrorBrush => "#B42318";

    public IReadOnlyList<string> AvailableModeOptions => ModeOptions;

    public IReadOnlyList<string> TargetLogOptions { get; }

    public IReadOnlyList<string> AvailableLogLevelOptions => LogLevelOptions;

    public ObservableCollection<CustomSignalVariableEntryViewModel> Variables { get; } = [];

    public ObservableCollection<MonitorActionEntryViewModel> Actions { get; } = [];

    public IReadOnlyList<FormulaInsertButtonDefinition> OperatorButtons { get; }

    public IReadOnlyList<FormulaInsertButtonDefinition> VariableButtons =>
    [
        .. Variables
            .Where(static variable => !string.IsNullOrWhiteSpace(variable.Name))
            .Select(variable => new FormulaInsertButtonDefinition(variable.Name.Trim(), $"{{{variable.Name.Trim()}}}"))
    ];

    public string ExampleText => "Examples: ({A} > 10) AND {Enabled}   or   !{Fault}";

    public string FormulaPanelBackground => SectionContentBackground;

    public string AddVariableIconPath => SvgIconCache.ResolvePath("avares://HornetStudio.Editor/EditorIcons/circle-plus-solid-full.svg", ButtonForeground)
        ?? "avares://HornetStudio.Editor/EditorIcons/circle-plus-solid-full.svg";

    public string TokenButtonForeground => ButtonForeground;

    public string TokenButtonBackground => ButtonBackground;

    public string TokenButtonBorderBrush => ButtonBorderBrush;

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                RaisePropertyChanged(nameof(PreviewPath));
            }
        }
    }

    public string SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value ?? MonitorRuleMode.Default.ToString()))
            {
                RaisePropertyChanged(nameof(IsDefaultMode));
                RaisePropertyChanged(nameof(IsCustomMode));
                RaisePropertyChanged(nameof(ShowSourcePicker));
                RaisePropertyChanged(nameof(ShowFormulaStatus));
                UpdateFormulaValidation();
            }
        }
    }

    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value ?? string.Empty);
    }

    public string RefreshRateMsText
    {
        get => _refreshRateMsText;
        set => SetProperty(ref _refreshRateMsText, value ?? string.Empty);
    }

    public string TimeoutMsText
    {
        get => _timeoutMsText;
        set => SetProperty(ref _timeoutMsText, value ?? string.Empty);
    }

    public string LowerLimit
    {
        get => _lowerLimit;
        set => SetProperty(ref _lowerLimit, value ?? string.Empty);
    }

    public string UpperLimit
    {
        get => _upperLimit;
        set => SetProperty(ref _upperLimit, value ?? string.Empty);
    }

    public string InhibitMsText
    {
        get => _inhibitMsText;
        set => SetProperty(ref _inhibitMsText, value ?? string.Empty);
    }

    public string FormulaText
    {
        get => _formulaText;
        set
        {
            if (SetProperty(ref _formulaText, value ?? string.Empty))
            {
                UpdateFormulaValidation();
            }
        }
    }

    public string EventIdText
    {
        get => _eventIdText;
        set => SetProperty(ref _eventIdText, value ?? string.Empty);
    }

    public string EventText
    {
        get => _eventText;
        set => SetProperty(ref _eventText, value ?? string.Empty);
    }

    public string SelectedLogLevel
    {
        get => _selectedLogLevel;
        set => SetProperty(ref _selectedLogLevel, value ?? MonitorLogLevel.Warning.ToString());
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value ?? string.Empty))
            {
                RaisePropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasActions => Actions.Count > 0;

    public bool HasNoActions => !HasActions;

    public bool IsDefaultMode => string.Equals(SelectedMode, MonitorRuleMode.Default.ToString(), StringComparison.OrdinalIgnoreCase);

    public bool IsCustomMode => !IsDefaultMode;
    
    public bool ShowSourcePicker => IsDefaultMode;

    public string FormulaStatusMessage
    {
        get => _formulaStatusMessage;
        private set => SetProperty(ref _formulaStatusMessage, value ?? string.Empty);
    }

    public string FormulaStatusBrush
    {
        get => _formulaStatusBrush;
        private set => SetProperty(ref _formulaStatusBrush, value ?? SecondaryTextBrush);
    }

    public bool ShowFormulaStatus => IsCustomMode && !string.IsNullOrWhiteSpace(FormulaStatusMessage);

    public string PreviewPath => MonitorRuleRow.BuildRegistryPath(_ownerItem.FolderName, _ownerItem.Name, Name);

    public string AggregatePreviewPath => MonitorRuleRow.BuildMonitorRegistryPath(_ownerItem.FolderName, _ownerItem.Name);

    public void AddVariable()
    {
        AddVariable(GenerateNextVariableName(), string.Empty);
    }

    public void AddVariable(string? name, string? sourcePath)
    {
        var variable = new CustomSignalVariableEntryViewModel(name ?? string.Empty, sourcePath ?? string.Empty);
        variable.PropertyChanged += OnVariablePropertyChanged;
        Variables.Add(variable);
    }

    public void RemoveVariable(CustomSignalVariableEntryViewModel variable)
    {
        if (!Variables.Remove(variable))
        {
            return;
        }

        variable.PropertyChanged -= OnVariablePropertyChanged;
        RaisePropertyChanged(nameof(VariableButtons));
        UpdateFormulaValidation();
    }

    public void AddAction()
    {
        AddAction(MonitorActionTrigger.OnActivated.ToString(), MonitorActionType.WriteLog.ToString(), string.Empty, string.Empty, string.Empty, string.Empty);
    }

    public void AddAction(string? targetLog)
    {
        AddAction(MonitorActionTrigger.OnActivated.ToString(), MonitorActionType.WriteLog.ToString(), targetLog, string.Empty, string.Empty, string.Empty);
    }

    public void AddAction(string? trigger, string? actionType, string? targetLog)
    {
        AddAction(trigger, actionType, targetLog, string.Empty, string.Empty, string.Empty);
    }

    public void AddAction(string? trigger, string? actionType, string? targetLog, string? targetPath, string? functionName, string? argument)
    {
        var entry = new MonitorActionEntryViewModel(trigger, actionType, targetLog, targetPath, functionName, argument);
        entry.PropertyChanged += OnActionPropertyChanged;
        RefreshActionFunctionOptions(entry);
        Actions.Add(entry);
    }

    public void RemoveAction(MonitorActionEntryViewModel action)
    {
        if (!Actions.Remove(action))
        {
            return;
        }

        action.PropertyChanged -= OnActionPropertyChanged;
    }

    public int InsertFormulaToken(string token, int caretIndex, int caretBacktrack = 0)
    {
        var formula = FormulaText ?? string.Empty;
        var insertIndex = Math.Clamp(caretIndex, 0, formula.Length);
        FormulaText = formula.Insert(insertIndex, token);
        return Math.Clamp(insertIndex + token.Length + caretBacktrack, 0, FormulaText.Length);
    }

    public bool TryBuildDefinition(out MonitorDefinition definition, out string errorMessage)
    {
        definition = new MonitorDefinition();
        errorMessage = string.Empty;

        var normalizedName = TargetPathHelper.NormalizeIdentityName(Name);
        if (!TargetPathHelper.IsValidPathIdentityName(normalizedName))
        {
            errorMessage = "Name must use snake_case and start with a lowercase letter.";
            return false;
        }

        var existingNames = MonitorDefinitionCodec.ParseDefinitions(_ownerItem.MonitorDefinitions)
            .Select(candidate => candidate.Name)
            .Where(candidate => !string.Equals(candidate, _originalName, StringComparison.OrdinalIgnoreCase));
        if (existingNames.Contains(normalizedName, StringComparer.OrdinalIgnoreCase))
        {
            errorMessage = "Name must be unique within the Monitor widget.";
            return false;
        }

        if (!TryParseRequiredPositiveInt(RefreshRateMsText, 250, out var refreshRateMs, out errorMessage))
        {
            return false;
        }

        if (!TryParseOptionalPositiveInt(TimeoutMsText, out var timeoutMs, out errorMessage))
        {
            return false;
        }

        if (!TryParseOptionalPositiveInt(InhibitMsText, out var inhibitMs, out errorMessage))
        {
            return false;
        }

        if (!TryValidateOptionalNumber(LowerLimit, "Lower limit", out errorMessage)
            || !TryValidateOptionalNumber(UpperLimit, "Upper limit", out errorMessage))
        {
            return false;
        }

        if (!Enum.TryParse<MonitorRuleMode>(SelectedMode, true, out var mode))
        {
            errorMessage = "Mode is invalid.";
            return false;
        }

        var normalizedSourcePath = mode == MonitorRuleMode.Default
            ? TargetPathHelper.NormalizeConfiguredTargetPath(SourcePath)
            : string.Empty;
        if (mode == MonitorRuleMode.Default && string.IsNullOrWhiteSpace(normalizedSourcePath))
        {
            errorMessage = "Source path is required.";
            return false;
        }

        if (!Enum.TryParse<MonitorLogLevel>(SelectedLogLevel, true, out var logLevel))
        {
            errorMessage = "Log level is invalid.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(EventIdText))
        {
            errorMessage = "Event Id is required.";
            return false;
        }

        if (!int.TryParse(EventIdText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var eventId))
        {
            errorMessage = "Event Id must be a valid integer.";
            return false;
        }

        if (eventId <= 0)
        {
            errorMessage = "Event Id must be greater than 0.";
            return false;
        }

        if (EnumerateSiblingDefinitions()
            .Any(candidate => candidate.EventId == eventId))
        {
            errorMessage = "Event Id must be unique within the Monitor widget.";
            return false;
        }

        if (!TryBuildVariables(out var variables, out errorMessage))
        {
            return false;
        }

        if (!TryBuildActions(out var actions, out errorMessage))
        {
            return false;
        }

        var customFormula = FormulaText?.Trim() ?? string.Empty;
        if (mode == MonitorRuleMode.Custom)
        {
            if (string.IsNullOrWhiteSpace(customFormula))
            {
                errorMessage = "Formula is required in Custom mode.";
                return false;
            }

            if (!CustomSignalFormulaEngine.TryEvaluateBooleanExpression(customFormula, CreatePreviewVariables(variables), out _, out var formulaError))
            {
                errorMessage = formulaError;
                return false;
            }
        }

        definition = new MonitorDefinition
        {
            Name = normalizedName,
            SourcePath = normalizedSourcePath,
            RefreshRateMs = refreshRateMs,
            TimeoutMs = timeoutMs,
            Mode = mode,
            LowerLimit = mode == MonitorRuleMode.Default ? LowerLimit?.Trim() ?? string.Empty : LowerLimit?.Trim() ?? string.Empty,
            UpperLimit = mode == MonitorRuleMode.Default ? UpperLimit?.Trim() ?? string.Empty : UpperLimit?.Trim() ?? string.Empty,
            InhibitMs = inhibitMs ?? 0,
            CustomFormula = customFormula,
            CustomVariables = variables,
            EventId = eventId,
            EventText = EventText?.Trim() ?? string.Empty,
            Actions = actions,
            TargetLog = string.Empty,
            LogLevel = logLevel
        };
        return true;
    }

    private string GenerateNextRuleName()
    {
        var existingNames = MonitorDefinitionCodec.ParseDefinitions(_ownerItem.MonitorDefinitions)
            .Select(definition => definition.Name);
        return TargetPathHelper.GenerateIndexedPathIdentityName("monitor_rule", existingNames, "monitor_rule");
    }

    private int GenerateNextEventId()
    {
        var usedEventIds = MonitorDefinitionCodec.ParseDefinitions(_ownerItem.MonitorDefinitions)
            .Select(static definition => definition.EventId)
            .Where(static eventId => eventId > 0)
            .ToHashSet();

        var candidate = 1;
        while (usedEventIds.Contains(candidate))
        {
            candidate++;
        }

        return candidate;
    }

    private IEnumerable<MonitorDefinition> EnumerateSiblingDefinitions()
    {
        return MonitorDefinitionCodec.ParseDefinitions(_ownerItem.MonitorDefinitions)
            .Where(candidate => !string.Equals(candidate.Name, _originalName, StringComparison.OrdinalIgnoreCase));
    }

    private string GenerateNextVariableName()
    {
        for (var offset = 0; offset < 26; offset++)
        {
            var candidate = ((char)('A' + offset)).ToString();
            if (Variables.All(variable => !string.Equals(variable.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        var suffix = 1;
        while (true)
        {
            for (var offset = 0; offset < 26; offset++)
            {
                var candidate = $"{(char)('A' + offset)}{suffix}";
                if (Variables.All(variable => !string.Equals(variable.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }

            suffix++;
        }
    }

    private void OnVariablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(VariableButtons));
        UpdateFormulaValidation();
    }

    private void OnActionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(HasActions));
        RaisePropertyChanged(nameof(HasNoActions));
    }

    private void OnVariablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(VariableButtons));
        UpdateFormulaValidation();
    }

    private void OnActionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MonitorActionEntryViewModel action)
        {
            return;
        }

        if (e.PropertyName is nameof(MonitorActionEntryViewModel.SelectedActionType)
            or nameof(MonitorActionEntryViewModel.TargetPath))
        {
            RefreshActionFunctionOptions(action);
        }
    }

    private void RefreshActionFunctionOptions(MonitorActionEntryViewModel action)
    {
        if (!action.IsInvokeFunctionAction)
        {
            action.SetFunctionOptions(Array.Empty<string>());
            return;
        }

        var resolvedTargetPath = ApplicationExplorerRuntime.ResolveInteractionTargetPath(_ownerItem, action.TargetPath);
        action.SetFunctionOptions(PythonClientRuntimeRegistry.GetFunctionNames(resolvedTargetPath));
    }

    private bool TryBuildVariables(out List<MonitorVariableDefinition> variables, out string errorMessage)
    {
        variables = [];
        errorMessage = string.Empty;

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "value", "source" };
        foreach (var variable in Variables)
        {
            var name = (variable.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                errorMessage = "Variable name is required.";
                return false;
            }

            if (!IsValidVariableName(name))
            {
                errorMessage = $"Variable name '{name}' is invalid.";
                return false;
            }

            if (!usedNames.Add(name))
            {
                errorMessage = $"Variable name '{name}' is used more than once.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(variable.SourcePath))
            {
                errorMessage = $"Variable '{name}' requires a source path.";
                return false;
            }

            variables.Add(new MonitorVariableDefinition
            {
                Name = name,
                SourcePath = TargetPathHelper.NormalizeConfiguredTargetPath(variable.SourcePath)
            });
        }

        return true;
    }

    private bool TryBuildActions(out List<MonitorActionDefinition> actions, out string errorMessage)
    {
        actions = [];
        errorMessage = string.Empty;

        for (var index = 0; index < Actions.Count; index++)
        {
            var action = Actions[index];
            if (!Enum.TryParse<MonitorActionTrigger>(action.SelectedTrigger, true, out var trigger))
            {
                errorMessage = $"Action {index + 1} has an invalid trigger.";
                return false;
            }

            if (!Enum.TryParse<MonitorActionType>(action.SelectedActionType, true, out var actionType))
            {
                errorMessage = $"Action {index + 1} has an invalid action type.";
                return false;
            }

            var targetLog = TargetPathHelper.NormalizeConfiguredTargetPath(action.TargetLog);
            var targetPath = actionType == MonitorActionType.InvokeFunction
                ? action.TargetPath?.Trim() ?? string.Empty
                : TargetPathHelper.NormalizeConfiguredTargetPath(action.TargetPath);
            var functionName = action.FunctionName?.Trim() ?? string.Empty;
            var argument = action.Argument?.Trim() ?? string.Empty;
            if (actionType == MonitorActionType.WriteLog && string.IsNullOrWhiteSpace(targetLog))
            {
                errorMessage = $"Action {index + 1} requires a target log for WriteLog.";
                return false;
            }

            if (actionType == MonitorActionType.SetValue && string.IsNullOrWhiteSpace(targetPath))
            {
                errorMessage = $"Action {index + 1} requires a target path for SetValue.";
                return false;
            }

            if (actionType == MonitorActionType.InvokeFunction)
            {
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    errorMessage = $"Action {index + 1} requires a target path for InvokeFunction.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(functionName))
                {
                    errorMessage = $"Action {index + 1} requires a function name for InvokeFunction.";
                    return false;
                }
            }

            actions.Add(new MonitorActionDefinition
            {
                Trigger = trigger,
                ActionType = actionType,
                TargetLog = targetLog,
                TargetPath = targetPath,
                FunctionName = functionName,
                Argument = argument
            });
        }

        return true;
    }

    private void UpdateFormulaValidation()
    {
        if (!IsCustomMode)
        {
            FormulaStatusMessage = string.Empty;
            FormulaStatusBrush = SecondaryTextBrush;
            RaisePropertyChanged(nameof(ShowFormulaStatus));
            return;
        }

        if (!TryBuildVariables(out var variables, out var variableError))
        {
            FormulaStatusMessage = variableError;
            FormulaStatusBrush = "#B42318";
            RaisePropertyChanged(nameof(ShowFormulaStatus));
            return;
        }

        if (string.IsNullOrWhiteSpace(FormulaText))
        {
            FormulaStatusMessage = "Formula is required in Custom mode.";
            FormulaStatusBrush = "#B42318";
            RaisePropertyChanged(nameof(ShowFormulaStatus));
            return;
        }

        if (CustomSignalFormulaEngine.TryEvaluateBooleanExpression(FormulaText.Trim(), CreatePreviewVariables(variables), out _, out var errorMessage))
        {
            FormulaStatusMessage = "Formula looks valid.";
            FormulaStatusBrush = "#027A48";
        }
        else
        {
            FormulaStatusMessage = errorMessage;
            FormulaStatusBrush = "#B42318";
        }

        RaisePropertyChanged(nameof(ShowFormulaStatus));
    }

    private static Dictionary<string, object?> CreatePreviewVariables(IEnumerable<MonitorVariableDefinition> variables)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["value"] = 1d,
            ["source"] = 1d
        };

        foreach (var variable in variables)
        {
            dictionary[variable.Name] = 1d;
        }

        return dictionary;
    }

    private static bool IsValidVariableName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !char.IsLetter(name[0]))
        {
            return false;
        }

        return name.All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    private static IReadOnlyList<FormulaInsertButtonDefinition> CreateOperatorButtons()
    {
        return
        [
            new FormulaInsertButtonDefinition("AND", " && ", tooltip: "Logical AND"),
            new FormulaInsertButtonDefinition("OR", " || ", tooltip: "Logical OR"),
            new FormulaInsertButtonDefinition("NOT", "!", tooltip: "Logical NOT"),
            new FormulaInsertButtonDefinition("=", " == ", tooltip: "Equal"),
            new FormulaInsertButtonDefinition("!=", " != ", tooltip: "Not equal"),
            new FormulaInsertButtonDefinition(">", " > ", tooltip: "Greater than"),
            new FormulaInsertButtonDefinition("<", " < ", tooltip: "Less than"),
            new FormulaInsertButtonDefinition(">=", " >= ", tooltip: "Greater or equal"),
            new FormulaInsertButtonDefinition("<=", " <= ", tooltip: "Less or equal"),
            new FormulaInsertButtonDefinition("(", "(", tooltip: "Open bracket"),
            new FormulaInsertButtonDefinition(")", ")", tooltip: "Close bracket")
        ];
    }

    private static bool TryParseRequiredPositiveInt(string raw, int minValue, out int value, out string errorMessage)
    {
        errorMessage = string.Empty;
        value = 0;
        if (!int.TryParse(string.IsNullOrWhiteSpace(raw) ? "0" : raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < minValue)
        {
            errorMessage = $"Value must be a whole number greater than or equal to {minValue}.";
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryParseOptionalPositiveInt(string raw, out int? value, out string errorMessage)
    {
        errorMessage = string.Empty;
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            errorMessage = "Numeric values must be empty or a non-negative integer.";
            return false;
        }

        value = parsed > 0 ? parsed : null;
        return true;
    }

    private static bool TryValidateOptionalNumber(string raw, string label, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _))
        {
            return true;
        }
        errorMessage = $"{label} must be empty or a valid number.";
        return false;
    }
}

public sealed class MonitorActionEntryViewModel : ObservableObject
{
    private static readonly IReadOnlyList<string> TriggerOptions = Enum.GetNames<MonitorActionTrigger>();
    private static readonly IReadOnlyList<string> ActionTypeOptions = Enum.GetNames<MonitorActionType>();
    private string _selectedTrigger = MonitorActionTrigger.OnActivated.ToString();
    private string _selectedActionType = MonitorActionType.WriteLog.ToString();
    private string _targetLog = string.Empty;
    private string _targetPath = string.Empty;
    private string _functionName = string.Empty;
    private string _argument = string.Empty;

    public MonitorActionEntryViewModel(string? trigger, string? actionType, string? targetLog, string? targetPath, string? functionName, string? argument)
    {
        SelectedTrigger = trigger ?? MonitorActionTrigger.OnActivated.ToString();
        SelectedActionType = actionType ?? MonitorActionType.WriteLog.ToString();
        TargetLog = targetLog ?? string.Empty;
        TargetPath = targetPath ?? string.Empty;
        FunctionName = functionName ?? string.Empty;
        Argument = argument ?? string.Empty;
    }

    public IReadOnlyList<string> AvailableTriggerOptions => TriggerOptions;

    public IReadOnlyList<string> AvailableActionTypeOptions => ActionTypeOptions;

    public string SelectedTrigger
    {
        get => _selectedTrigger;
        set => SetProperty(ref _selectedTrigger, value ?? MonitorActionTrigger.OnActivated.ToString());
    }

    public string SelectedActionType
    {
        get => _selectedActionType;
        set
        {
            if (SetProperty(ref _selectedActionType, value ?? MonitorActionType.WriteLog.ToString()))
            {
                RaisePropertyChanged(nameof(IsWriteLogAction));
                RaisePropertyChanged(nameof(IsSetValueAction));
                RaisePropertyChanged(nameof(IsInvokeFunctionAction));
                RaisePropertyChanged(nameof(ShowArgument));
                RaisePropertyChanged(nameof(ShowFunction));
                RaisePropertyChanged(nameof(DisplayTarget));
            }
        }
    }

    public string TargetLog
    {
        get => _targetLog;
        set
        {
            if (SetProperty(ref _targetLog, value ?? string.Empty))
            {
                RaisePropertyChanged(nameof(DisplayTarget));
            }
        }
    }

    public string TargetPath
    {
        get => _targetPath;
        set
        {
            if (SetProperty(ref _targetPath, value ?? string.Empty))
            {
                RaisePropertyChanged(nameof(DisplayTarget));
            }
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

    public string DisplayTarget
    {
        get => IsWriteLogAction ? TargetLog : TargetPath;
        set
        {
            if (IsWriteLogAction)
            {
                TargetLog = value;
            }
            else
            {
                TargetPath = value;
            }
        }
    }

    public bool IsWriteLogAction => string.Equals(SelectedActionType, nameof(MonitorActionType.WriteLog), StringComparison.OrdinalIgnoreCase);

    public bool IsSetValueAction => string.Equals(SelectedActionType, nameof(MonitorActionType.SetValue), StringComparison.OrdinalIgnoreCase);

    public bool IsInvokeFunctionAction => string.Equals(SelectedActionType, nameof(MonitorActionType.InvokeFunction), StringComparison.OrdinalIgnoreCase);

    public bool ShowArgument => IsSetValueAction || IsInvokeFunctionAction;

    public bool ShowFunction => IsInvokeFunctionAction;

    public ObservableCollection<string> FunctionOptions { get; } = [];

    public void SetFunctionOptions(IEnumerable<string> options)
    {
        FunctionOptions.Clear();
        foreach (var option in options.Where(static option => !string.IsNullOrWhiteSpace(option)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            FunctionOptions.Add(option);
        }

        if (!string.IsNullOrWhiteSpace(FunctionName)
            && !FunctionOptions.Contains(FunctionName, StringComparer.OrdinalIgnoreCase))
        {
            FunctionOptions.Add(FunctionName);
        }
    }
}
