using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Controls;

public partial class EditorPropertyDialog : UserControl
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

    private EditorDialogField? _activeColorField;

    public EditorPropertyDialog()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => BuildColorPalette();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnOpenColorPickerClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: EditorDialogField field } control)
        {
            return;
        }

        _activeColorField = field;
        if (this.FindControl<TextBox>("ColorHexTextBox") is { } hexBox)
        {
            hexBox.Text = field.Value;
        }

        if (this.FindControl<Popup>("ColorPopup") is { } popup)
        {
            popup.PlacementTarget = control;
            popup.IsOpen = true;
        }

        e.Handled = true;
    }

    private void OnPaletteColorClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string colorText } || _activeColorField is null)
        {
            return;
        }

        _activeColorField.Value = colorText;
        CloseColorPopup();
        e.Handled = true;
    }

    private void OnTransparentColorClicked(object? sender, RoutedEventArgs e)
    {
        if (_activeColorField is not null)
        {
            _activeColorField.Value = "Transparent";
        }

        CloseColorPopup();
        e.Handled = true;
    }

    private void OnDefaultColorClicked(object? sender, RoutedEventArgs e)
    {
        if (_activeColorField is not null)
        {
            _activeColorField.Value = string.Empty;
        }

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

    private void OnAddChartSeriesClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: EditorDialogField field })
        {
            return;
        }

        field.AddChartSeriesEntry();
        e.Handled = true;
    }

    private void OnRemoveChartSeriesClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ChartSeriesEditorRow row } control)
        {
            return;
        }

        var field = control.GetVisualAncestors()
            .OfType<Control>()
            .Select(ancestor => ancestor.DataContext)
            .OfType<EditorDialogField>()
            .FirstOrDefault();

        field?.RemoveChartSeriesEntry(row);
        e.Handled = true;
    }

    private void OnConfirmClicked(object? sender, RoutedEventArgs e)
    {
        ViewModel?.CommitEditorDialog();
        e.Handled = true;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        ViewModel?.CancelEditorDialog();
        e.Handled = true;
    }

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        ViewModel?.CommitEditorDialog();
        e.Handled = true;
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
        if (_activeColorField is null || this.FindControl<TextBox>("ColorHexTextBox") is not { } hexBox)
        {
            return;
        }

        _activeColorField.Value = NormalizeColorText(hexBox.Text);
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
        if (string.Equals(trimmed, "Transparent", StringComparison.OrdinalIgnoreCase))
        {
            return "Transparent";
        }

        return Color.TryParse(trimmed, out var color) ? ToHex(color) : trimmed;
    }

    private static string ToHex(Color color)
        => color.A == byte.MaxValue
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
}
