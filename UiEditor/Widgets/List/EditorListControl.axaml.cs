using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Amium.EditorUi.Controls;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public partial class EditorListControl : EditorTemplateWidget
{
    private Border? _viewportBorder;
    private ListBox? _itemListBox;
    private ScrollViewer? _listScrollViewer;
    private INotifyCollectionChanged? _itemsCollection;

    private FolderItemModel? Item => DataContext as FolderItemModel;

    private MainWindowViewModel? ViewModel
        => this.GetVisualRoot() is Window { DataContext: MainWindowViewModel viewModel } ? viewModel : null;

    public EditorListControl()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    private FolderItemModel? ListItem => Item;

    private void OnChildItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: FolderItemModel childItem } || ListItem is null || ViewModel?.IsEditMode != true)
        {
            return;
        }

        ListItem.SelectedListItem = childItem;
        ViewModel.SelectItem(childItem);
        e.Handled = true;
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        HandleSettingsClicked(e);
    }

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandleInteractivePointerPressed(e);
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

public partial class EditorListWidget : EditorListControl
{
}
