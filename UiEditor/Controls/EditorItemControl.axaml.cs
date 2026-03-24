using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Amium.EditorUi.Controls;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Controls;

public partial class EditorItemControl : EditorTemplateControl
{
    private PageItemModel? Item => DataContext as PageItemModel;

    private MainWindowViewModel? ViewModel
        => this.GetVisualRoot() is Window { DataContext: MainWindowViewModel viewModel } ? viewModel : null;

    public EditorItemControl()
    {
        InitializeComponent();
        var parameterPresenter = this.FindControl<ParameterControl>("ParameterPresenter")!;
        parameterPresenter.BitChoiceClicked += OnBitChoiceClicked;
        parameterPresenter.BoolChoiceClicked += OnBoolChoiceClicked;
    }

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandleInteractivePointerPressed(e);
    }

    private void OnParameterPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel?.IsEditMode == true || Item is null || !Item.CanOpenValueEditor)
        {
            return;
        }

        if (Item.TargetParameterView.Definition.Kind == ParameterVisualKind.Bits)
        {
            e.Handled = true;
            return;
        }

        var viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        viewModel.OpenValueInput(Item);
        e.Handled = true;
    }

    private void OnBitChoiceClicked(object? sender, BitChoiceClickedEventArgs e)
    {
        if (Item is null || ViewModel?.IsEditMode == true)
        {
            return;
        }

        _ = Item.TryToggleTargetBit(e.BitIndex, out _);
    }

    private void OnBoolChoiceClicked(object? sender, BoolChoiceClickedEventArgs e)
    {
        if (Item is null || ViewModel?.IsEditMode == true)
        {
            return;
        }

        _ = Item.TryUpdateTargetParameterValue(e.Value, out _);
    }

    private void OnSubItemsClicked(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<Popup>("SubItemsPopup") is { } popup)
        {
            popup.IsOpen = !popup.IsOpen;
        }

        e.Handled = true;
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        HandleSettingsClicked(e);
    }
}
