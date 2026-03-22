using System;
using Avalonia.Input;
using Avalonia.Interactivity;
using Amium.Host;

namespace Amium.UiEditor.Controls;

public partial class EditorButtonControl : EditorTemplateControl
{
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
