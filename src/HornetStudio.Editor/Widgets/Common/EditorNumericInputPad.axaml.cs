using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using HornetStudio.Editor.ViewModels;

namespace HornetStudio.Editor.Widgets;

public partial class EditorNumericInputPad : UserControl
{
    private const double KeyButtonWidth = 112;
    private const double ActionButtonWidth = 112;
    private const double ButtonSpacing = 12;
    private MainWindowViewModel? _subscribedViewModel;

    public EditorNumericInputPad()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => AttachToViewModel(ViewModel);
        DetachedFromVisualTree += (_, _) => AttachToViewModel(null);
    }

    public event EventHandler<string>? KeyInvoked;
    public event EventHandler<string>? ActionInvoked;

    public double PreferredWidth { get; private set; }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    public void Configure(bool allowDecimal, bool allowSign)
    {
        var labels = new List<string>
        {
            "7", "8", "9",
            "4", "5", "6",
            "1", "2", "3",
            "0"
        };

        if (allowDecimal)
        {
            labels.Add(".");
        }

        if (allowSign)
        {
            labels.Add("+/-");
        }

        var inputGrid = this.FindControl<UniformGrid>("InputGrid")!;
        inputGrid.Children.Clear();
        foreach (var label in labels)
        {
            inputGrid.Children.Add(CreateButton(label, false));
        }

        var actionPanel = this.FindControl<StackPanel>("ActionPanel")!;
        actionPanel.Children.Clear();
        foreach (var label in new[] { "DEL", "Clear", "Cancel", "OK" })
        {
            actionPanel.Children.Add(CreateButton(label, true));
        }

        PreferredWidth = (3 * (KeyButtonWidth + ButtonSpacing)) + 16 + ActionButtonWidth;
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
            or nameof(MainWindowViewModel.EditPanelButtonBorderBrush))
        {
            RefreshButtonChrome();
        }
    }

    private Button CreateButton(string label, bool action)
    {
        var button = new Button
        {
            Content = label,
            Width = action ? ActionButtonWidth : KeyButtonWidth,
            MinHeight = 48,
            Margin = action ? default : new Thickness(0, 0, ButtonSpacing, ButtonSpacing),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Tag = label
        };
        ApplyButtonChrome(button);
        button.Click += OnButtonClicked;
        button.PointerEntered += (_, _) => ApplyButtonChrome(button, true);
        button.PointerExited += (_, _) => ApplyButtonChrome(button);
        return button;
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
