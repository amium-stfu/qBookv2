using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

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
        NewArgument = string.Empty;
        InitializeComponent();
        DataContext = this;
        AttachToViewModel(viewModel);
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

    public string NewEventName { get; set; }

    public string NewActionName { get; set; }

    public string NewTargetPath { get; set; }

    public string NewArgument { get; set; }

    protected override void OnClosed(System.EventArgs e)
    {
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
            Argument = NewArgument
        };

        foreach (var option in EventOptions)
        {
            row.EventOptions.Add(option);
        }

        foreach (var option in ActionOptions)
        {
            row.ActionOptions.Add(option);
        }

        foreach (var option in TargetOptions)
        {
            row.TargetOptions.Add(option);
        }

        Rows.Add(row);
        e.Handled = true;
    }

    private void OnRemoveClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ItemInteractionEditorRow row })
        {
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