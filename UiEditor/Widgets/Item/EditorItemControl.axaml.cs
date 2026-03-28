using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Amium.EditorUi.Controls;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public partial class EditorItemControl : EditorTemplateWidget
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
        if (ViewModel?.IsEditMode == true || Item is null)
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

        var interactionEvent = GetInteractionEvent(e, sender as Control);
        if (interactionEvent is not null)
        {
            if (Item.TryExecuteInteraction(interactionEvent.Value, viewModel, out _))
            {
                e.Handled = true;
                return;
            }

            if (interactionEvent != ItemInteractionEvent.BodyLeftClick || Item.HasInteractionRules || !Item.CanOpenValueEditor)
            {
                return;
            }
        }

        if (!Item.CanOpenValueEditor)
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

        _ = Item.TrySendInput(e.Value, out _);
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        HandleSettingsClicked(e);
    }

    private static ItemInteractionEvent? GetInteractionEvent(PointerPressedEventArgs e, Control? control)
    {
        var point = e.GetCurrentPoint(control);
        return point.Properties.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => ItemInteractionEvent.BodyLeftClick,
            PointerUpdateKind.RightButtonPressed => ItemInteractionEvent.BodyRightClick,
            _ => null
        };
    }
}

public partial class EditorItemWidget : EditorItemControl
{
}
