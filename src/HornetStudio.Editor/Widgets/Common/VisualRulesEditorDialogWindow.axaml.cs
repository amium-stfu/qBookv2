using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using HornetStudio.Editor.Helpers;
using HornetStudio.Editor.Models;
using HornetStudio.Editor.ViewModels;

namespace HornetStudio.Editor.Widgets;

/// <summary>
/// Converts persisted visual rule source paths to concise display text.
/// </summary>
public sealed class VisualRuleSourcePathDisplayConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => VisualRulesEditorDialogWindow.GetSourcePathDisplayText(value as string);

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value ?? string.Empty;
}

public partial class VisualRulesEditorDialogWindow : Window, INotifyPropertyChanged
{
    private static readonly IReadOnlyList<Color> StandardColors =
    [
        Color.Parse("#1B19D8"),
        Color.Parse("#C62828"),
        Color.Parse("#118C22"),
        Color.Parse("#7E3CCB"),
        Color.Parse("#FFFFFF"),
        Color.Parse("#3A8EE6"),
        Color.Parse("#5F7433"),
        Color.Parse("#4F477D"),
        Color.Parse("#A55B12"),
        Color.Parse("#FFAF1A"),
        Color.Parse("#111111"),
        Color.Parse("#FF1F92"),
        Color.Parse("#6B63C8"),
        Color.Parse("#D46BD4"),
        Color.Parse("#B8860B"),
        Color.Parse("#8FAFB1"),
        Color.Parse("#FF1A1A"),
        Color.Parse("#FFFF00"),
        Color.Parse("#BDBDBD"),
        Color.Parse("#EAEAEA")
    ];

    private readonly EditorDialogField _field;
    private readonly MainWindowViewModel? _viewModel;
    private Action<string>? _applySelectedColor;
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
    private string _newSourceKind = nameof(VisualRuleSourceKind.MonitorRule);
    private string _newSourcePath = string.Empty;
    private string _newTarget = nameof(VisualRuleTarget.Body);
    private string _newPropertyName = nameof(VisualRuleProperty.BodyBackColor);
    private string _newEffect = nameof(VisualRuleEffect.None);
    private string _newActiveValue = string.Empty;
    private string _newInactiveValue = string.Empty;

    public VisualRulesEditorDialogWindow()
    {
        _field = null!;
        Rows = [];
        SourceKindOptions = new ObservableCollection<string>(VisualRuleCodec.SourceKindOptions);
        SourceOptions = [];
        TargetOptions = new ObservableCollection<string>(VisualRuleCodec.TargetOptions);
        PropertyOptions = new ObservableCollection<string>(VisualRuleCodec.PropertyOptions);
        EffectOptions = new ObservableCollection<string>(VisualRuleCodec.EffectOptions);
        _newPropertyName = PropertyOptions.FirstOrDefault() ?? nameof(VisualRuleProperty.BodyBackColor);
        InitializeComponent();
        DataContext = this;
        UpdateThemeBindings();
        BuildColorPalette();
    }

    public VisualRulesEditorDialogWindow(MainWindowViewModel? viewModel, EditorDialogField field)
    {
        _viewModel = viewModel;
        _field = field;
        SourceKindOptions = new ObservableCollection<string>(VisualRuleCodec.SourceKindOptions);
        SourceOptions = new ObservableCollection<string>(GetSourceOptions(viewModel, field));
        TargetOptions = new ObservableCollection<string>(VisualRuleCodec.TargetOptions);
        PropertyOptions = new ObservableCollection<string>(GetPropertyOptions(viewModel, field));
        EffectOptions = new ObservableCollection<string>(VisualRuleCodec.EffectOptions);
        Rows = new ObservableCollection<ItemVisualRuleEditorRow>(VisualRuleCodec.ParseDefinitions(field.Value).Select(CreateRow));
        _newPropertyName = PropertyOptions.FirstOrDefault() ?? nameof(VisualRuleProperty.BodyBackColor);
        InitializeComponent();
        DataContext = this;
        UpdateThemeBindings();
        BuildColorPalette();
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ItemVisualRuleEditorRow> Rows { get; }

    public ObservableCollection<string> SourceKindOptions { get; }

    public ObservableCollection<string> SourceOptions { get; }

    public ObservableCollection<string> TargetOptions { get; }

    public ObservableCollection<string> PropertyOptions { get; }

    public ObservableCollection<string> EffectOptions { get; }

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

    public string NewSourceKind
    {
        get => _newSourceKind;
        set => SetAndRaise(ref _newSourceKind, string.IsNullOrWhiteSpace(value) ? nameof(VisualRuleSourceKind.MonitorRule) : value, nameof(NewSourceKind));
    }

    public string NewSourcePath
    {
        get => _newSourcePath;
        set => SetAndRaise(ref _newSourcePath, value ?? string.Empty, nameof(NewSourcePath));
    }

    public string NewTarget
    {
        get => _newTarget;
        set => SetAndRaise(ref _newTarget, string.IsNullOrWhiteSpace(value) ? nameof(VisualRuleTarget.Body) : value, nameof(NewTarget));
    }

    public string NewPropertyName
    {
        get => _newPropertyName;
        set => SetAndRaise(ref _newPropertyName, string.IsNullOrWhiteSpace(value) ? (PropertyOptions.FirstOrDefault() ?? nameof(VisualRuleProperty.BodyBackColor)) : value, nameof(NewPropertyName));
    }

    public string NewEffect
    {
        get => _newEffect;
        set => SetAndRaise(ref _newEffect, string.IsNullOrWhiteSpace(value) ? nameof(VisualRuleEffect.None) : value, nameof(NewEffect));
    }

    public string NewActiveValue
    {
        get => _newActiveValue;
        set => SetAndRaise(ref _newActiveValue, value ?? string.Empty, nameof(NewActiveValue));
    }

    public string NewInactiveValue
    {
        get => _newInactiveValue;
        set => SetAndRaise(ref _newInactiveValue, value ?? string.Empty, nameof(NewInactiveValue));
    }

    private void OnAddClicked(object? sender, RoutedEventArgs e)
    {
        var property = ParseProperty(NewPropertyName);
        Rows.Add(CreateRow(new VisualRule
        {
            SourceKind = ParseSourceKind(NewSourceKind),
            SourcePath = NewSourcePath,
            Target = VisualRuleCodec.GetCompatibilityTarget(property),
            Property = property,
            Effect = ParseEffect(NewEffect),
            ActiveValue = NewActiveValue,
            InactiveValue = NewInactiveValue
        }));
        e.Handled = true;
    }

    private void OnRemoveClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ItemVisualRuleEditorRow row })
        {
            Rows.Remove(row);
        }

        e.Handled = true;
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var allowedProperties = new HashSet<string>(PropertyOptions, StringComparer.Ordinal);
        _field.Value = VisualRuleCodec.SerializeDefinitions(Rows
            .Where(row => allowedProperties.Count == 0 || allowedProperties.Contains(row.PropertyName))
            .Select(static row =>
            {
                var property = Enum.TryParse<VisualRuleProperty>(row.PropertyName, ignoreCase: true, out var parsedProperty)
                    ? parsedProperty
                    : VisualRuleProperty.BodyBackColor;

                return new VisualRule
                {
                    SourceKind = Enum.TryParse<VisualRuleSourceKind>(row.SourceKind, ignoreCase: true, out var sourceKind)
                        ? sourceKind
                        : VisualRuleSourceKind.MonitorRule,
                    SourcePath = row.SourcePath,
                    Target = VisualRuleCodec.GetCompatibilityTarget(property),
                    Property = property,
                    Effect = Enum.TryParse<VisualRuleEffect>(row.Effect, ignoreCase: true, out var effect)
                        ? effect
                        : VisualRuleEffect.None,
                    ActiveValue = row.ActiveValue,
                    InactiveValue = row.InactiveValue
                };
            }));
        Close();
        e.Handled = true;
    }

    private void OnRowActiveColorPickerClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ItemVisualRuleEditorRow row } control)
        {
            OpenColorPopup(control, row.ActiveValue, value => row.ActiveValue = value);
        }

        e.Handled = true;
    }

    private void OnRowInactiveColorPickerClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ItemVisualRuleEditorRow row } control)
        {
            OpenColorPopup(control, row.InactiveValue, value => row.InactiveValue = value);
        }

        e.Handled = true;
    }

    private void OnNewActiveColorPickerClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
        {
            OpenColorPopup(control, NewActiveValue, value => NewActiveValue = value);
        }

        e.Handled = true;
    }

    private void OnNewInactiveColorPickerClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
        {
            OpenColorPopup(control, NewInactiveValue, value => NewInactiveValue = value);
        }

        e.Handled = true;
    }

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnPaletteColorClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string colorText } || _applySelectedColor is null)
        {
            return;
        }

        _applySelectedColor(colorText);
        CloseColorPopup();
        e.Handled = true;
    }

    private void OnTransparentColorClicked(object? sender, RoutedEventArgs e)
    {
        _applySelectedColor?.Invoke("Transparent");
        CloseColorPopup();
        e.Handled = true;
    }

    private void OnDefaultColorClicked(object? sender, RoutedEventArgs e)
    {
        _applySelectedColor?.Invoke(string.Empty);
        CloseColorPopup();
        e.Handled = true;
    }

    private void OnApplyHexColorClicked(object? sender, RoutedEventArgs e)
    {
        ApplyHexColorFromEditor();
        e.Handled = true;
    }

    private void OnColorHexTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        ApplyHexColorFromEditor();
        e.Handled = true;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close();
        e.Handled = true;
    }

    private ItemVisualRuleEditorRow CreateRow(VisualRule rule)
    {
        var row = new ItemVisualRuleEditorRow
        {
            SourceKind = rule.SourceKind.ToString(),
            SourcePath = rule.SourcePath,
            Target = VisualRuleCodec.GetCompatibilityTarget(rule.Property).ToString(),
            PropertyName = rule.Property.ToString(),
            Effect = rule.Effect.ToString(),
            ActiveValue = rule.ActiveValue,
            InactiveValue = rule.InactiveValue
        };

        foreach (var option in VisualRuleCodec.SourceKindOptions)
        {
            row.SourceKindOptions.Add(option);
        }

        foreach (var option in SourceOptions)
        {
            row.SourceOptions.Add(option);
        }

        if (!string.IsNullOrWhiteSpace(row.SourcePath) && !row.SourceOptions.Contains(row.SourcePath))
        {
            row.SourceOptions.Add(row.SourcePath);
        }

        foreach (var option in VisualRuleCodec.TargetOptions)
        {
            row.TargetOptions.Add(option);
        }

        foreach (var option in PropertyOptions)
        {
            row.PropertyOptions.Add(option);
        }

        if (!string.IsNullOrWhiteSpace(row.PropertyName) && !row.PropertyOptions.Contains(row.PropertyName))
        {
            row.PropertyOptions.Add(row.PropertyName);
        }

        foreach (var option in VisualRuleCodec.EffectOptions)
        {
            row.EffectOptions.Add(option);
        }

        return row;
    }

    private void OpenColorPopup(Control placementTarget, string currentValue, Action<string> applyColor)
    {
        _applySelectedColor = applyColor;
        if (this.FindControl<TextBox>("ColorHexTextBox") is { } hexBox)
        {
            hexBox.Text = currentValue;
        }

        if (this.FindControl<Popup>("ColorPopup") is { } popup)
        {
            popup.PlacementTarget = placementTarget;
            popup.IsOpen = true;
        }
    }

    private void BuildColorPalette()
    {
        if (this.FindControl<WrapPanel>("ColorPalettePanel") is not { } panel || panel.Children.Count > 0)
        {
            return;
        }

        foreach (var color in StandardColors)
        {
            var colorText = ToHex(color);
            var button = new Button
            {
                Width = 32,
                Height = 32,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 6, 6),
                Tag = colorText,
                Content = new Border
                {
                    Width = 24,
                    Height = 24,
                    Background = new SolidColorBrush(color),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6)
                },
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            button.Click += OnPaletteColorClicked;
            button.PointerPressed += OnInteractivePointerPressed;
            panel.Children.Add(button);
        }
    }

    private void ApplyHexColorFromEditor()
    {
        if (_applySelectedColor is null || this.FindControl<TextBox>("ColorHexTextBox") is not { } hexBox)
        {
            return;
        }

        _applySelectedColor(NormalizeColorText(hexBox.Text));
        CloseColorPopup();
    }

    private void CloseColorPopup()
    {
        if (this.FindControl<Popup>("ColorPopup") is { } popup)
        {
            popup.IsOpen = false;
        }
    }

    private static string NormalizeColorText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "Transparent", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Transparent";
        }

        return Color.TryParse(trimmed, out var color) ? ToHex(color) : trimmed;
    }

    private static string ToHex(Color color)
        => color.A == byte.MaxValue
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>
    /// Gets concise display text for a visual rule source path without changing the persisted source path.
    /// </summary>
    /// <param name="sourcePath">The persisted source path.</param>
    /// <returns>The shortened display path.</returns>
    public static string GetSourcePathDisplayText(string? sourcePath)
    {
        var normalized = TargetPathHelper.NormalizePathDelimiters(sourcePath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var segments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length >= 5
            && string.Equals(segments[0], "studio", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[2], "monitor", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join('.', segments.Skip(3));
        }

        if (segments.Length >= 3 && string.Equals(segments[0], "monitor", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join('.', segments.Skip(1));
        }

        return normalized;
    }

    private static IEnumerable<string> GetSourceOptions(MainWindowViewModel? viewModel, EditorDialogField field)
    {
        if (viewModel is null)
        {
            return string.IsNullOrWhiteSpace(field.Value)
                ? []
                : VisualRuleCodec.ParseDefinitions(field.Value)
                    .Select(static rule => rule.SourcePath)
                    .Where(static path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(System.StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static path => path, System.StringComparer.OrdinalIgnoreCase)
                    .ToArray();
        }

        return viewModel.GetSelectableVisualRuleSourceOptions(field.OwnerItem);
    }

    private static IEnumerable<string> GetPropertyOptions(MainWindowViewModel? viewModel, EditorDialogField field)
    {
        if (viewModel is null)
        {
            var existingOptions = VisualRuleCodec.ParseDefinitions(field.Value)
                .Select(static rule => rule.Property.ToString())
                .Where(static property => !string.IsNullOrWhiteSpace(property))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return existingOptions.Length > 0
                ? existingOptions
                : VisualRuleCodec.PropertyOptions;
        }

        var options = viewModel.GetSelectableVisualRulePropertyOptions(field.OwnerItem);
        return options.Count > 0 ? options : VisualRuleCodec.PropertyOptions;
    }

    private VisualRuleSourceKind ParseSourceKind(string? value)
        => System.Enum.TryParse<VisualRuleSourceKind>(value, ignoreCase: true, out var result)
            ? result
            : VisualRuleSourceKind.MonitorRule;

    private VisualRuleTarget ParseTarget(string? value)
        => System.Enum.TryParse<VisualRuleTarget>(value, ignoreCase: true, out var result)
            ? result
            : VisualRuleTarget.Body;

    private VisualRuleProperty ParseProperty(string? value)
        => System.Enum.TryParse<VisualRuleProperty>(value, ignoreCase: true, out var result)
            ? result
            : VisualRuleProperty.BodyBackColor;

    private VisualRuleEffect ParseEffect(string? value)
        => System.Enum.TryParse<VisualRuleEffect>(value, ignoreCase: true, out var result)
            ? result
            : VisualRuleEffect.None;

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
