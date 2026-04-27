using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public sealed class PythonInteractionTargetDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not string raw || string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNewStandardAction)));
            RefreshNewEntryOptions();
        }
    }

    public string NewTargetPath
    {
        get => _newTargetPath;
        set
        {
            var normalized = value ?? string.Empty;
            if (!IsNewPythonFunctionAction && string.IsNullOrWhiteSpace(normalized))
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
        }
    }

    public string NewFunctionName
    {
        get => _newFunctionName;
        set => SetAndRaise(ref _newFunctionName, value ?? string.Empty, nameof(NewFunctionName));
    }

    public string NewArgument
    {
        get => _newArgument;
        set => SetAndRaise(ref _newArgument, value ?? string.Empty, nameof(NewArgument));
    }

    public bool IsNewPythonFunctionAction => string.Equals(NewActionName, nameof(ItemInteractionAction.InvokePythonFunction), StringComparison.OrdinalIgnoreCase);

    public bool IsNewStandardAction => !IsNewPythonFunctionAction;

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
            row.TargetPath = selectedTarget;
        }

        e.Handled = true;
    }

    private async void OnBrowseNewTargetClicked(object? sender, RoutedEventArgs e)
    {
        var currentTarget = string.IsNullOrWhiteSpace(NewTargetPath) && IsNewStandardAction
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

    private void OnRemoveClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ItemInteractionEditorRow row })
        {
            DetachRow(row);
            Rows.Remove(row);
        }

        e.Handled = true;
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
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

        if (IsNewPythonFunctionAction)
        {
            if (string.IsNullOrWhiteSpace(NewTargetPath) && TargetOptions.Count > 0)
            {
                NewTargetPath = TargetOptions[0];
            }
        }
        else if (string.IsNullOrWhiteSpace(NewTargetPath))
        {
            NewTargetPath = "this";
        }

        RefreshNewEntryFunctionOptions();
    }

    private void RefreshNewEntryFunctionOptions()
    {
        if (_field is null)
        {
            return;
        }

        var functionOptions = _field.GetInteractionFunctionOptions(NewTargetPath);
        var functionCombo = this.FindControl<ComboBox>("NewFunctionComboBox");
        if (functionCombo is not null)
        {
            functionCombo.ItemsSource = functionOptions;
        }
    }

    private async Task<string?> SelectTargetAsync(IEnumerable<string> options, string currentSelection)
    {
        var pageName = ExtractPageName(_field?.Parameter.Path);
        var owner = this;
        var dialog = new TargetTreeSelectionDialogWindow(_viewModel, options, currentSelection, pageName);
        await dialog.ShowDialog(owner);
        return string.IsNullOrWhiteSpace(dialog.CommittedSelection) ? null : dialog.CommittedSelection;
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