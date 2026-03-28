using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public partial class EditorHexInputPad : UserControl
{
    private const double KeyButtonWidth = 112;
    private const double ActionButtonWidth = 112;
    private const double ButtonSpacing = 12;

    private MainWindowViewModel? _subscribedViewModel;

    public EditorHexInputPad()
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
            or nameof(MainWindowViewModel.EditPanelButtonBorderBrush))
        {
            RefreshButtonChrome();
        }
    }

    private void BuildLayout()
    {
        if (this.FindControl<Grid>("RootGrid") is not { } host)
        {
            return;
        }

        host.Children.Clear();
        host.RowDefinitions.Clear();
        host.ColumnDefinitions.Clear();

        host.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        host.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(24)));
        host.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        host.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(24)));
        host.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var alphaGrid = new Grid();
        for (var i = 0; i < 4; i++)
        {
            alphaGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        alphaGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        alphaGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        AddHexButton(alphaGrid, "A", 0, 0);
        AddHexButton(alphaGrid, "E", 0, 1);
        AddHexButton(alphaGrid, "B", 1, 0);
        AddHexButton(alphaGrid, "F", 1, 1);
        AddHexButton(alphaGrid, "C", 2, 0);
        AddHexButton(alphaGrid, "D", 3, 0);
        Grid.SetColumn(alphaGrid, 0);
        host.Children.Add(alphaGrid);

        var numericGrid = new Grid();
        for (var i = 0; i < 4; i++)
        {
            numericGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (var i = 0; i < 3; i++)
        {
            numericGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        AddHexButton(numericGrid, "7", 0, 0);
        AddHexButton(numericGrid, "8", 0, 1);
        AddHexButton(numericGrid, "9", 0, 2);
        AddHexButton(numericGrid, "4", 1, 0);
        AddHexButton(numericGrid, "5", 1, 1);
        AddHexButton(numericGrid, "6", 1, 2);
        AddHexButton(numericGrid, "1", 2, 0);
        AddHexButton(numericGrid, "2", 2, 1);
        AddHexButton(numericGrid, "3", 2, 2);
        AddHexButton(numericGrid, "0", 3, 1);
        Grid.SetColumn(numericGrid, 2);
        host.Children.Add(numericGrid);

        var actionsGrid = new Grid();
        for (var i = 0; i < 4; i++)
        {
            actionsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        AddHexActionButton(actionsGrid, "DEL", 0);
        AddHexActionButton(actionsGrid, "Clear", 1, "CLR");
        AddHexActionButton(actionsGrid, "Cancel", 2, "X");
        AddHexActionButton(actionsGrid, "OK", 3);
        Grid.SetColumn(actionsGrid, 4);
        host.Children.Add(actionsGrid);

        PreferredWidth = (2 * (KeyButtonWidth + ButtonSpacing)) + (3 * (KeyButtonWidth + ButtonSpacing)) + ActionButtonWidth + 48;
        RefreshButtonChrome();
    }

    private void AddHexButton(Grid grid, string label, int row, int column)
    {
        var button = new Button
        {
            Content = label,
            Width = KeyButtonWidth,
            MinHeight = 48,
            Margin = new Thickness(0, 0, ButtonSpacing, ButtonSpacing),
            Tag = label,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        button.Classes.Add("input-key");
        ApplyButtonChrome(button);
        button.Click += OnButtonClicked;
        button.PointerEntered += (_, _) => ApplyButtonChrome(button, true);
        button.PointerExited += (_, _) => ApplyButtonChrome(button);
        Grid.SetRow(button, row);
        Grid.SetColumn(button, column);
        grid.Children.Add(button);
    }

    private void AddHexActionButton(Grid grid, string action, int row, string? contentOverride = null)
    {
        var button = new Button
        {
            Content = contentOverride ?? action,
            Width = ActionButtonWidth,
            MinHeight = 48,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, ButtonSpacing),
            Tag = action
        };

        button.Classes.Add("action-key");
        ApplyButtonChrome(button);
        button.Click += OnButtonClicked;
        button.PointerEntered += (_, _) => ApplyButtonChrome(button, true);
        button.PointerExited += (_, _) => ApplyButtonChrome(button);
        Grid.SetRow(button, row);
        Grid.SetColumn(button, 0);
        grid.Children.Add(button);
    }

    private void OnButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value })
        {
            return;
        }

        if (value is "DEL" or "Clear" or "Cancel" or "OK")
        {
            ActionInvoked?.Invoke(this, value);
        }
        else
        {
            KeyInvoked?.Invoke(this, value);
        }

        e.Handled = true;
    }

    private void RefreshButtonChrome()
    {
        foreach (var button in this.GetVisualDescendants().OfType<Button>())
        {
            ApplyButtonChrome(button);
        }
    }

    private void ApplyButtonChrome(Button button, bool isPointerOver = false)
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        button.Background = Brush.Parse(isPointerOver ? vm.ButtonHoverColor : vm.ButtonBackColor);
        button.BorderBrush = Brush.Parse(vm.EditPanelButtonBorderBrush);
        button.Foreground = Brush.Parse(vm.ButtonForeColor);
        button.BorderThickness = new Thickness(1);
        button.CornerRadius = new CornerRadius(8);
        button.Opacity = 1;
    }
}