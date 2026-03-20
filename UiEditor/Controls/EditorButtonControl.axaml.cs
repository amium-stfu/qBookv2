using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using UiEditor.Models;
using UiEditor.ViewModels;

namespace UiEditor.Controls;

public partial class EditorButtonControl : UserControl
{
    public EditorButtonControl()
    {
        InitializeComponent();
    }

    private PageItemModel? Item => DataContext as PageItemModel;

    private MainWindowViewModel? ViewModel
        => this.GetVisualRoot() is Window { DataContext: MainWindowViewModel viewModel } ? viewModel : null;

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
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
