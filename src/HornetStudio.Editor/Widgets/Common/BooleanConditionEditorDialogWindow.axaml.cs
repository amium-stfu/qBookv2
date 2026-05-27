using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HornetStudio.Editor.Controls;
using HornetStudio.Editor.Models;
using HornetStudio.Editor.ViewModels;

namespace HornetStudio.Editor.Widgets.Common;

/// <summary>
/// Carries the committed condition dialog result.
/// </summary>
/// <param name="FormulaText">The committed formula text.</param>
/// <param name="Variables">The committed variables.</param>
public sealed record BooleanConditionEditorDialogResult(string FormulaText, IReadOnlyList<BooleanConditionVariableDefinition> Variables);

/// <summary>
/// Hosts the shared boolean condition editor in a save/cancel dialog.
/// </summary>
public partial class BooleanConditionEditorDialogWindow : Window, INotifyPropertyChanged
{
    private readonly MainWindowViewModel? _viewModel;
    private readonly IReadOnlyList<string> _targetOptions;
    private readonly string _ownerFolderName;
    private string _dialogBackground = "#E3E5EE";
    private string _borderColor = "#D5D9E0";
    private string _primaryTextBrush = "#111827";
    private string _secondaryTextBrush = "#5E6777";
    private string _buttonBackground = "#F8FAFC";
    private string _buttonBorderBrush = "#CBD5E1";
    private string _buttonForeground = "#111827";
    private string _editorBackground = "#FFFFFF";
    private string _editorForeground = "#111827";
    private string _parameterHoverColor = "#BDBDBD";
    private string _sectionBackground = "#EEF3F8";
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Initializes a new empty instance for XAML loading.
    /// </summary>
    public BooleanConditionEditorDialogWindow()
        : this(null, string.Empty, Array.Empty<string>(), new BooleanConditionEditorViewModel())
    {
    }

    private BooleanConditionEditorDialogWindow(
        MainWindowViewModel? viewModel,
        string ownerFolderName,
        IEnumerable<string> targetOptions,
        BooleanConditionEditorViewModel editor)
    {
        _viewModel = viewModel;
        _ownerFolderName = ownerFolderName ?? string.Empty;
        _targetOptions = targetOptions?.Where(static option => !string.IsNullOrWhiteSpace(option))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static option => option, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        Editor = editor ?? throw new ArgumentNullException(nameof(editor));

        InitializeComponent();
        DataContext = this;
        AttachToViewModel(viewModel);
    }

    /// <summary>
    /// Raised when one property value changes.
    /// </summary>
    public new event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the cloned condition editor state.
    /// </summary>
    public BooleanConditionEditorViewModel Editor { get; }

    /// <summary>
    /// Gets the dialog background brush value.
    /// </summary>
    public string DialogBackground
    {
        get => _dialogBackground;
        private set => SetAndRaise(ref _dialogBackground, value, nameof(DialogBackground));
    }

    /// <summary>
    /// Gets the border brush value.
    /// </summary>
    public string BorderColor
    {
        get => _borderColor;
        private set => SetAndRaise(ref _borderColor, value, nameof(BorderColor));
    }

    /// <summary>
    /// Gets the primary text brush value.
    /// </summary>
    public string PrimaryTextBrush
    {
        get => _primaryTextBrush;
        private set => SetAndRaise(ref _primaryTextBrush, value, nameof(PrimaryTextBrush));
    }

    /// <summary>
    /// Gets the secondary text brush value.
    /// </summary>
    public string SecondaryTextBrush
    {
        get => _secondaryTextBrush;
        private set => SetAndRaise(ref _secondaryTextBrush, value, nameof(SecondaryTextBrush));
    }

    /// <summary>
    /// Gets the button background brush value.
    /// </summary>
    public string ButtonBackground
    {
        get => _buttonBackground;
        private set => SetAndRaise(ref _buttonBackground, value, nameof(ButtonBackground));
    }

    /// <summary>
    /// Gets the button border brush value.
    /// </summary>
    public string ButtonBorderBrush
    {
        get => _buttonBorderBrush;
        private set => SetAndRaise(ref _buttonBorderBrush, value, nameof(ButtonBorderBrush));
    }

    /// <summary>
    /// Gets the button foreground brush value.
    /// </summary>
    public string ButtonForeground
    {
        get => _buttonForeground;
        private set => SetAndRaise(ref _buttonForeground, value, nameof(ButtonForeground));
    }

    /// <summary>
    /// Gets the editor background brush value.
    /// </summary>
    public string EditorBackground
    {
        get => _editorBackground;
        private set => SetAndRaise(ref _editorBackground, value, nameof(EditorBackground));
    }

    /// <summary>
    /// Gets the editor foreground brush value.
    /// </summary>
    public string EditorForeground
    {
        get => _editorForeground;
        private set => SetAndRaise(ref _editorForeground, value, nameof(EditorForeground));
    }

    /// <summary>
    /// Gets the parameter hover color.
    /// </summary>
    public string ParameterHoverColor
    {
        get => _parameterHoverColor;
        private set => SetAndRaise(ref _parameterHoverColor, value, nameof(ParameterHoverColor));
    }

    /// <summary>
    /// Gets the section background brush value.
    /// </summary>
    public string SectionBackground
    {
        get => _sectionBackground;
        private set => SetAndRaise(ref _sectionBackground, value, nameof(SectionBackground));
    }

    /// <summary>
    /// Gets the current error message.
    /// </summary>
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

    /// <summary>
    /// Gets a value indicating whether the dialog currently shows an error.
    /// </summary>
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>
    /// Shows the condition editor dialog and returns the committed condition state.
    /// </summary>
    /// <param name="owner">The owning window.</param>
    /// <param name="viewModel">The optional main window view model used for theme values.</param>
    /// <param name="ownerFolderName">The owner folder name used by the target picker.</param>
    /// <param name="targetOptions">The available source path options.</param>
    /// <param name="editor">The detached condition editor state to edit.</param>
    /// <returns>The committed condition state, or <see langword="null"/> when canceled.</returns>
    public static Task<BooleanConditionEditorDialogResult?> ShowAsync(
        Window owner,
        MainWindowViewModel? viewModel,
        string ownerFolderName,
        IEnumerable<string> targetOptions,
        BooleanConditionEditorViewModel editor)
    {
        var dialog = new BooleanConditionEditorDialogWindow(viewModel, ownerFolderName, targetOptions, editor)
        {
            Owner = owner
        };

        return dialog.ShowDialog<BooleanConditionEditorDialogResult?>(owner);
    }

    protected override void OnClosed(EventArgs e)
    {
        AttachToViewModel(null);
        base.OnClosed(e);
    }

    private async void OnConditionVariableSourcePickRequested(object? sender, ConditionVariablePickerRequestedEventArgs e)
    {
        var dialog = new TargetTreeSelectionDialogWindow(_viewModel, _targetOptions, e.Variable.SourcePath, _ownerFolderName);
        await dialog.ShowDialog(this);
        if (!string.IsNullOrWhiteSpace(dialog.CommittedSelection))
        {
            e.Variable.SourcePath = dialog.CommittedSelection;
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close((BooleanConditionEditorDialogResult?)null);
        e.Handled = true;
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        ErrorMessage = string.Empty;

        if (!Editor.TryValidate(out var validationError))
        {
            ErrorMessage = validationError;
            e.Handled = true;
            return;
        }

        if (!Editor.TryBuildVariables(out var variables, out var variableError))
        {
            ErrorMessage = variableError;
            e.Handled = true;
            return;
        }

        Close(new BooleanConditionEditorDialogResult(Editor.FormulaText.Trim(), variables));
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
            || e.PropertyName == nameof(MainWindowViewModel.EditorDialogSectionContentBackground))
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
        EditorBackground = viewModel?.ParameterEditBackgrundColor ?? "#FFFFFF";
        EditorForeground = viewModel?.ParameterEditForeColor ?? "#111827";
        ParameterHoverColor = viewModel?.ParameterHoverColor ?? "#BDBDBD";
        SectionBackground = viewModel?.EditorDialogSectionContentBackground ?? "#EEF3F8";
    }

    private void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetAndRaise(ref string field, string value, string propertyName)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }
}