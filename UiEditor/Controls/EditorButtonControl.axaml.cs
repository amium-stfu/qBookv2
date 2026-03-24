using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Amium.EditorUi.Controls;
using Amium.Host;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Controls;

public partial class EditorButtonControl : EditorTemplateControl
{
    private PageItemModel? Item => DataContext as PageItemModel;

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
        if (viewModel?.IsEditMode == true)
        {
            return;
        }

        var commandName = Item.EffectiveButtonCommand;
        if (string.IsNullOrWhiteSpace(commandName))
        {
            e.Handled = true;
            return;
        }

        if (!HostRegistries.Commands.Execute(commandName))
        {
            e.Handled = true;
            return;
        }

        e.Handled = true;
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        HandleSettingsClicked(e);
    }
}
