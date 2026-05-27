using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HornetStudio.Editor.Widgets.Common;

namespace HornetStudio.Editor.Controls;

/// <summary>
/// Routed event arguments for one boolean condition variable source picker request.
/// </summary>
public sealed class ConditionVariablePickerRequestedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionVariablePickerRequestedEventArgs"/> class.
    /// </summary>
    /// <param name="variable">The requested variable entry.</param>
    public ConditionVariablePickerRequestedEventArgs(ConditionVariableEntryViewModel variable)
    {
        Variable = variable ?? throw new ArgumentNullException(nameof(variable));
    }

    /// <summary>
    /// Gets the variable entry for which a source should be selected.
    /// </summary>
    public ConditionVariableEntryViewModel Variable { get; }
}

/// <summary>
/// Provides a reusable editor surface for boolean conditions with variable rows, token buttons, and live validation.
/// </summary>
public partial class BooleanConditionEditorControl : UserControl
{
    /// <summary>
    /// Identifies the <see cref="Editor"/> styled property.
    /// </summary>
    public static readonly StyledProperty<BooleanConditionEditorViewModel?> EditorProperty =
        AvaloniaProperty.Register<BooleanConditionEditorControl, BooleanConditionEditorViewModel?>(nameof(Editor));

    public static readonly StyledProperty<string> SectionBackgroundProperty =
        AvaloniaProperty.Register<BooleanConditionEditorControl, string>(nameof(SectionBackground), "#EEF3F8");

    public static readonly StyledProperty<string> BorderColorProperty =
        AvaloniaProperty.Register<BooleanConditionEditorControl, string>(nameof(BorderColor), "#CBD5E1");

    public static readonly StyledProperty<string> PrimaryTextBrushProperty =
        AvaloniaProperty.Register<BooleanConditionEditorControl, string>(nameof(PrimaryTextBrush), "#111827");

    public static readonly StyledProperty<string> SecondaryTextBrushProperty =
        AvaloniaProperty.Register<BooleanConditionEditorControl, string>(nameof(SecondaryTextBrush), "#5E6777");

    public static readonly StyledProperty<string> EditorBackgroundProperty =
        AvaloniaProperty.Register<BooleanConditionEditorControl, string>(nameof(EditorBackground), "#FFFFFF");

    public static readonly StyledProperty<string> EditorForegroundProperty =
        AvaloniaProperty.Register<BooleanConditionEditorControl, string>(nameof(EditorForeground), "#111827");

    public static readonly StyledProperty<string> ButtonBackgroundProperty =
        AvaloniaProperty.Register<BooleanConditionEditorControl, string>(nameof(ButtonBackground), "#F8FAFC");

    public static readonly StyledProperty<string> ButtonBorderBrushProperty =
        AvaloniaProperty.Register<BooleanConditionEditorControl, string>(nameof(ButtonBorderBrush), "#CBD5E1");

    public static readonly StyledProperty<string> ButtonForegroundProperty =
        AvaloniaProperty.Register<BooleanConditionEditorControl, string>(nameof(ButtonForeground), "#111827");

    public static readonly StyledProperty<string> ParameterHoverColorProperty =
        AvaloniaProperty.Register<BooleanConditionEditorControl, string>(nameof(ParameterHoverColor), "#93C5FD");

    private TextBox? _formulaEditorTextBox;

    /// <summary>
    /// Initializes a new instance of the <see cref="BooleanConditionEditorControl"/> class.
    /// </summary>
    public BooleanConditionEditorControl()
    {
        InitializeComponent();
        _formulaEditorTextBox = this.FindControl<TextBox>("FormulaEditorTextBox");
    }

    /// <summary>
    /// Occurs when the host should provide a source path for a condition variable.
    /// </summary>
    public event EventHandler<ConditionVariablePickerRequestedEventArgs>? VariableSourcePickRequested;

    /// <summary>
    /// Gets or sets the shared boolean condition editor view model.
    /// </summary>
    public BooleanConditionEditorViewModel? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>
    /// Gets or sets the section background brush value.
    /// </summary>
    public string SectionBackground
    {
        get => GetValue(SectionBackgroundProperty);
        set => SetValue(SectionBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the border color brush value.
    /// </summary>
    public string BorderColor
    {
        get => GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the primary text brush value.
    /// </summary>
    public string PrimaryTextBrush
    {
        get => GetValue(PrimaryTextBrushProperty);
        set => SetValue(PrimaryTextBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the secondary text brush value.
    /// </summary>
    public string SecondaryTextBrush
    {
        get => GetValue(SecondaryTextBrushProperty);
        set => SetValue(SecondaryTextBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the editor background brush value.
    /// </summary>
    public string EditorBackground
    {
        get => GetValue(EditorBackgroundProperty);
        set => SetValue(EditorBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the editor foreground brush value.
    /// </summary>
    public string EditorForeground
    {
        get => GetValue(EditorForegroundProperty);
        set => SetValue(EditorForegroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the button background brush value.
    /// </summary>
    public string ButtonBackground
    {
        get => GetValue(ButtonBackgroundProperty);
        set => SetValue(ButtonBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the button border brush value.
    /// </summary>
    public string ButtonBorderBrush
    {
        get => GetValue(ButtonBorderBrushProperty);
        set => SetValue(ButtonBorderBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the button foreground brush value.
    /// </summary>
    public string ButtonForeground
    {
        get => GetValue(ButtonForegroundProperty);
        set => SetValue(ButtonForegroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the hover color used by editor fields.
    /// </summary>
    public string ParameterHoverColor
    {
        get => GetValue(ParameterHoverColorProperty);
        set => SetValue(ParameterHoverColorProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether an editor is currently assigned.
    /// </summary>
    public bool HasEditor => Editor is not null;

    private void OnAddVariableClicked(object? sender, RoutedEventArgs e)
    {
        Editor?.AddVariable();
        e.Handled = true;
    }

    private void OnRemoveVariableClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: ConditionVariableEntryViewModel variable })
        {
            Editor?.RemoveVariable(variable);
            e.Handled = true;
        }
    }

    private void OnPickVariableSourceClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: ConditionVariableEntryViewModel variable })
        {
            VariableSourcePickRequested?.Invoke(this, new ConditionVariablePickerRequestedEventArgs(variable));
            e.Handled = true;
        }
    }

    private void OnInsertFormulaTokenClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: FormulaInsertButtonDefinition token } || Editor is null)
        {
            return;
        }

        var caretIndex = _formulaEditorTextBox?.CaretIndex ?? Editor.FormulaText.Length;
        var nextCaretIndex = Editor.InsertFormulaToken(token.Token, caretIndex, token.CaretBacktrack);
        if (_formulaEditorTextBox is not null)
        {
            _formulaEditorTextBox.Focus();
            _formulaEditorTextBox.CaretIndex = Math.Clamp(nextCaretIndex, 0, _formulaEditorTextBox.Text?.Length ?? 0);
        }

        e.Handled = true;
    }
}