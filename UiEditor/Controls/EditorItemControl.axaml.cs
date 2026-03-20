using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using UiEditor.Models;
using UiEditor.ViewModels;

namespace UiEditor.Controls;

public partial class EditorItemControl : UserControl
{
    public EditorItemControl()
    {
        InitializeComponent();
        this.FindControl<ParameterControl>("ParameterPresenter")!.BitChoiceClicked += OnBitChoiceClicked;
    }

    private PageItemModel? Item => DataContext as PageItemModel;

    private MainWindowViewModel? ViewModel
        => this.GetVisualRoot() is Window { DataContext: MainWindowViewModel viewModel } ? viewModel : null;

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
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
        if (Item is null || ViewModel is null || this.GetVisualAncestors().OfType<PageEditorControl>().FirstOrDefault() is not { } editor)
        {
            return;
        }

        var anchor = this.TranslatePoint(new Point(Bounds.Width + 8, 0), editor) ?? new Point(24, 24);
        ViewModel.OpenItemEditor(Item, anchor.X, anchor.Y);
        e.Handled = true;
    }
}
