using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HornetStudio.Editor.Controls;
using HornetStudio.Editor.Models;
using HornetStudio.Editor.ViewModels;
using HornetStudio.Editor.Widgets.Common;
using HornetStudio.Editor.Widgets.Workflow;

namespace HornetStudio.Editor.Widgets;

/// <summary>
/// Carries the committed function editor result including the target file path.
/// </summary>
/// <param name="FilePath">The target function YAML file path.</param>
/// <param name="Definition">The committed function definition.</param>
public sealed record FunctionEditorResult(string FilePath, FunctionDefinition Definition);

/// <summary>
/// Provides the first function editor dialog for flat function steps.
/// </summary>
public partial class FunctionEditorDialogWindow : Window, INotifyPropertyChanged
{
    private readonly MainWindowViewModel? _viewModel;
    private readonly FolderItemModel _ownerItem;
    private readonly string _functionDirectory;
    private readonly string? _existingFilePath;
    private readonly IReadOnlyList<string> _targetOptions;
    private string _dialogBackground = "#E3E5EE";
    private string _borderColor = "#D5D9E0";
    private string _primaryTextBrush = "#111827";
    private string _secondaryTextBrush = "#5E6777";
    private string _buttonBackground = "#F8FAFC";
    private string _buttonBorderBrush = "#CBD5E1";
    private string _buttonForeground = "#111827";
    private string _inputBackground = "#FFFFFF";
    private string _inputForeground = "#111827";
    private string _parameterHoverColor = "#BDBDBD";
    private string _editorDialogSectionContentBackground = "#EEF3F8";
    private string _sectionBorderBrush = "#CBD5E1";
    private string _sectionHeaderForeground = "#111827";
    private string _functionName = string.Empty;
    private string _fileNameStem = "new_function";
    private string _errorMessage = string.Empty;
    private FunctionStepType _newStepType = FunctionStepType.Log;

    /// <summary>
    /// Initializes a new empty function editor dialog for XAML loading.
    /// </summary>
    public FunctionEditorDialogWindow()
        : this(
            viewModel: null,
            ownerItem: new FolderItemModel(),
            functionDirectory: string.Empty,
            definition: null,
            existingFilePath: null,
            targetOptions: Array.Empty<string>(),
            logTargetOptions: Array.Empty<string>())
    {
    }

    public FunctionEditorDialogWindow(
        MainWindowViewModel? viewModel,
        FolderItemModel ownerItem,
        string functionDirectory,
        FunctionDefinition? definition,
        string? existingFilePath,
        IEnumerable<string> targetOptions,
        IEnumerable<string> logTargetOptions)
    {
        _viewModel = viewModel;
        _ownerItem = ownerItem ?? throw new ArgumentNullException(nameof(ownerItem));
        _functionDirectory = functionDirectory ?? string.Empty;
        _existingFilePath = existingFilePath;
        _targetOptions = targetOptions?.Where(static option => !string.IsNullOrWhiteSpace(option)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static option => option, StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();

        Rows = new ObservableCollection<FunctionStepEditorRow>(FunctionEditorDefinitionConverter.CreateRows(definition));
        StepTypeOptions = new ObservableCollection<FunctionStepType>(FunctionStepEditorRow.EditableStepTypes);
        LogLevelOptions = new ObservableCollection<string>(Enum.GetNames<MonitorLogLevel>());
        LogTargetOptions = new ObservableCollection<string>(logTargetOptions?.Where(static option => !string.IsNullOrWhiteSpace(option)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static option => option, StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>());

        _functionName = definition?.Name ?? string.Empty;
        _fileNameStem = existingFilePath is null
            ? BuildSuggestedFileStem(definition?.Name)
            : Path.GetFileNameWithoutExtension(existingFilePath);

        InitializeComponent();
        DataContext = this;
        AttachToViewModel(viewModel);
        RefreshRowState();
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FunctionStepEditorRow> Rows { get; }

    public ObservableCollection<FunctionStepType> StepTypeOptions { get; }

    public ObservableCollection<string> LogLevelOptions { get; }

    public ObservableCollection<string> LogTargetOptions { get; }

    public string DialogBackground
    {
        get => _dialogBackground;
        private set => SetAndRaise(ref _dialogBackground, value, nameof(DialogBackground));
    }

    public string BorderColor
    {
        get => _borderColor;
        private set => SetAndRaise(ref _borderColor, value, nameof(BorderColor));
    }

    public string PrimaryTextBrush
    {
        get => _primaryTextBrush;
        private set => SetAndRaise(ref _primaryTextBrush, value, nameof(PrimaryTextBrush));
    }

    public string SecondaryTextBrush
    {
        get => _secondaryTextBrush;
        private set => SetAndRaise(ref _secondaryTextBrush, value, nameof(SecondaryTextBrush));
    }

    public string ButtonBackground
    {
        get => _buttonBackground;
        private set => SetAndRaise(ref _buttonBackground, value, nameof(ButtonBackground));
    }

    public string ButtonBorderBrush
    {
        get => _buttonBorderBrush;
        private set => SetAndRaise(ref _buttonBorderBrush, value, nameof(ButtonBorderBrush));
    }

    public string ButtonForeground
    {
        get => _buttonForeground;
        private set => SetAndRaise(ref _buttonForeground, value, nameof(ButtonForeground));
    }

    public string InputBackground
    {
        get => _inputBackground;
        private set => SetAndRaise(ref _inputBackground, value, nameof(InputBackground));
    }

    public string InputForeground
    {
        get => _inputForeground;
        private set => SetAndRaise(ref _inputForeground, value, nameof(InputForeground));
    }

    public string ParameterHoverColor
    {
        get => _parameterHoverColor;
        private set => SetAndRaise(ref _parameterHoverColor, value, nameof(ParameterHoverColor));
    }

    public string EditorDialogSectionContentBackground
    {
        get => _editorDialogSectionContentBackground;
        private set => SetAndRaise(ref _editorDialogSectionContentBackground, value, nameof(EditorDialogSectionContentBackground));
    }

    public string SectionBorderBrush
    {
        get => _sectionBorderBrush;
        private set => SetAndRaise(ref _sectionBorderBrush, value, nameof(SectionBorderBrush));
    }

    public string SectionHeaderForeground
    {
        get => _sectionHeaderForeground;
        private set => SetAndRaise(ref _sectionHeaderForeground, value, nameof(SectionHeaderForeground));
    }

    public string FunctionName
    {
        get => _functionName;
        set => SetAndRaise(ref _functionName, value ?? string.Empty, nameof(FunctionName));
    }

    public string FileNameStem
    {
        get => _fileNameStem;
        set
        {
            if (!SetAndRaise(ref _fileNameStem, value ?? string.Empty, nameof(FileNameStem)))
            {
                return;
            }

            RaisePropertyChanged(nameof(FileNameDescription));
        }
    }

    public string FileNameDescription => IsFileNameReadOnly
        ? $"Existing file: {Path.GetFileName(_existingFilePath) ?? string.Empty}".Trim()
        : "New function files must use snake_case and are saved as <name>.yaml.";

    public bool IsFileNameReadOnly => !string.IsNullOrWhiteSpace(_existingFilePath);

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (!SetAndRaise(ref _errorMessage, value ?? string.Empty, nameof(ErrorMessage)))
            {
                return;
            }

            RaisePropertyChanged(nameof(HasErrorMessage));
        }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasNoRows => Rows.Count == 0;

    public FunctionStepType NewStepType
    {
        get => _newStepType;
        set => SetAndRaise(ref _newStepType, value, nameof(NewStepType));
    }

    /// <summary>
    /// Shows the function editor dialog and returns the committed function definition when saved.
    /// </summary>
    /// <param name="owner">The owning window.</param>
    /// <param name="viewModel">The main window view model.</param>
    /// <param name="ownerItem">The owning folder item.</param>
    /// <param name="functionDirectory">The function directory below the active folder.</param>
    /// <param name="definition">The function definition to edit, or <see langword="null"/> for a new function.</param>
    /// <param name="existingFilePath">The existing function file path, or <see langword="null"/> for a new function.</param>
    /// <param name="targetOptions">Available SetValue target options.</param>
    /// <param name="logTargetOptions">Available process log target options.</param>
    /// <returns>The committed function editor result, or <see langword="null"/> when the dialog was canceled.</returns>
    public static async Task<FunctionEditorResult?> ShowAsync(
        Window owner,
        MainWindowViewModel? viewModel,
        FolderItemModel ownerItem,
        string functionDirectory,
        FunctionDefinition? definition,
        string? existingFilePath,
        IEnumerable<string> targetOptions,
        IEnumerable<string> logTargetOptions)
    {
        var dialog = new FunctionEditorDialogWindow(viewModel, ownerItem, functionDirectory, definition, existingFilePath, targetOptions, logTargetOptions)
        {
            Owner = owner
        };

        return await dialog.ShowDialog<FunctionEditorResult?>(owner);
    }

    protected override void OnClosed(EventArgs e)
    {
        AttachToViewModel(null);
        base.OnClosed(e);
    }

    private async void OnBrowseStepTargetClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: FunctionStepEditorRow row })
        {
            return;
        }

        var selectedTarget = await SelectTargetAsync(row.Target);
        if (!string.IsNullOrWhiteSpace(selectedTarget))
        {
            row.Target = selectedTarget;
        }

        e.Handled = true;
    }

    private async void OnBrowseStepValueFromClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: FunctionStepEditorRow row })
        {
            return;
        }

        var selectedSource = await SelectTargetAsync(row.ValueFrom);
        if (!string.IsNullOrWhiteSpace(selectedSource))
        {
            row.ValueFrom = selectedSource;
            row.Value = string.Empty;
        }

        e.Handled = true;
    }

    private void OnAddStepClicked(object? sender, RoutedEventArgs e)
    {
        var newRow = FunctionStepEditorRow.CreateNew(NewStepType);
        Rows.Add(newRow);
        ErrorMessage = string.Empty;
        RefreshRowState();
        FocusNewRow(newRow);
        e.Handled = true;
    }

    private void OnRemoveStepClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: FunctionStepEditorRow row })
        {
            return;
        }

        if (TryFindRowCollection(row, Rows, out var collection))
        {
            if (row.RequiresPositiveDelay && !CanRemoveWhileDelayGuard(row, collection))
            {
                ErrorMessage = "While body requires at least one positive Delay step.";
                e.Handled = true;
                return;
            }

            collection.Remove(row);
        }

        ErrorMessage = string.Empty;
        RefreshRowState();
        e.Handled = true;
    }

    private void OnAddBranchStepClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: FunctionStepEditorRow row, Tag: string branchName })
        {
            return;
        }

        var branchCollection = string.Equals(branchName, "Else", StringComparison.OrdinalIgnoreCase)
            ? row.ElseRows
            : string.Equals(branchName, "While", StringComparison.OrdinalIgnoreCase)
                ? row.WhileRows
                : row.ThenRows;
        var newRow = FunctionStepEditorRow.CreateNew(string.Equals(branchName, "Else", StringComparison.OrdinalIgnoreCase)
            ? row.NewElseStepType
            : string.Equals(branchName, "While", StringComparison.OrdinalIgnoreCase)
                ? row.NewWhileStepType
                : row.NewThenStepType);
        branchCollection.Add(newRow);
        ErrorMessage = string.Empty;
        RefreshRowState();
        FocusNewRow(newRow);
        e.Handled = true;
    }

    private async void OnEditConditionClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: FunctionStepEditorRow row })
        {
            return;
        }

        var result = await BooleanConditionEditorDialogWindow.ShowAsync(
            this,
            _viewModel,
            _ownerItem.FolderName ?? string.Empty,
            _targetOptions,
            row.CreateConditionEditorClone()).ConfigureAwait(true);
        if (result is not null)
        {
            row.ApplyCondition(result.FormulaText, result.Variables);
            ErrorMessage = string.Empty;
        }

        e.Handled = true;
    }

    private async void OnConditionVariableSourcePickRequested(object? sender, ConditionVariablePickerRequestedEventArgs e)
    {
        var selectedTarget = await SelectTargetAsync(e.Variable.SourcePath).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(selectedTarget))
        {
            e.Variable.SourcePath = selectedTarget;
        }
    }

    private void OnMoveUpStepClicked(object? sender, RoutedEventArgs e)
    {
        MoveRow(sender, direction: -1);
        e.Handled = true;
    }

    private void OnMoveDownStepClicked(object? sender, RoutedEventArgs e)
    {
        MoveRow(sender, direction: 1);
        e.Handled = true;
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(_functionDirectory))
        {
            ErrorMessage = "Function directory is not available.";
            e.Handled = true;
            return;
        }

        var targetFilePath = BuildTargetFilePath(out var fileErrorMessage);
        if (string.IsNullOrWhiteSpace(targetFilePath))
        {
            ErrorMessage = fileErrorMessage;
            e.Handled = true;
            return;
        }

        if (!FunctionEditorDefinitionConverter.TryBuildDefinition(FunctionName, Rows, out var definition, out var definitionErrorMessage))
        {
            ErrorMessage = definitionErrorMessage;
            e.Handled = true;
            return;
        }

        Close(new FunctionEditorResult(targetFilePath, definition!));
        e.Handled = true;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close((FunctionEditorResult?)null);
        e.Handled = true;
    }

    private void AttachToViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            UpdateThemeBindings(viewModel);
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (viewModel is not null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateThemeBindings(viewModel);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.DialogBackground)
            || e.PropertyName == nameof(MainWindowViewModel.CardBorderBrush)
            || e.PropertyName == nameof(MainWindowViewModel.PrimaryTextBrush)
            || e.PropertyName == nameof(MainWindowViewModel.SecondaryTextBrush)
            || e.PropertyName == nameof(MainWindowViewModel.EditPanelButtonBackground)
            || e.PropertyName == nameof(MainWindowViewModel.EditPanelButtonBorderBrush)
            || e.PropertyName == nameof(MainWindowViewModel.ParameterEditBackgrundColor)
            || e.PropertyName == nameof(MainWindowViewModel.ParameterEditForeColor)
            || e.PropertyName == nameof(MainWindowViewModel.ParameterHoverColor)
            || e.PropertyName == nameof(MainWindowViewModel.EditorDialogSectionContentBackground)
            || e.PropertyName == nameof(MainWindowViewModel.EditorDialogSectionHeaderBorderBrush)
            || e.PropertyName == nameof(MainWindowViewModel.EditorDialogSectionHeaderForeground))
        {
            UpdateThemeBindings(_viewModel);
        }
    }

    private void UpdateThemeBindings(MainWindowViewModel? viewModel)
    {
        DialogBackground = viewModel?.DialogBackground ?? "#E3E5EE";
        BorderColor = viewModel?.CardBorderBrush ?? "#D5D9E0";
        PrimaryTextBrush = viewModel?.PrimaryTextBrush ?? "#111827";
        SecondaryTextBrush = viewModel?.SecondaryTextBrush ?? "#5E6777";
        ButtonBackground = viewModel?.EditPanelButtonBackground ?? "#F8FAFC";
        ButtonBorderBrush = viewModel?.EditPanelButtonBorderBrush ?? "#CBD5E1";
        ButtonForeground = viewModel?.PrimaryTextBrush ?? "#111827";
        InputBackground = viewModel?.ParameterEditBackgrundColor ?? "#FFFFFF";
        InputForeground = viewModel?.ParameterEditForeColor ?? "#111827";
        ParameterHoverColor = viewModel?.ParameterHoverColor ?? "#BDBDBD";
        EditorDialogSectionContentBackground = viewModel?.EditorDialogSectionContentBackground ?? "#EEF3F8";
        SectionBorderBrush = viewModel?.EditorDialogSectionHeaderBorderBrush ?? "#CBD5E1";
        SectionHeaderForeground = viewModel?.EditorDialogSectionHeaderForeground ?? "#111827";
    }

    private async Task<string?> SelectTargetAsync(string currentSelection)
    {
        var dialog = new TargetTreeSelectionDialogWindow(_viewModel, _targetOptions, currentSelection, _ownerItem.FolderName ?? string.Empty);
        await dialog.ShowDialog(this);
        return string.IsNullOrWhiteSpace(dialog.CommittedSelection) ? currentSelection : dialog.CommittedSelection;
    }

    private string BuildTargetFilePath(out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!TryValidateFileNameStem(FileNameStem, out var normalizedStem, out errorMessage))
        {
            return string.Empty;
        }

        var targetFilePath = _existingFilePath ?? Path.Combine(_functionDirectory, normalizedStem + ".yaml");
        if (_existingFilePath is null && File.Exists(targetFilePath))
        {
            errorMessage = $"Function file '{Path.GetFileName(targetFilePath)}' already exists.";
            return string.Empty;
        }

        return targetFilePath;
    }

    private void MoveRow(object? sender, int direction)
    {
        if (sender is not Button { CommandParameter: FunctionStepEditorRow row })
        {
            return;
        }

        if (!TryFindRowCollection(row, Rows, out var collection))
        {
            return;
        }

        var currentIndex = collection.IndexOf(row);
        if (currentIndex < 0)
        {
            return;
        }

        var targetIndex = currentIndex + direction;
        if (targetIndex < 0 || targetIndex >= collection.Count)
        {
            return;
        }

        collection.Move(currentIndex, targetIndex);
        ErrorMessage = string.Empty;
    }

    private static bool CanRemoveWhileDelayGuard(FunctionStepEditorRow row, ObservableCollection<FunctionStepEditorRow> collection)
    {
        if (!row.RequiresPositiveDelay)
        {
            return true;
        }

        return collection
            .Where(candidate => !ReferenceEquals(candidate, row))
            .Any(static candidate => candidate.StepType == FunctionStepType.Delay
                && int.TryParse(candidate.MillisecondsText, out var milliseconds)
                && milliseconds > 0);
    }

    private void RefreshRowState()
    {
        RaisePropertyChanged(nameof(HasNoRows));
    }

    private void FocusNewRow(FunctionStepEditorRow row)
    {
        Dispatcher.UIThread.Post(() => FocusNewRowCore(row), DispatcherPriority.Loaded);
    }

    private void FocusNewRowCore(FunctionStepEditorRow row)
    {
        var focusTarget = FindPreferredFocusTarget(row);
        if (focusTarget is null)
        {
            return;
        }

        focusTarget.BringIntoView();
        focusTarget.Focus();
    }

    private Control? FindPreferredFocusTarget(FunctionStepEditorRow row)
    {
        var controls = this.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => ReferenceEquals(control.DataContext, row) && control.IsVisible && control.IsEffectivelyEnabled)
            .ToArray();

        return row.StepType switch
        {
            FunctionStepType.SetValue => controls.FirstOrDefault(control => control.Classes.Contains("step-focus-target-field")),
            FunctionStepType.Delay => controls.FirstOrDefault(control => control.Classes.Contains("step-focus-delay-field")),
            FunctionStepType.Log => controls.FirstOrDefault(control => control.Classes.Contains("step-focus-log-target-field")),
            FunctionStepType.IfThenElse or FunctionStepType.While => controls.FirstOrDefault(control => control.Classes.Contains("step-focus-condition-button")),
            _ => controls.FirstOrDefault()
        };
    }

    private static bool TryFindRowCollection(
        FunctionStepEditorRow target,
        ObservableCollection<FunctionStepEditorRow> rows,
        out ObservableCollection<FunctionStepEditorRow> collection)
    {
        if (rows.Contains(target))
        {
            collection = rows;
            return true;
        }

        foreach (var row in rows)
        {
            if (TryFindRowCollection(target, row.ThenRows, out collection)
                || TryFindRowCollection(target, row.ElseRows, out collection)
                || TryFindRowCollection(target, row.WhileRows, out collection))
            {
                return true;
            }
        }

        collection = null!;
        return false;
    }

    private static string BuildSuggestedFileStem(string? functionName)
    {
        if (string.IsNullOrWhiteSpace(functionName))
        {
            return "new_function";
        }

        var buffer = functionName.Trim().ToLowerInvariant()
            .Select(static character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray();
        var collapsed = string.Join(string.Empty, new string(buffer).Split('_', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(collapsed))
        {
            return "new_workflow";
        }

        var safe = new List<char>();
        foreach (var character in functionName.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                safe.Add(character);
            }
            else if (safe.Count == 0 || safe[^1] != '_')
            {
                safe.Add('_');
            }
        }

        var result = new string(safe.ToArray()).Trim('_');
        if (string.IsNullOrWhiteSpace(result))
        {
            return "new_function";
        }

        if (!char.IsLetter(result[0]) || !char.IsLower(result[0]))
        {
            result = "function_" + result;
        }

        return result;
    }

    private static bool TryValidateFileNameStem(string? fileNameStem, out string normalizedStem, out string errorMessage)
    {
        normalizedStem = (fileNameStem ?? string.Empty).Trim();
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedStem))
        {
            errorMessage = "Function file name is required.";
            return false;
        }

        if (!char.IsLetter(normalizedStem[0]) || !char.IsLower(normalizedStem[0]))
        {
            errorMessage = "Function file name must start with a lowercase letter and use snake_case.";
            return false;
        }

        if (normalizedStem.Any(static character => !(char.IsLower(character) || char.IsDigit(character) || character == '_')))
        {
            errorMessage = "Function file name must use snake_case with lowercase letters, digits, and underscores only.";
            return false;
        }

        return true;
    }

    private bool SetAndRaise<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }

    private void RaisePropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
