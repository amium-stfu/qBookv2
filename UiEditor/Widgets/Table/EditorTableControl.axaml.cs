using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Amium.EditorUi.Controls;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;
using TableCellSlot = Amium.UiEditor.Models.PageItemModel.TableCellSlot;

namespace Amium.UiEditor.Widgets;

public partial class EditorTableControl : EditorTemplateWidget
{
    private Grid? _tableRoot;
    private Grid? _itemContentGrid;
    private Grid? _itemOverlayGrid;
    private INotifyCollectionChanged? _itemsCollection;
    private bool _isSelecting;
    private int _selectionAnchorRow;
    private int _selectionAnchorColumn;
    private int _selectionCurrentRow;
    private int _selectionCurrentColumn;
    private bool _hasRangeAnchor;
    private int _rangeAnchorRow;
    private int _rangeAnchorColumn;
    private Border? _selectionOverlay;
    private Cursor? _previousCursor;

    private PageItemModel? Item => DataContext as PageItemModel;

    private MainWindowViewModel? ViewModel
        => this.GetVisualRoot() is Window { DataContext: MainWindowViewModel viewModel } ? viewModel : null;

    public EditorTableControl()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _tableRoot = this.FindControl<Grid>("TableRoot");
        _itemContentGrid = this.FindControl<Grid>("ItemContentGrid");
        _itemOverlayGrid = this.FindControl<Grid>("ItemOverlayGrid");
        _selectionOverlay = this.FindControl<Border>("SelectionOverlay");
        HookItemsCollection();
        RefreshItemOverlay();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        UnhookItemsCollection();
        if (_selectionOverlay is not null)
        {
            _selectionOverlay.IsVisible = false;
        }
        _tableRoot = null;
        _itemContentGrid = null;
        _itemOverlayGrid = null;
        _selectionOverlay = null;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        HookItemsCollection();
        RefreshItemOverlay();
    }

    private void HookItemsCollection()
    {
        UnhookItemsCollection();
        _itemsCollection = Item?.Items;
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

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshItemOverlay();
    }

    private void RefreshItemOverlay()
    {
        if (_itemContentGrid is null || _itemOverlayGrid is null || Item is null)
        {
            return;
        }

        _itemContentGrid.RowDefinitions.Clear();
        _itemContentGrid.ColumnDefinitions.Clear();
        _itemOverlayGrid.RowDefinitions.Clear();
        _itemOverlayGrid.ColumnDefinitions.Clear();

        for (var r = 0; r < Item.TableRows; r++)
        {
            _itemContentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            _itemOverlayGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        }

        for (var c = 0; c < Item.TableColumns; c++)
        {
            _itemContentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _itemOverlayGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        _itemContentGrid.Children.Clear();
        _itemOverlayGrid.Children.Clear();

        foreach (var child in Item.Items)
        {
            if (!child.IsTableChildControl)
            {
                continue;
            }

            var row = Math.Max(0, child.TableCellRow - 1);
            var column = Math.Max(0, child.TableCellColumn - 1);
            var rowSpan = Math.Max(1, child.TableCellRowSpan);
            var columnSpan = Math.Max(1, child.TableCellColumnSpan);

            // Inhaltliches Widget (immer sichtbar, nicht interaktiv) –
            // gleiche spezialisierten Controls wie im PageEditor verwenden.
            Control content;
            if (child.IsButton)
            {
                content = new EditorButtonControl();
            }
            else if (child.IsItem)
            {
                content = new EditorItemControl();
            }
            else if (child.IsLogControl)
            {
                content = new EditorLogControl
                {
                    PageIsActive = true
                };
            }
            else if (child.IsChartControl)
            {
                content = new RealtimeChartControl
                {
                    PageIsActive = true
                };
            }
            else if (child.IsUdlClientControl)
            {
                content = new UdlClientControl();
            }
            else
            {
                // Fallback: nicht erwarteter Typ – ueberspringen.
                continue;
            }

            content.DataContext = child;
            content.HorizontalAlignment = HorizontalAlignment.Stretch;
            content.VerticalAlignment = VerticalAlignment.Stretch;
            content.IsHitTestVisible = false;

            Grid.SetRow(content, row);
            Grid.SetColumn(content, column);
            Grid.SetRowSpan(content, rowSpan);
            Grid.SetColumnSpan(content, columnSpan);

            _itemContentGrid.Children.Add(content);

            // Overlay fuer Edit-Controls (nur im EditMode sichtbar/aktiv)
            var overlay = new Grid
            {
                DataContext = child,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var toolbar = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4, 4, 4, 0),
                Background = Avalonia.Media.Brushes.Orange,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(2, 1, 2, 1),
                Opacity = 0.95
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2
            };

            var settingsButton = new Button
            {
                Width = 16,
                Height = 16,
                MinWidth = 16,
                Padding = new Thickness(0),
                Background = Avalonia.Media.Brushes.Transparent,
                BorderBrush = Avalonia.Media.Brushes.Transparent,
                Tag = child
            };
            settingsButton.Classes.Add("toolicon");
            ToolTip.SetTip(settingsButton, "Settings");
            settingsButton.Click += OnChildSettingsClicked;

            var settingsIcon = new ThemeSvgIcon
            {
                Width = 10,
                Height = 10,
                IconPath = "avares://Amium.Editor/EditorIcons/cog.svg"
            };
            settingsButton.Content = settingsIcon;

            var deleteButton = new Button
            {
                Width = 16,
                Height = 16,
                MinWidth = 16,
                Padding = new Thickness(0),
                Background = Avalonia.Media.Brushes.Transparent,
                BorderBrush = Avalonia.Media.Brushes.Transparent,
                Tag = child
            };
            deleteButton.Classes.Add("toolicon");
            ToolTip.SetTip(deleteButton, "Delete");
            deleteButton.Click += OnChildDeleteClicked;

            var deleteIcon = new ThemeSvgIcon
            {
                Width = 10,
                Height = 10,
                IconPath = "avares://Amium.Editor/EditorIcons/remove.svg"
            };
            deleteButton.Content = deleteIcon;

            stack.Children.Add(settingsButton);
            stack.Children.Add(deleteButton);
            toolbar.Child = stack;

            overlay.Children.Add(toolbar);

            // Overlay nur anzeigen, wenn das Item selektiert ist.
            overlay.Bind(IsVisibleProperty, new Avalonia.Data.Binding("IsSelected"));

            Grid.SetRow(overlay, row);
            Grid.SetColumn(overlay, column);
            Grid.SetRowSpan(overlay, rowSpan);
            Grid.SetColumnSpan(overlay, columnSpan);

            _itemOverlayGrid.Children.Add(overlay);
        }
    }

    private void OnAddItemClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
            // Oeffnet das Kontextmenue zur Auswahl des Control-Typs.
            if (sender is Control { ContextMenu: { } menu })
            {
                menu.Open();
                e.Handled = true;
            }
    }

    private void OnCellPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is null || Item is null || !ViewModel.IsEditMode)
        {
            return;
        }

        if (sender is not Control control || control.DataContext is not TableCellSlot slot)
        {
            return;
        }
        var point = e.GetCurrentPoint(control);

        // Klick auf eine Zelle mit Child-Item: nur das Widget ist selektierbar, nicht die Zelle.
        if (slot.ChildItem is PageItemModel childItem
            && (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed
                || point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed))
        {
            ViewModel.SelectItem(childItem);
            e.Handled = true;
            return;
        }

        if (point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
        {
            // Rechtsklick: Item fuer aktuelle Auswahl (oder nur diese Zelle) anlegen.
            ViewModel.ToggleTableCellSelection(Item, slot.Row, slot.Column, toggle: false);
            ViewModel.AddItemToSelectedTableCells(Item);
            RefreshItemOverlay();
            e.Handled = true;
            return;
        }
        var modifiers = e.KeyModifiers;

        // STRG + Linksklick: rechteckige Bereichsauswahl (Startpunkt -> Endpunkt).
        if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed
            && modifiers.HasFlag(KeyModifiers.Control))
        {
            HandleCtrlRangeClick(slot);
            e.Handled = true;
            return;
        }

        // Jede andere Aktion beendet ggf. einen laufenden Range-Anchor.
        _hasRangeAnchor = false;

        // Ohne STRG: Single-/Shift-Toggle wie bisher.
        var isToggle = modifiers.HasFlag(KeyModifiers.Shift);
        ViewModel.ToggleTableCellSelection(Item, slot.Row, slot.Column, isToggle);
        e.Handled = true;
    }

    private void OnCellPointerEnter(object? sender, PointerEventArgs e)
    {
        if (!_isSelecting || ViewModel is null || Item is null || !ViewModel.IsEditMode)
        {
            return;
        }

        if (sender is not Control { DataContext: TableCellSlot slot } control)
        {
            return;
        }

        var point = e.GetCurrentPoint(control);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _isSelecting = false;
            if (_selectionOverlay is not null)
            {
                _selectionOverlay.IsVisible = false;
            }
            control.Cursor = _previousCursor;
            return;
        }

        _selectionCurrentRow = slot.Row;
        _selectionCurrentColumn = slot.Column;
        UpdateSelectionOverlay(_selectionAnchorRow, _selectionAnchorColumn, _selectionCurrentRow, _selectionCurrentColumn);
        e.Handled = true;
    }

    private void OnCellPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            if (_isSelecting && ViewModel is not null && Item is not null && ViewModel.IsEditMode)
            {
                ViewModel.SelectTableRectangle(Item, _selectionAnchorRow, _selectionAnchorColumn, _selectionCurrentRow, _selectionCurrentColumn);
            }

            _isSelecting = false;
            e.Pointer.Capture(null);

            if (sender is Control control)
            {
                control.Cursor = _previousCursor;
            }

            if (_selectionOverlay is not null)
            {
                _selectionOverlay.IsVisible = false;
            }
        }
    }

    // STRG-Range-Selektion: erster Klick setzt Anker, zweiter Klick selektiert das Rechteck.
    private void HandleCtrlRangeClick(TableCellSlot slot)
    {
        if (ViewModel is null || Item is null || !ViewModel.IsEditMode)
        {
            return;
        }

        // Auf belegten Zellen keine Rechteckauswahl starten.
        if (slot.ChildItem is not null)
        {
            return;
        }

        if (!_hasRangeAnchor)
        {
            _rangeAnchorRow = slot.Row;
            _rangeAnchorColumn = slot.Column;
            _hasRangeAnchor = true;

            // Ankerzelle sichtbar machen.
            ViewModel.SelectTableRectangle(Item, slot.Row, slot.Column, slot.Row, slot.Column);
            return;
        }

        // Zweiter STRG-Klick: komplettes Rechteck selektieren.
        ViewModel.SelectTableRectangle(Item, _rangeAnchorRow, _rangeAnchorColumn, slot.Row, slot.Column);
        _hasRangeAnchor = false;
    }

    private void UpdateSelectionOverlay(int startRow, int startColumn, int endRow, int endColumn)
    {
        if (_selectionOverlay is null || _tableRoot is null || Item is null)
        {
            return;
        }

        var minRow = Math.Min(startRow, endRow);
        var maxRow = Math.Max(startRow, endRow);
        var minColumn = Math.Min(startColumn, endColumn);
        var maxColumn = Math.Max(startColumn, endColumn);

        var rootBounds = _tableRoot.Bounds;
        if (rootBounds.Width <= 0 || rootBounds.Height <= 0)
        {
            return;
        }

        var cellWidth = rootBounds.Width / Math.Max(1, Item.TableColumns);
        var cellHeight = rootBounds.Height / Math.Max(1, Item.TableRows);

        var x = (minColumn - 1) * cellWidth;
        var y = (minRow - 1) * cellHeight;
        var width = Math.Max(cellWidth, (maxColumn - minColumn + 1) * cellWidth);
        var height = Math.Max(cellHeight, (maxRow - minRow + 1) * cellHeight);

        _selectionOverlay.Margin = new Thickness(x, y, 0, 0);
        _selectionOverlay.Width = width;
        _selectionOverlay.Height = height;
        _selectionOverlay.IsVisible = true;
    }

    private void AddControlToTable(ControlKind kind)
    {
        if (ViewModel is null || Item is null || !ViewModel.IsEditMode)
        {
            return;
        }

        ViewModel.AddControlToSelectedTableCells(Item, kind);
        RefreshItemOverlay();
    }

    private void OnAddItemMenuClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddControlToTable(ControlKind.Item);
        e.Handled = true;
    }

    private void OnAddButtonMenuClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddControlToTable(ControlKind.Button);
        e.Handled = true;
    }

    private void OnAddChartMenuClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddControlToTable(ControlKind.ChartControl);
        e.Handled = true;
    }

    private void OnAddLogMenuClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddControlToTable(ControlKind.LogControl);
        e.Handled = true;
    }

    private void OnAddUdlClientMenuClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is null || !ViewModel.SupportsUdlClientControl)
        {
            return;
        }

        AddControlToTable(ControlKind.UdlClientControl);
        e.Handled = true;
    }

    private void OnChildSettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not Control { Tag: PageItemModel child })
        {
            return;
        }

        // Editor-Dialog fuer das Child-Item oeffnen; Position ist hier nachrangig.
        ViewModel.OpenItemEditor(child, 0, 0);
        e.Handled = true;
    }

    private void OnChildDeleteClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Item is null || sender is not Control { Tag: PageItemModel child })
        {
            return;
        }

        if (Item.Items.Contains(child))
        {
            Item.Items.Remove(child);
            Item.UpdateTableCellContentFromChildren();
            RefreshItemOverlay();
        }

        e.Handled = true;
    }
}
