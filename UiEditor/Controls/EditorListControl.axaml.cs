using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using UiEditor.Models;
using UiEditor.ViewModels;

namespace UiEditor.Controls;

public partial class EditorListControl : UserControl
{
    private Border? _viewportBorder;
    private ListBox? _itemListBox;
    private ScrollViewer? _listScrollViewer;
    private INotifyCollectionChanged? _itemsCollection;

    public EditorListControl()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    private PageItemModel? ListItem => DataContext as PageItemModel;

    private MainWindowViewModel? ViewModel
        => this.GetVisualRoot() is Window { DataContext: MainWindowViewModel viewModel } ? viewModel : null;

    private void OnChildItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: PageItemModel childItem } || ListItem is null || ViewModel?.IsEditMode != true)
        {
            return;
        }

        ListItem.SelectedListItem = childItem;
        ViewModel.SelectItem(childItem);
        e.Handled = true;
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (ListItem is null || ViewModel is null || this.GetVisualAncestors().OfType<PageEditorControl>().FirstOrDefault() is not { } editor)
        {
            return;
        }

        var anchor = this.TranslatePoint(new Point(Bounds.Width + 8, 0), editor) ?? new Point(24, 24);
        ViewModel.OpenItemEditor(ListItem, anchor.X, anchor.Y);
        e.Handled = true;
    }

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _viewportBorder = this.FindControl<Border>("ListViewportBorder");
        _itemListBox = this.FindControl<ListBox>("ItemListBox");

        SizeChanged += OnAnySizeChanged;
        if (_viewportBorder is not null)
        {
            _viewportBorder.SizeChanged += OnAnySizeChanged;
        }

        if (_itemListBox is not null)
        {
            _itemListBox.SizeChanged += OnAnySizeChanged;
            ResolveAndTrackScrollViewer();
        }

        HookItemsCollection();
        Dispatcher.UIThread.Post(() => ResolveAndTrackScrollViewer(), DispatcherPriority.Loaded);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        SizeChanged -= OnAnySizeChanged;

        if (_viewportBorder is not null)
        {
            _viewportBorder.SizeChanged -= OnAnySizeChanged;
            _viewportBorder = null;
        }

        if (_itemListBox is not null)
        {
            _itemListBox.SizeChanged -= OnAnySizeChanged;
            _itemListBox = null;
        }

        if (_listScrollViewer is not null)
        {
            _listScrollViewer.SizeChanged -= OnAnySizeChanged;
            _listScrollViewer = null;
        }

        UnhookItemsCollection();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        HookItemsCollection();
    }

    private void OnAnySizeChanged(object? sender, SizeChangedEventArgs e)
    {
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => ResolveAndTrackScrollViewer(), DispatcherPriority.Background);
    }

    private void ResolveAndTrackScrollViewer()
    {
        if (_itemListBox is null)
        {
            return;
        }

        var resolved = _itemListBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (ReferenceEquals(resolved, _listScrollViewer))
        {
            return;
        }

        if (_listScrollViewer is not null)
        {
            _listScrollViewer.SizeChanged -= OnAnySizeChanged;
        }

        _listScrollViewer = resolved;
        if (_listScrollViewer is not null)
        {
            _listScrollViewer.SizeChanged += OnAnySizeChanged;
        }
    }

    private void HookItemsCollection()
    {
        UnhookItemsCollection();
        _itemsCollection = ListItem?.Items;
        if (_itemsCollection is not null)
        {
            _itemsCollection.CollectionChanged += OnItemsCollectionChanged;
        }
    }

    private void UnhookItemsCollection()
    {
        if (_itemsCollection is null)
        {
            return;
        }

        _itemsCollection.CollectionChanged -= OnItemsCollectionChanged;
        _itemsCollection = null;
    }
}
