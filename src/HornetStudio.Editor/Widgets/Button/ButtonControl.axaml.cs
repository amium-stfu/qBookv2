using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using HornetStudio.Host;
using HornetStudio.Editor.Models;
using HornetStudio.Editor.ViewModels;

namespace HornetStudio.Editor.Widgets;

public partial class ButtonControl : UserControl
{
    private FolderItemModel? Item => DataContext as FolderItemModel;

    private MainWindowViewModel? ViewModel
        => this.GetVisualRoot() is Window { DataContext: MainWindowViewModel viewModel } ? viewModel : null;

    public ButtonControl()
    {
        InitializeComponent();

        // Ensure we see pointer events anywhere inside this control
        AddHandler(InputElement.PointerPressedEvent, OnButtonPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(InputElement.PointerReleasedEvent, OnButtonReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    private void OnButtonPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Item is not { Enabled: true })
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (point.Properties.PointerUpdateKind is PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.RightButtonPressed)
        {
            RootBorder.Classes.Add("pressed");
        }
    }

    private void OnButtonReleased(object? sender, PointerReleasedEventArgs e)
    {
        RootBorder.Classes.Remove("pressed");

        if (Item is null)
        {
            return;
        }

        var viewModel = ViewModel;
        if (viewModel is { IsEditMode: true, IsShiftInteractionMode: false })
        {
            return;
        }

        var interactionEvent = e.InitialPressMouseButton switch
        {
            MouseButton.Left => ItemInteractionEvent.BodyLeftClick,
            MouseButton.Right => ItemInteractionEvent.BodyRightClick,
            _ => (ItemInteractionEvent?)null
        };

        if (interactionEvent is not null)
        {
            if (Item.TryExecuteInteraction(interactionEvent.Value, viewModel, out _))
            {
                e.Handled = true;
                return;
            }

            if (interactionEvent == ItemInteractionEvent.BodyLeftClick && Item.TryExecuteButtonCommand(out _))
            {
                e.Handled = true;
            }
        }
    }
}
