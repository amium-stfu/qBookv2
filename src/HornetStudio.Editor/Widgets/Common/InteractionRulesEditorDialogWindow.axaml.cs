using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using HornetStudio.Editor.Helpers;
using HornetStudio.Editor.Models;
using HornetStudio.Editor.ViewModels;

namespace HornetStudio.Editor.Widgets;

public sealed class PythonInteractionTargetDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not string raw || string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        if (raw.Contains(" - ", StringComparison.Ordinal))
        {
            return raw;
        }

        return ApplicationExplorerRuntime.GetInteractionTargetDisplayText(raw);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value ?? string.Empty;
}

public partial class InteractionRulesEditorDialogWindow : Window, INotifyPropertyChanged
{
    private readonly EditorDialogField? _field;
    private MainWindowViewModel? _viewModel;
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
    private string _newEventName = "BodyLeftClick";
    private string _newActionName = "OpenValueEditor";
    private string _newTargetPath = "this";
    private string _newFunctionName = string.Empty;
    private string _newArgument = string.Empty;
    private string _newSetValueSummary = string.Empty;
    private string _newSetValueValidationMessage = string.Empty;
    private FunctionPickerOption? _selectedNewRunFunctionOption;
    private SetValueInlineOperationOption? _selectedNewSetValueOperation;
    private string _newSetValueLiteralArgument = string.Empty;
    private string _newSetValueSeparator = string.Empty;
    private string _newSetValueSourcePath = string.Empty;
    private SetValueTargetKind _newSetValueTargetKind;
    private bool _isSynchronizingNewSetValueInlineState;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public InteractionRulesEditorDialogWindow()
    {
        Rows = [];
        EventOptions = new ObservableCollection<string>(ItemInteractionRuleCodec.EventOptions);
        ActionOptions = new ObservableCollection<string>(ItemInteractionRuleCodec.ActionOptions);
        TargetOptions = [];
        NewEventName = ItemInteractionRuleCodec.EventOptions.FirstOrDefault() ?? "BodyLeftClick";
        NewActionName = ItemInteractionRuleCodec.ActionOptions.FirstOrDefault() ?? "OpenValueEditor";
        NewTargetPath = "this";
        NewFunctionName = string.Empty;
        NewArgument = string.Empty;
        InitializeComponent();
        DataContext = this;
    }

    public InteractionRulesEditorDialogWindow(MainWindowViewModel? viewModel, EditorDialogField field)
    {
        _field = field;
        Rows = new ObservableCollection<ItemInteractionEditorRow>(field.CreateInteractionRuleSnapshot());
        EventOptions = new ObservableCollection<string>(field.InteractionEventOptions);
        ActionOptions = new ObservableCollection<string>(field.InteractionActionOptions);
        TargetOptions = new ObservableCollection<string>(field.InteractionTargetOptions);
        NewEventName = EventOptions.FirstOrDefault() ?? "BodyLeftClick";
        NewActionName = ActionOptions.FirstOrDefault() ?? "OpenValueEditor";
        NewTargetPath = TargetOptions.FirstOrDefault() ?? "this";
        NewFunctionName = string.Empty;
        NewArgument = string.Empty;
        InitializeComponent();
        DataContext = this;
        AttachToViewModel(viewModel);
        foreach (var row in Rows)
        {
            AttachRow(row);
        }

        RefreshNewEntryOptions();
    }

    public ObservableCollection<ItemInteractionEditorRow> Rows { get; }

    public ObservableCollection<string> EventOptions { get; }

    public ObservableCollection<string> ActionOptions { get; }

    public ObservableCollection<string> TargetOptions { get; }

    public ObservableCollection<FunctionPickerOption> NewRunFunctionOptions { get; } = [];

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

    public string NewEventName
    {
        get => _newEventName;
        set => SetAndRaise(ref _newEventName, string.IsNullOrWhiteSpace(value) ? "BodyLeftClick" : value, nameof(NewEventName));
    }

    public string NewActionName
    {
        get => _newActionName;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "OpenValueEditor" : value;
            if (_newActionName == normalized)
            {
                return;
            }

            _newActionName = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewActionName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNewPythonFunctionAction)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNewRunFunctionAction)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNewDialogAction)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNewSetValueAction)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsTargetSelection)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UsesComboTargetSelection)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UsesBrowseTargetSelection)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsFunctionPicker)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsLegacyFunctionPicker)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsNewRunFunctionPicker)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsNewSetValueFunctionPicker)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsNewFunctionPlaceholder)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsArgumentEditor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsNewSetValueEditor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsNewSetValueLiteralEditor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsNewSetValueSeparatorEditor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsNewSetValueSourceEditor)));
            RefreshNewEntryOptions();
        }
    }

    public string NewTargetPath
    {
        get => _newTargetPath;
        set
        {
            var normalized = value ?? string.Empty;
            if (ShowsTargetSelection && string.IsNullOrWhiteSpace(normalized))
            {
                normalized = "this";
            }

            if (_newTargetPath == normalized)
            {
                return;
            }

            _newTargetPath = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewTargetPath)));
            RefreshNewEntryFunctionOptions();
            RefreshNewSetValueMetadata();
        }
    }

    public string NewFunctionName
    {
        get => _newFunctionName;
        set
        {
            var normalized = value ?? string.Empty;
            if (_newFunctionName == normalized)
            {
                return;
            }

            _newFunctionName = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewFunctionName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedNewRunFunctionOption)));
        }
    }

    public SetValueTargetKind NewSetValueTargetKind
    {
        get => _newSetValueTargetKind;
        private set
        {
            if (_newSetValueTargetKind == value)
            {
                return;
            }

            _newSetValueTargetKind = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewSetValueTargetKind)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewSetValueLiteralWatermark)));
        }
    }

    public FunctionPickerOption? SelectedNewRunFunctionOption
    {
        get => _selectedNewRunFunctionOption;
        set
        {
            if (!ReferenceEquals(_selectedNewRunFunctionOption, value))
            {
                _selectedNewRunFunctionOption = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedNewRunFunctionOption)));
                NewFunctionName = value?.Reference ?? string.Empty;
            }
        }
    }

    public string NewArgument
    {
        get => _newArgument;
        set
        {
            if (_newArgument == (value ?? string.Empty))
            {
                return;
            }

            _newArgument = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewArgument)));

            if (!_isSynchronizingNewSetValueInlineState)
            {
                RefreshNewSetValueMetadata();
            }
        }
    }

    public string NewSetValueSummary
    {
        get => _newSetValueSummary;
        private set => SetAndRaise(ref _newSetValueSummary, value, nameof(NewSetValueSummary));
    }

    public string NewSetValueValidationMessage
    {
        get => _newSetValueValidationMessage;
        private set
        {
            if (_newSetValueValidationMessage == value)
            {
                return;
            }

            _newSetValueValidationMessage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewSetValueValidationMessage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNewSetValueValidationError)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAdd)));
        }
    }

    public ObservableCollection<SetValueInlineOperationOption> NewSetValueOperationOptions { get; } = [];

    public ObservableCollection<string> NewSetValueSourceOptions { get; } = [];

    public bool IsNewPythonFunctionAction => string.Equals(NewActionName, nameof(ItemInteractionAction.InvokePythonFunction), StringComparison.OrdinalIgnoreCase);

    public bool IsNewSetValueAction => string.Equals(NewActionName, nameof(ItemInteractionAction.SetValue), StringComparison.OrdinalIgnoreCase);

    public bool IsNewRunFunctionAction
        => string.Equals(NewActionName, nameof(ItemInteractionAction.RunFunction), StringComparison.OrdinalIgnoreCase)
            || string.Equals(NewActionName, nameof(ItemInteractionAction.StopFunction), StringComparison.OrdinalIgnoreCase);

    public bool IsNewDialogAction
        => string.Equals(NewActionName, nameof(ItemInteractionAction.OpenDialog), StringComparison.OrdinalIgnoreCase)
            || string.Equals(NewActionName, nameof(ItemInteractionAction.CloseDialog), StringComparison.OrdinalIgnoreCase);

    public bool ShowsTargetSelection => !IsNewRunFunctionAction;

    public bool UsesComboTargetSelection => IsNewPythonFunctionAction || IsNewDialogAction;

    public bool UsesBrowseTargetSelection => ShowsTargetSelection && !UsesComboTargetSelection;

    public bool ShowsFunctionPicker => IsNewPythonFunctionAction || IsNewRunFunctionAction;

    public bool ShowsLegacyFunctionPicker => IsNewPythonFunctionAction;

    public bool ShowsNewRunFunctionPicker => IsNewRunFunctionAction;

    public bool ShowsNewSetValueFunctionPicker => IsNewSetValueAction;

    public bool ShowsArgumentEditor => !IsNewSetValueAction;

    public bool ShowsNewSetValueEditor => IsNewSetValueAction;

    public bool ShowsNewFunctionPlaceholder => !ShowsFunctionPicker && !IsNewSetValueAction;

    public bool ShowsNewSetValueLiteralEditor
        => IsNewSetValueAction
            && SelectedNewSetValueOperation?.UsesSourceItem != true
            && SelectedNewSetValueOperation?.Kind is not SetValueOperationKind.SetTrue
            && SelectedNewSetValueOperation?.Kind is not SetValueOperationKind.SetFalse;

    public bool ShowsNewSetValueSeparatorEditor
        => IsNewSetValueAction
            && SelectedNewSetValueOperation?.Kind == SetValueOperationKind.AppendText
            && NewSetValueTargetKind == SetValueTargetKind.String;

    public bool ShowsNewSetValueSourceEditor => IsNewSetValueAction && SelectedNewSetValueOperation?.UsesSourceItem == true;

    public bool HasNewSetValueValidationError => !string.IsNullOrWhiteSpace(NewSetValueValidationMessage);

    public bool CanAdd => !HasNewSetValueValidationError;

    public bool CanSave => Rows.All(static row => !row.HasSetValueValidationError);

    public string NewSetValueLiteralWatermark => NewSetValueTargetKind switch
    {
        SetValueTargetKind.Numeric => "12.5",
        SetValueTargetKind.Boolean => "true",
        _ => "Value"
    };

    public SetValueInlineOperationOption? SelectedNewSetValueOperation
    {
        get => _selectedNewSetValueOperation;
        set
        {
            if (ReferenceEquals(_selectedNewSetValueOperation, value))
            {
                return;
            }

            _selectedNewSetValueOperation = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedNewSetValueOperation)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsNewSetValueLiteralEditor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsNewSetValueSeparatorEditor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsNewSetValueSourceEditor)));
            SyncNewArgumentFromInlineState();
        }
    }

    public string NewSetValueLiteralArgument
    {
        get => _newSetValueLiteralArgument;
        set
        {
            var normalized = value ?? string.Empty;
            if (_newSetValueLiteralArgument == normalized)
            {
                return;
            }

            _newSetValueLiteralArgument = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewSetValueLiteralArgument)));
            SyncNewArgumentFromInlineState();
        }
    }

    public string NewSetValueSeparator
    {
        get => _newSetValueSeparator;
        set
        {
            var normalized = value ?? string.Empty;
            if (_newSetValueSeparator == normalized)
            {
                return;
            }

            _newSetValueSeparator = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewSetValueSeparator)));
            SyncNewArgumentFromInlineState();
        }
    }

    public string NewSetValueSourcePath
    {
        get => _newSetValueSourcePath;
        set
        {
            var normalized = value ?? string.Empty;
            if (_newSetValueSourcePath == normalized)
            {
                return;
            }

            _newSetValueSourcePath = normalized;
            EnsureCurrentNewSetValueSourceOption();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewSetValueSourcePath)));
            SyncNewArgumentFromInlineState();
        }
    }

    protected override void OnClosed(System.EventArgs e)
    {
        foreach (var row in Rows)
        {
            DetachRow(row);
        }

        AttachToViewModel(null);
        base.OnClosed(e);
    }

    private void OnAddClicked(object? sender, RoutedEventArgs e)
    {
        RefreshNewSetValueMetadata();
        if (!CanAdd)
        {
            e.Handled = true;
            return;
        }

        var row = new ItemInteractionEditorRow
        {
            EventName = string.IsNullOrWhiteSpace(NewEventName) ? "BodyLeftClick" : NewEventName,
            ActionName = string.IsNullOrWhiteSpace(NewActionName) ? "OpenValueEditor" : NewActionName,
            TargetPath = string.IsNullOrWhiteSpace(NewTargetPath) ? "this" : NewTargetPath,
            FunctionName = NewFunctionName,
            Argument = NewArgument
        };

        AttachRow(row);

        Rows.Add(row);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave)));
        e.Handled = true;
    }

    private async void OnBrowseRowTargetClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ItemInteractionEditorRow row })
        {
            return;
        }

        var selectedTarget = await SelectTargetAsync(row.TargetOptions, row.TargetPath);
        if (!string.IsNullOrWhiteSpace(selectedTarget))
        {
            DetachRow(row);
            row.TargetPath = selectedTarget;
            RefreshRowOptions(row);
            row.TargetPath = selectedTarget;
            row.RaisePropertyChanged(nameof(ItemInteractionEditorRow.TargetPath));
            AttachRow(row);
        }

        e.Handled = true;
    }

    private async void OnBrowseNewTargetClicked(object? sender, RoutedEventArgs e)
    {
        var currentTarget = string.IsNullOrWhiteSpace(NewTargetPath) && UsesBrowseTargetSelection
            ? "this"
            : NewTargetPath;
        var selectedTarget = await SelectTargetAsync(TargetOptions, currentTarget);
        if (!string.IsNullOrWhiteSpace(selectedTarget))
        {
            NewTargetPath = selectedTarget;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewTargetPath)));
        }

        e.Handled = true;
    }

    private async void OnEditRowSetValueClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ItemInteractionEditorRow row })
        {
            return;
        }

        var targetPath = string.IsNullOrWhiteSpace(row.TargetPath)
            ? "this"
            : TargetPathHelper.NormalizeConfiguredTargetPath(row.TargetPath);

        var descriptor = _field?.GetSetValueTargetDescriptor(targetPath) ?? new SetValueTargetDescriptor
        {
            TargetPath = targetPath,
            TargetKind = SetValueTargetKind.Unknown,
            IsWritable = true,
            ValuePropertyName = string.Empty
        };
        var sourceOptions = _field?.GetCompatibleSetValueSourceOptions(targetPath) ?? row.TargetOptions;
        var dialog = new SetValueOperationEditorDialogWindow(
            _viewModel,
            descriptor.TargetPath,
            descriptor.TargetKind,
            row.Argument,
            sourceOptions);
        var result = await dialog.ShowDialog<string?>(this);
        if (result is not null)
        {
            row.Argument = result;
            RefreshRowOptions(row);
        }

        e.Handled = true;
    }

    private async void OnBrowseRowSetValueSourceClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ItemInteractionEditorRow row })
        {
            return;
        }

        var currentTarget = string.IsNullOrWhiteSpace(row.SetValueSourcePath)
            ? row.SetValueSourceOptions.FirstOrDefault() ?? string.Empty
            : row.SetValueSourcePath;
        var selectedTarget = await SelectTargetAsync(row.SetValueSourceOptions, currentTarget, usesBrowseSelection: true);
        if (!string.IsNullOrWhiteSpace(selectedTarget))
        {
            row.SetValueSourcePath = selectedTarget;
        }

        e.Handled = true;
    }

    private async void OnBrowseNewSetValueSourceClicked(object? sender, RoutedEventArgs e)
    {
        var currentTarget = string.IsNullOrWhiteSpace(NewSetValueSourcePath)
            ? NewSetValueSourceOptions.FirstOrDefault() ?? string.Empty
            : NewSetValueSourcePath;
        var selectedTarget = await SelectTargetAsync(NewSetValueSourceOptions, currentTarget, usesBrowseSelection: true);
        if (!string.IsNullOrWhiteSpace(selectedTarget))
        {
            NewSetValueSourcePath = selectedTarget;
        }

        e.Handled = true;
    }

    private void OnRemoveClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ItemInteractionEditorRow row })
        {
            DetachRow(row);
            Rows.Remove(row);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave)));
        }

        e.Handled = true;
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        RefreshAllSetValueValidation();
        if (!CanSave)
        {
            e.Handled = true;
            return;
        }

        _field?.ApplyInteractionRuleEntries(Rows);
        Close();
        e.Handled = true;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close();
        e.Handled = true;
    }

    private void AttachToViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateThemeBindings();
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
            UpdateThemeBindings();
        }
    }

    private void UpdateThemeBindings()
    {
        DialogBackground = _viewModel?.DialogBackground ?? "#E3E5EE";
        BorderColor = _viewModel?.CardBorderBrush ?? "#D5D9E0";
        PrimaryTextBrush = _viewModel?.PrimaryTextBrush ?? "#111827";
        SecondaryTextBrush = _viewModel?.SecondaryTextBrush ?? "#5E6777";
        ButtonBackground = _viewModel?.EditPanelButtonBackground ?? "#F8FAFC";
        ButtonBorderBrush = _viewModel?.EditPanelButtonBorderBrush ?? "#CBD5E1";
        ButtonForeground = _viewModel?.PrimaryTextBrush ?? "#111827";
        InputBackground = _viewModel?.ParameterEditBackgrundColor ?? "#FFFFFF";
        InputForeground = _viewModel?.ParameterEditForeColor ?? "#111827";
        ParameterHoverColor = _viewModel?.ParameterHoverColor ?? "#BDBDBD";
        EditorDialogSectionContentBackground = _viewModel?.EditorDialogSectionContentBackground ?? "#EEF3F8";
        SectionBorderBrush = _viewModel?.EditorDialogSectionHeaderBorderBrush ?? "#CBD5E1";
        SectionHeaderForeground = _viewModel?.EditorDialogSectionHeaderForeground ?? "#111827";
    }

    private void AttachRow(ItemInteractionEditorRow row)
    {
        foreach (var option in EventOptions)
        {
            if (!row.EventOptions.Contains(option))
            {
                row.EventOptions.Add(option);
            }
        }

        foreach (var option in ActionOptions)
        {
            if (!row.ActionOptions.Contains(option))
            {
                row.ActionOptions.Add(option);
            }
        }

        RefreshRowOptions(row);
        row.PropertyChanged += OnRowPropertyChanged;
    }

    private void DetachRow(ItemInteractionEditorRow row)
    {
        row.PropertyChanged -= OnRowPropertyChanged;
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ItemInteractionEditorRow row)
        {
            return;
        }

        if (e.PropertyName is nameof(ItemInteractionEditorRow.ActionName) or nameof(ItemInteractionEditorRow.TargetPath))
        {
            RefreshRowOptions(row);
        }

        if (e.PropertyName is nameof(ItemInteractionEditorRow.Argument)
            or nameof(ItemInteractionEditorRow.SetValueValidationMessage)
            or nameof(ItemInteractionEditorRow.ActionName)
            or nameof(ItemInteractionEditorRow.TargetPath))
        {
            if (e.PropertyName == nameof(ItemInteractionEditorRow.Argument))
            {
                _field?.RefreshSetValueMetadata(row);
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave)));
        }
    }

    private void RefreshRowOptions(ItemInteractionEditorRow row)
    {
        if (_field is not null)
        {
            _field.RefreshInteractionRuleRowOptions(row);
            return;
        }

        row.TargetOptions.Clear();
        foreach (var option in TargetOptions)
        {
            row.TargetOptions.Add(option);
        }
    }

    private void RefreshNewEntryOptions()
    {
        if (_field is null)
        {
            return;
        }

        TargetOptions.Clear();
        foreach (var option in _field.GetInteractionTargetOptions(NewActionName))
        {
            TargetOptions.Add(option);
        }

        if (UsesComboTargetSelection)
        {
            if ((string.IsNullOrWhiteSpace(NewTargetPath) || !TargetOptions.Contains(NewTargetPath, StringComparer.Ordinal)) && TargetOptions.Count > 0)
            {
                NewTargetPath = TargetOptions[0];
            }
        }
        else if (string.IsNullOrWhiteSpace(NewTargetPath))
        {
            NewTargetPath = "this";
        }

        RefreshNewEntryFunctionOptions();
        RefreshNewSetValueMetadata();
    }

    private void RefreshNewEntryFunctionOptions()
    {
        if (_field is null)
        {
            return;
        }

        if (!ShowsTargetSelection)
        {
            NewTargetPath = "this";
        }

        var functionOptions = _field.GetInteractionFunctionOptions(NewActionName, NewTargetPath);

        NewRunFunctionOptions.Clear();
        if (IsNewRunFunctionAction)
        {
            foreach (var option in _field.GetRunFunctionOptions())
            {
                NewRunFunctionOptions.Add(option);
            }

            var selectedOption = NewRunFunctionOptions.FirstOrDefault(option => HornetStudio.Editor.Functions.FunctionRegistry.ReferencesEqual(option.Reference, NewFunctionName));
            if (selectedOption is null && !string.IsNullOrWhiteSpace(NewFunctionName))
            {
                selectedOption = new FunctionPickerOption
                {
                    Reference = NewFunctionName.Trim(),
                    DisplayText = $"Missing / {NewFunctionName.Trim()}"
                };
                NewRunFunctionOptions.Add(selectedOption);
            }

            if (selectedOption is null)
            {
                selectedOption = NewRunFunctionOptions.FirstOrDefault();
            }

            _selectedNewRunFunctionOption = selectedOption;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedNewRunFunctionOption)));
            NewFunctionName = selectedOption?.Reference ?? NewFunctionName;
            return;
        }

        var functionCombo = this.FindControl<ComboBox>("NewFunctionComboBox");
        if (functionCombo is not null)
        {
            functionCombo.ItemsSource = functionOptions;
        }

        if (!string.IsNullOrWhiteSpace(NewFunctionName) && functionOptions.Contains(NewFunctionName, StringComparer.Ordinal))
        {
            return;
        }

        NewFunctionName = functionOptions.FirstOrDefault() ?? NewFunctionName;
    }

    private void RefreshNewSetValueMetadata()
    {
        if (!IsNewSetValueAction)
        {
            NewSetValueTargetKind = SetValueTargetKind.Unknown;
            NewSetValueOperationOptions.Clear();
            NewSetValueSourceOptions.Clear();
            NewSetValueSummary = string.Empty;
            NewSetValueValidationMessage = string.Empty;
            return;
        }

        var descriptor = _field?.GetSetValueTargetDescriptor(NewTargetPath) ?? new SetValueTargetDescriptor
        {
            TargetPath = string.IsNullOrWhiteSpace(NewTargetPath) ? "this" : NewTargetPath,
            TargetKind = SetValueTargetKind.Unknown,
            IsWritable = true,
            ValuePropertyName = string.Empty
        };
        NewSetValueTargetKind = descriptor.TargetKind;
        RefreshNewSetValueInlineState();
        NewSetValueSummary = SetValueOperationCodec.GetSummary(NewArgument, descriptor.TargetKind);

        var parsed = SetValueOperationCodec.Parse(NewArgument);
        if (!parsed.IsValid)
        {
            NewSetValueValidationMessage = parsed.ErrorMessage;
            return;
        }

        var validation = SetValueOperationCodec.Validate(
            operation: parsed.Operation,
            targetKind: descriptor.TargetKind,
            isCompatibleSourcePath: sourcePath => _field?.IsCompatibleSetValueSourcePath(NewTargetPath, sourcePath)
                ?? TargetOptions.Contains(sourcePath, StringComparer.OrdinalIgnoreCase));
        NewSetValueValidationMessage = validation.IsValid ? string.Empty : validation.ErrorMessage;
    }

    private void RefreshAllSetValueValidation()
    {
        if (_field is null)
        {
            return;
        }

        foreach (var row in Rows)
        {
            _field.RefreshSetValueMetadata(row);
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave)));
    }

    private void RefreshNewSetValueInlineState()
    {
        var selectedKind = _selectedNewSetValueOperation?.Kind;

        NewSetValueOperationOptions.Clear();
        foreach (var option in SetValueOperationCodec.GetInlineOperationOptions(NewSetValueTargetKind))
        {
            NewSetValueOperationOptions.Add(option);
        }

        NewSetValueSourceOptions.Clear();
        foreach (var option in (_field?.GetCompatibleSetValueSourceOptions(NewTargetPath) ?? TargetOptions)
                     .Where(static option => !string.IsNullOrWhiteSpace(option))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            NewSetValueSourceOptions.Add(option);
        }

        var parsed = SetValueOperationCodec.Parse(NewArgument);
        var inlineOperation = parsed.IsValid
            ? SetValueOperationCodec.ToInlineEditorOperation(parsed.Operation, NewSetValueTargetKind)
            : new SetValueOperation
            {
                Kind = SetValueOperationKind.SetLiteral,
                LiteralValue = NewArgument,
                IsLegacyLiteral = true
            };

        _isSynchronizingNewSetValueInlineState = true;
        _newSetValueLiteralArgument = inlineOperation.LiteralValue ?? string.Empty;
        _newSetValueSeparator = inlineOperation.Separator ?? string.Empty;
        _newSetValueSourcePath = inlineOperation.SourcePath ?? string.Empty;
        var preferredKind = NewSetValueOperationOptions.Any(option => option.Kind == inlineOperation.Kind)
            ? inlineOperation.Kind
            : selectedKind is not null && NewSetValueOperationOptions.Any(option => option.Kind == selectedKind)
                ? selectedKind.Value
                : NewSetValueOperationOptions.FirstOrDefault()?.Kind ?? SetValueOperationKind.SetLiteral;
        _selectedNewSetValueOperation = NewSetValueOperationOptions.FirstOrDefault(option => option.Kind == preferredKind);
        EnsureCurrentNewSetValueSourceOption();
        _isSynchronizingNewSetValueInlineState = false;

        if (NewSetValueTargetKind == SetValueTargetKind.Boolean
            && string.IsNullOrWhiteSpace(NewArgument)
            && _selectedNewSetValueOperation is not null)
        {
            SyncNewArgumentFromInlineState();
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewSetValueLiteralArgument)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewSetValueSeparator)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewSetValueSourcePath)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedNewSetValueOperation)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsNewSetValueLiteralEditor)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsNewSetValueSeparatorEditor)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsNewSetValueSourceEditor)));
    }

    private void SyncNewArgumentFromInlineState()
    {
        if (_isSynchronizingNewSetValueInlineState || !IsNewSetValueAction || SelectedNewSetValueOperation is null)
        {
            return;
        }

        var serializedArgument = SetValueOperationCodec.Serialize(new SetValueOperation
        {
            Kind = SelectedNewSetValueOperation.Kind,
            LiteralValue = NewSetValueLiteralArgument,
            Separator = SelectedNewSetValueOperation.Kind == SetValueOperationKind.AppendText ? NewSetValueSeparator : string.Empty,
            SourcePath = NewSetValueSourcePath,
            IsLegacyLiteral = false
        });

        if (string.Equals(NewArgument, serializedArgument, StringComparison.Ordinal))
        {
            return;
        }

        _isSynchronizingNewSetValueInlineState = true;
        NewArgument = serializedArgument;
        _isSynchronizingNewSetValueInlineState = false;
        RefreshNewSetValueMetadata();
    }

    private void EnsureCurrentNewSetValueSourceOption()
    {
        if (string.IsNullOrWhiteSpace(NewSetValueSourcePath)
            || NewSetValueSourceOptions.Contains(NewSetValueSourcePath, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        NewSetValueSourceOptions.Add(NewSetValueSourcePath);
    }

    private async Task<string?> SelectTargetAsync(IEnumerable<string> options, string currentSelection, bool usesBrowseSelection = false)
    {
        if (!usesBrowseSelection && !UsesBrowseTargetSelection)
        {
            return string.IsNullOrWhiteSpace(currentSelection)
                ? options.FirstOrDefault()
                : currentSelection;
        }

        var pageName = ExtractPageName(_field?.Parameter.Path);
        var owner = this;
        var dialog = new TargetTreeSelectionDialogWindow(_viewModel, options, currentSelection, pageName);
        await dialog.ShowDialog(owner);
        var selectedTarget = dialog.CommittedSelection;
        return string.IsNullOrWhiteSpace(selectedTarget) ? null : selectedTarget;
    }

    private static string ExtractPageName(string? parameterPath)
    {
        if (string.IsNullOrWhiteSpace(parameterPath))
        {
            return string.Empty;
        }

        var normalizedPath = parameterPath.Replace('/', '.').Trim();
        var firstSeparator = normalizedPath.IndexOf('.');
        if (firstSeparator <= 0)
        {
            return normalizedPath;
        }

        return normalizedPath[..firstSeparator].Trim();
    }

    private void SetAndRaise(ref string field, string value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
