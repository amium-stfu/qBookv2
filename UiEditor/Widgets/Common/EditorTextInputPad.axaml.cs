using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public partial class EditorTextInputPad : UserControl
{
    private const double KeyButtonWidth = 84;
    private const double KeyButtonHeight = 56;
    private const double ButtonSpacing = 8;
    private const double ActionButtonWidth = 92;
    private const double ActionButtonHeight = 86;
    private const double ShiftButtonWidth = 84;
    private const double SpaceButtonWidth = 236;

    private readonly List<Button> _buttons = [];
    private readonly List<(Button Button, string Lower, string Upper)> _letterButtons = [];
    private MainWindowViewModel? _subscribedViewModel;
    private Button? _shiftButton;
    private bool _isShiftEnabled;

    public EditorTextInputPad()
    {
        InitializeComponent();
        BuildLayout();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => AttachToViewModel(ViewModel);
        DetachedFromVisualTree += (_, _) => AttachToViewModel(null);
    }

    public event EventHandler<string>? KeyInvoked;
    public event EventHandler<string>? ActionInvoked;

    public double PreferredWidth { get; private set; }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    public void RefreshButtonChrome()
    {
        foreach (var button in _buttons)
        {
            ApplyButtonChrome(button, false);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        AttachToViewModel(ViewModel);
        RefreshButtonChrome();
    }

    private void AttachToViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_subscribedViewModel, viewModel))
        {
            return;
        }

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _subscribedViewModel = viewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.ButtonBackColor)
            or nameof(MainWindowViewModel.ButtonHoverColor)
            or nameof(MainWindowViewModel.ButtonForeColor)
            or nameof(MainWindowViewModel.EditPanelButtonBorderBrush)
            or nameof(MainWindowViewModel.HeaderBadgeBackground)
            or nameof(MainWindowViewModel.HeaderBadgeForeground))
        {
            RefreshButtonChrome();
        }
    }

    private void BuildLayout()
    {
        AddRow(this.FindControl<StackPanel>("SpecialRow")!, new[] { "!", "\"", "§", "$", "%", "&", "/", "(", ")", "=", "?" });
        AddLetterRow(this.FindControl<StackPanel>("QwertzRow")!, new[] { "q", "w", "e", "r", "t", "z", "u", "i", "o", "p" });
        AddLetterRow(this.FindControl<StackPanel>("HomeRow")!, new[] { "a", "s", "d", "f", "g", "h", "j", "k", "l" });
        AddShiftRow();
        AddBottomRow();
        BuildNumberPad();
        BuildActionGrid();
        PreferredWidth = 11 * KeyButtonWidth + 10 * ButtonSpacing + 20 + 3 * KeyButtonWidth + 2 * ButtonSpacing + 20 + ActionButtonWidth;
        UpdateLetterLabels();
        RefreshButtonChrome();
    }

    private void AddShiftRow()
    {
        var row = this.FindControl<StackPanel>("ShiftRow")!;
        _shiftButton = CreateButton("Shift", "Shift", ShiftButtonWidth, KeyButtonHeight, isAction: true);
        row.Children.Add(_shiftButton);
        AddLetterRow(row, new[] { "y", "x", "c", "v", "b", "n", "m" });
        row.Children.Add(CreateButton(".", ".", KeyButtonWidth, KeyButtonHeight));
        row.Children.Add(CreateButton(",", ",", KeyButtonWidth, KeyButtonHeight));
    }

    private void AddBottomRow()
    {
        var row = this.FindControl<StackPanel>("BottomRow")!;
        row.Children.Add(CreateButton("_", "_", KeyButtonWidth, KeyButtonHeight));
        row.Children.Add(CreateButton("/", "/", KeyButtonWidth, KeyButtonHeight));
        row.Children.Add(CreateButton("\\", "\\", KeyButtonWidth, KeyButtonHeight));
        row.Children.Add(CreateButton("Space", " ", SpaceButtonWidth, KeyButtonHeight));
        row.Children.Add(CreateButton("+", "+", KeyButtonWidth, KeyButtonHeight));
        row.Children.Add(CreateButton("-", "-", KeyButtonWidth, KeyButtonHeight));
        row.Children.Add(CreateButton("°", "°", KeyButtonWidth, KeyButtonHeight));
    }

    private void AddRow(StackPanel row, IReadOnlyList<string> labels)
    {
        foreach (var label in labels)
        {
            row.Children.Add(CreateButton(label, label, KeyButtonWidth, KeyButtonHeight));
        }
    }

    private void AddLetterRow(StackPanel row, IReadOnlyList<string> labels)
    {
        foreach (var label in labels)
        {
            var button = CreateButton(label, label, KeyButtonWidth, KeyButtonHeight);
            _letterButtons.Add((button, label.ToLowerInvariant(), label.ToUpperInvariant()));
            row.Children.Add(button);
        }
    }

    private void BuildNumberPad()
    {
        var grid = this.FindControl<Grid>("NumberPadGrid")!;
        for (var row = 0; row < 4; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (var column = 0; column < 3; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        AddGridButton(grid, "1", "1", 0, 0);
        AddGridButton(grid, "2", "2", 0, 1);
        AddGridButton(grid, "3", "3", 0, 2);
        AddGridButton(grid, "4", "4", 1, 0);
        AddGridButton(grid, "5", "5", 1, 1);
        AddGridButton(grid, "6", "6", 1, 2);
        AddGridButton(grid, "7", "7", 2, 0);
        AddGridButton(grid, "8", "8", 2, 1);
        AddGridButton(grid, "9", "9", 2, 2);
        AddGridButton(grid, "0", "0", 3, 0, columnSpan: 2);
        AddGridButton(grid, ".", ".", 3, 2);
    }

    private void BuildActionGrid()
    {
        var grid = this.FindControl<Grid>("ActionGrid")!;
        for (var row = 0; row < 4; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        AddGridButton(grid, "DEL", "Backspace", 0, 0, width: ActionButtonWidth, height: KeyButtonHeight, isAction: true);
        AddGridButton(grid, "<", "MoveLeft", 1, 0, width: ActionButtonWidth, height: KeyButtonHeight, isAction: true);
        AddGridButton(grid, "OK", "Apply", 2, 0, width: ActionButtonWidth, height: ActionButtonHeight, isAction: true);
        AddGridButton(grid, "X", "Cancel", 3, 0, width: ActionButtonWidth, height: ActionButtonHeight, isAction: true);
    }

    private void AddGridButton(Grid grid, string content, string value, int row, int column, int columnSpan = 1, double? width = null, double? height = null, bool isAction = false)
    {
        var button = CreateButton(content, value, width ?? KeyButtonWidth, height ?? KeyButtonHeight, isAction);
        Grid.SetRow(button, row);
        Grid.SetColumn(button, column);
        Grid.SetColumnSpan(button, columnSpan);
        grid.Children.Add(button);
    }

    private Button CreateButton(string content, string value, double width, double height, bool isAction = false)
    {
        var button = new Button
        {
            Content = content,
            Tag = value,
            Width = width,
            Height = height,
            Margin = new Thickness(0, 0, ButtonSpacing, ButtonSpacing),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold
        };

        if (value.Length == 1 && char.IsLetter(value[0]))
        {
            button.Classes.Add("letter-key");
        }

        if (isAction)
        {
            button.Classes.Add("action-key");
        }

        button.Click += OnButtonClicked;
        button.PointerEntered += (_, _) => ApplyButtonChrome(button, true);
        button.PointerExited += (_, _) => ApplyButtonChrome(button, false);
        _buttons.Add(button);
        return button;
    }

    private void OnButtonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } button)
        {
            return;
        }

        if (value == "Shift")
        {
            _isShiftEnabled = !_isShiftEnabled;
            UpdateLetterLabels();
            RefreshButtonChrome();
            e.Handled = true;
            return;
        }

        if (button.Classes.Contains("action-key"))
        {
            ActionInvoked?.Invoke(this, value);
            e.Handled = true;
            return;
        }

        var output = value;
        if (button.Classes.Contains("letter-key"))
        {
            output = _isShiftEnabled ? value.ToUpperInvariant() : value.ToLowerInvariant();
        }

        KeyInvoked?.Invoke(this, output);
        e.Handled = true;
    }

    private void UpdateLetterLabels()
    {
        foreach (var (button, lower, upper) in _letterButtons)
        {
            button.Content = _isShiftEnabled ? upper : lower;
            button.Tag = lower;
        }
    }

    private void ApplyButtonChrome(Button button, bool isPointerOver)
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        var isShiftButton = ReferenceEquals(button, _shiftButton);
        var useAccent = isShiftButton && _isShiftEnabled;
        var background = useAccent
            ? vm.HeaderBadgeBackground
            : isPointerOver
                ? vm.ButtonHoverColor
                : vm.ButtonBackColor;
        var foreground = useAccent ? vm.HeaderBadgeForeground : vm.ButtonForeColor;

        button.Background = Brush.Parse(background);
        button.BorderBrush = Brush.Parse(vm.EditPanelButtonBorderBrush);
        button.Foreground = Brush.Parse(foreground);
        button.BorderThickness = new Thickness(1);
        button.CornerRadius = new CornerRadius(8);
        button.Opacity = 1;
    }
}