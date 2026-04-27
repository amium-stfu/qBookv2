using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Amium.UiEditor.Controls;
using Amium.Host;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public partial class EditorButtonControl : EditorTemplateWidget
{
    private FolderItemModel? Item => DataContext as FolderItemModel;

    private MainWindowViewModel? ViewModel
        => this.GetVisualRoot() is Window { DataContext: MainWindowViewModel viewModel } ? viewModel : null;

    public EditorButtonControl()
    {
        InitializeComponent();
    }

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandleInteractivePointerPressed(e);
    }

    private void OnButtonReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || Item is null)
        {
            return;
        }

        var viewModel = ViewModel;
        if (viewModel is { IsEditMode: true, IsShiftInteractionMode: false })
        {
            return;
        }

        if (Item.TryExecuteInteraction(ItemInteractionEvent.BodyLeftClick, viewModel, out _))
        {
            e.Handled = true;
        }
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        HandleSettingsClicked(e);
    }
}

public partial class EditorButtonWidget : EditorButtonControl
{
}

