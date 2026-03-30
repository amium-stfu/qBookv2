
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Amium.EditorUi;
using Amium.Items;
using Amium.Host;
using Amium.Logging;
using Amium.UiEditor.Helpers;
using Amium.UiEditor.Models;
using Amium.UiEditor.Persistence;

namespace Amium.UiEditor.ViewModels;

public class MainWindowViewModel : ObservableObject, IEditorUiHost
{
    private const string DemoTargetPath = "Demo/Item/Demo 1";
    private static readonly IReadOnlyList<string> ParameterFormatOptions = ["Text", "Numeric", "Hex", "bool", "b4", "b8", "b16"];
    private static readonly IReadOnlyList<string> AlignmentOptions = ["Left", "Center", "Right"];

    private enum EditorDialogMode
    {
        None,
        AddCanvas,
        AddList,
        Edit
    }

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ObservableCollection<PageItemModel> _selectedItems = [];
    private readonly bool _supportsUdlClientControl;
    private bool _isEditMode;
    private bool _showGrid = true;
    private bool _snapToEdges = true;
    private bool _isDarkTheme;
    private bool _isHeaderCollapsed;
    private int _viewLimit;
    private int _gridSize = 20;
    private string _statusText;
    private string _dataRegistrySummary;
    private string _editorDialogChoiceSummary;
    private PageItemModel? _selectedItem;
    private PageModel _selectedPage = null!;
    private double _canvasWidth;
    private double _canvasHeight;
    private double _workspaceWidth;
    private double _workspaceHeight;
    private PageItemModel? _listPopupTarget;
    private EditorDialogMode _editorDialogMode;
    private PageItemModel? _editorDialogItem;
    private PageItemModel? _editorDialogParentItem;
    private string _editorDialogTitle = string.Empty;
    private string _editorDialogError = string.Empty;
    private bool _isEditorDialogOpen;
    private bool _isRefreshingEditorDialogFields;
    private double _editorDialogX;
    private double _editorDialogY;
    private int _dataRegistryRefreshQueued;
    private PageItemModel? _activeValueInputItem;
    private bool _isValueInputOpen;
    private UserLevel _currentUser = UserLevel.Default;
    private Dock _tabStripPlacement = Dock.Right;
    protected bool AutoSaveOnEditModeExit { get; set; } = true;

    public MainWindowViewModel(bool supportsUdlClientControl = false)
    {
        _supportsUdlClientControl = supportsUdlClientControl;
        Pages = [];
        GridLines = [];
        SelectionState = new SelectionState();
        EditorDialogSections = [];
        EditorDialogActionFields = [];
        Messages = [];
        LayoutFilePath = Path.Combine(AppContext.BaseDirectory, "layout.json");
        SelectPageCommand = new RelayCommand<PageModel>(SelectPage);
        SaveLayoutCommand = new RelayCommand(SaveLayout);
        LoadLayoutCommand = new RelayCommand(LoadLayout);
        AlignLeftCommand = new RelayCommand(AlignLeft);
        AlignRightCommand = new RelayCommand(AlignRight);
        AlignTopCommand = new RelayCommand(AlignTop);
        AlignBottomCommand = new RelayCommand(AlignBottom);
        AlignHorizontalCenterCommand = new RelayCommand(AlignHorizontalCenter);
        AlignVerticalCenterCommand = new RelayCommand(AlignVerticalCenter);
        MatchWidthCommand = new RelayCommand(MatchWidth);
        MatchHeightCommand = new RelayCommand(MatchHeight);
        ToggleEditModeCommand = new RelayCommand(ToggleEditMode);
        ToggleHeaderCollapsedCommand = new RelayCommand(ToggleHeaderCollapsed);
        SetTabStripPlacementCommand = new RelayCommand<string>(SetTabStripPlacement);
        _dataRegistrySummary = "HostRegistry: 0 Items";
        _editorDialogChoiceSummary = "Dialog Choices: closed";
        _statusText = $"Layout file: {LayoutFilePath}";
        _currentUser = UserLevel.Default;
        _viewLimit = _currentUser.ViewLimit;
        HostRegistries.Data.RegistryChanged += OnDataRegistryStructureChanged;

        SetPages(CreateDefaultPages());
        RefreshDataRegistryDiagnostics();
    }

    public bool SupportsUdlClientControl => _supportsUdlClientControl;

    public ObservableCollection<PageModel> Pages { get; }

    public ObservableCollection<EditorGridLine> GridLines { get; }

    public ObservableCollection<EditorDialogSection> EditorDialogSections { get; }

    public ObservableCollection<EditorDialogField> EditorDialogActionFields { get; }

    public ObservableCollection<HostMessageEntry> Messages { get; }

    public bool HasEditorDialogActionFields => EditorDialogActionFields.Count > 0;

    public bool ShowEditorDialogActionPlaceholder => !HasEditorDialogActionFields;

    public PageModel SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (value is null || ReferenceEquals(value, _selectedPage))
            {
                return;
            }

            var previousPage = _selectedPage;
            if (previousPage is not null)
            {
                previousPage.IsSelected = false;
            }

            if (SetProperty(ref _selectedPage, value))
            {
                _selectedPage.IsSelected = true;
                ClearItemSelection();
                CancelSelection();
                CancelEditorDialog();
                OnPropertyChanged(nameof(FooterText));
            }
        }
    }

    public PageItemModel? SelectedItem
    {
        get => _selectedItem;
        private set => SetProperty(ref _selectedItem, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                // Beim Umschalten des EditMode immer den Body-Interaktionsmodus
                // zuruecksetzen, damit man sich nicht "aussperrt".
                if (!value)
                {
                    IsShiftInteractionMode = false;
                }

                if (!value)
                {
                    CancelSelection();
                    ClearItemSelection();
                    CancelEditorDialog();
                    CancelValueInput();
                    if (AutoSaveOnEditModeExit)
                    {
                        SaveLayout();
                    }
                }

                OnPropertyChanged(nameof(EditModeText));
                OnPropertyChanged(nameof(FooterText));
                OnPropertyChanged(nameof(ShowGridOptions));
                OnPropertyChanged(nameof(ShowAlignmentPanel));
            }
        }
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            if (SetProperty(ref _showGrid, value))
            {
                RefreshGridLines();
                StatusText = value ? "Raster aktiviert" : "Raster deaktiviert";
            }
        }
    }

    public bool SnapToEdges
    {
        get => _snapToEdges;
        set
        {
            if (SetProperty(ref _snapToEdges, value))
            {
                StatusText = value ? "Snap to edge aktiviert" : "Snap to edge deaktiviert";
            }
        }
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
            {
                UpdateApplicationTheme(value);
                ApplyThemeToAllItems();
                OnPropertyChanged(nameof(ThemeModeText));
                OnPropertyChanged(nameof(WindowBackground));
                OnPropertyChanged(nameof(CardBackground));
                OnPropertyChanged(nameof(DialogBackground));
                OnPropertyChanged(nameof(CardBorderBrush));
                OnPropertyChanged(nameof(PrimaryTextBrush));
                OnPropertyChanged(nameof(SecondaryTextBrush));
                OnPropertyChanged(nameof(CanvasBackground));
                OnPropertyChanged(nameof(CanvasBorderBrush));
                OnPropertyChanged(nameof(GridLineBrush));
                OnPropertyChanged(nameof(EditPanelBackground));
                OnPropertyChanged(nameof(EditPanelBorderBrush));
                OnPropertyChanged(nameof(EditPanelButtonBackground));
                OnPropertyChanged(nameof(EditPanelButtonBorderBrush));
                OnPropertyChanged(nameof(EditPanelInputBackground));
                OnPropertyChanged(nameof(EditPanelInputForeground));
                OnPropertyChanged(nameof(ParameterEditBackgrundColor));
                OnPropertyChanged(nameof(ParameterEditForeColor));
                OnPropertyChanged(nameof(ParameterHoverColor));
                OnPropertyChanged(nameof(ButtonBackColor));
                OnPropertyChanged(nameof(ButtonHoverColor));
                OnPropertyChanged(nameof(ButtonForeColor));
                OnPropertyChanged(nameof(TabSelectNumerBackColor));
                OnPropertyChanged(nameof(TabSelectBackColor));
                OnPropertyChanged(nameof(TabSelectForeColor));
                OnPropertyChanged(nameof(TabNumerBackColor));
                OnPropertyChanged(nameof(TabBackColor));
                OnPropertyChanged(nameof(TabForeColor));
                OnPropertyChanged(nameof(EditorDialogSectionHeaderBackground));
                OnPropertyChanged(nameof(EditorDialogSectionHeaderForeground));
                OnPropertyChanged(nameof(EditorDialogSectionHeaderBorderBrush));
                OnPropertyChanged(nameof(EditorDialogSectionContentBackground));
                OnPropertyChanged(nameof(HeaderBadgeBackground));
                OnPropertyChanged(nameof(HeaderBadgeForeground));
                OnPropertyChanged(nameof(CurrentUserColor));
                OnPropertyChanged(nameof(FooterPanelBackground));
                OnPropertyChanged(nameof(FooterPanelForeground));
            }
        }
    }

    private static void UpdateApplicationTheme(bool isDark)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        app.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    public int GridSize
    {
        get => _gridSize;
        set
        {
            var normalized = Math.Clamp(value, 8, 200);
            if (SetProperty(ref _gridSize, normalized))
            {
                RefreshGridLines();
                StatusText = $"Rasterweite: {normalized}";
            }
        }
    }

    public bool ShowGridOptions => IsEditMode;
    private bool _isShiftInteractionMode;
    public bool IsShiftInteractionMode
    {
        get => _isShiftInteractionMode;
        set => SetProperty(ref _isShiftInteractionMode, value);
    }
    private ThemePalette CurrentTheme => IsDarkTheme ? ThemePalette.Dark : ThemePalette.Light;
    public Dock TabStripPlacement
    {
        get => _tabStripPlacement;
        set
        {
            if (SetProperty(ref _tabStripPlacement, value))
            {
                OnPropertyChanged(nameof(TabHeaderHorizontalAlignment));
                OnPropertyChanged(nameof(IsTopTabStripPlacement));
                OnPropertyChanged(nameof(IsRightTabStripPlacement));
            }
        }
    }
    public HorizontalAlignment TabHeaderHorizontalAlignment => TabStripPlacement switch
    {
        Dock.Right => HorizontalAlignment.Left,
        Dock.Left => HorizontalAlignment.Right,
        _ => HorizontalAlignment.Stretch
    };

    public bool IsTopTabStripPlacement => TabStripPlacement == Dock.Top;

    public bool IsRightTabStripPlacement => TabStripPlacement == Dock.Right;

    public bool IsHeaderCollapsed
    {
        get => _isHeaderCollapsed;
        set => SetProperty(ref _isHeaderCollapsed, value);
    }

    public string ThemeModeText => IsDarkTheme ? "Dark Theme" : "Light Theme";
    public string WindowBackground => CurrentTheme.WindowBackground;
    public string DialogBackground => CurrentTheme.DialogBackground;
    public string CardBackground => CurrentTheme.CardBackground;
    public string CardBorderBrush => CurrentTheme.CardBorderBrush;
    public string PrimaryTextBrush => CurrentTheme.PrimaryTextBrush;
    public string SecondaryTextBrush => CurrentTheme.SecondaryTextBrush;
    public string CanvasBackground => CurrentTheme.CanvasBackground;
    public string CanvasBorderBrush => CurrentTheme.CanvasBorderBrush;
    public string GridLineBrush => CurrentTheme.GridLineBrush;
    public string EditPanelBackground => CurrentTheme.EditPanelBackground;
    public string EditPanelBorderBrush => CurrentTheme.EditPanelBorderBrush;
    public string EditPanelButtonBackground => CurrentTheme.EditPanelButtonBackground;
    public string EditPanelButtonBorderBrush => CurrentTheme.EditPanelButtonBorderBrush;
    public string EditPanelInputBackground => CurrentTheme.EditPanelInputBackground;
    public string EditPanelInputForeground => CurrentTheme.EditPanelInputForeground;
    public string ParameterEditBackgrundColor => CurrentTheme.ParameterEditBackgrundColor;
    public string ParameterEditForeColor => CurrentTheme.ParameterEditForeColor;
    public string ParameterHoverColor => CurrentTheme.ParameterHoverColor;
    public string ButtonBackColor => CurrentTheme.ButtonBackColor;
    public string ButtonHoverColor => CurrentTheme.ButtonHoverColor;
    public string ButtonForeColor => CurrentTheme.ButtonForeColor;
    public string TabSelectNumerBackColor => CurrentTheme.TabSelectNumerBackColor;
    public string TabSelectBackColor => CurrentTheme.TabSelectBackColor;
    public string TabSelectForeColor => CurrentTheme.TabSelectForeColor;
    public string TabNumerBackColor => CurrentTheme.TabNumerBackColor;
    public string TabBackColor => CurrentTheme.TabBackColor;
    public string TabForeColor => CurrentTheme.TabForeColor;
    public string EditorDialogSectionHeaderBackground => CurrentTheme.EditorDialogSectionHeaderBackground;
    public string EditorDialogSectionHeaderForeground => CurrentTheme.EditorDialogSectionHeaderForeground;
    public string EditorDialogSectionHeaderBorderBrush => CurrentTheme.EditorDialogSectionHeaderBorderBrush;
    public string EditorDialogSectionContentBackground => CurrentTheme.EditorDialogSectionContentBackground;
    public string HeaderBadgeBackground => CurrentTheme.HeaderBadgeBackground;
    public string HeaderBadgeForeground => CurrentTheme.HeaderBadgeForeground;
    public string FooterPanelBackground => string.IsNullOrWhiteSpace(_currentUser.Color)
        ? CardBackground
        : _currentUser.Color;
    public string FooterPanelForeground => string.IsNullOrWhiteSpace(_currentUser.Color)
        ? SecondaryTextBrush
        : "Black";
    public int ViewLimit
    {
        get => _viewLimit;
        private set => SetProperty(ref _viewLimit, value);
    }

    public string CurrentUserCaption => _currentUser.Caption;

    public string CurrentUserColor => string.IsNullOrWhiteSpace(_currentUser.Color)
        ? PrimaryTextBrush
        : _currentUser.Color;

    public bool IsEditModeToggleVisible => _currentUser.Id == 2 || _currentUser.Id == 3;

    public bool IsLogoutAvailable => _currentUser.Id != UserLevel.Default.Id;

    public bool IsChangePasswordAvailable => _currentUser.Id != UserLevel.Default.Id && _currentUser.Id != 3;

    public bool IsResetPasswordsAvailable => _currentUser.Id == 3;
    public bool HasMultiSelection => _selectedItems.Count > 1;
    public bool ShowAlignmentPanel => IsEditMode && HasMultiSelection;
    public int SelectedItemsCount => _selectedItems.Count;
    public string EditModeText => IsEditMode ? "Edit mode aktiv" : "View mode";
    public string FooterText => $"{SelectedPage.Name} aktiv | {SelectedPage.Items.Count} Widgets | {SelectedItemsCount} ausgewaehlt | {(IsEditMode ? "Edit" : "Navigation")}";

    public string StatusText
    {
        get => _statusText;
        protected set => SetProperty(ref _statusText, value);
    }

    public string DataRegistrySummary
    {
        get => _dataRegistrySummary;
        private set => SetProperty(ref _dataRegistrySummary, value);
    }

    public string EditorDialogChoiceSummary
    {
        get => _editorDialogChoiceSummary;
        private set => SetProperty(ref _editorDialogChoiceSummary, value);
    }

    public string LayoutFilePath { get; }
    public SelectionState SelectionState { get; }
    public RelayCommand<PageModel> SelectPageCommand { get; }
    public RelayCommand SaveLayoutCommand { get; }
    public RelayCommand LoadLayoutCommand { get; }
    public RelayCommand AlignLeftCommand { get; }
    public RelayCommand AlignRightCommand { get; }
    public RelayCommand AlignTopCommand { get; }
    public RelayCommand AlignBottomCommand { get; }
    public RelayCommand AlignHorizontalCenterCommand { get; }
    public RelayCommand AlignVerticalCenterCommand { get; }
    public RelayCommand MatchWidthCommand { get; }
    public RelayCommand MatchHeightCommand { get; }
    public RelayCommand ToggleEditModeCommand { get; }
    public RelayCommand ToggleHeaderCollapsedCommand { get; }
    public RelayCommand<string> SetTabStripPlacementCommand { get; }

    // Logische Groesse des Arbeitsbereichs fuer ScrollViewer
    public double WorkspaceWidth
    {
        get => _workspaceWidth;
        private set => SetProperty(ref _workspaceWidth, value);
    }

    public double WorkspaceHeight
    {
        get => _workspaceHeight;
        private set => SetProperty(ref _workspaceHeight, value);
    }

    public bool IsEditorDialogOpen
    {
        get => _isEditorDialogOpen;
        private set => SetProperty(ref _isEditorDialogOpen, value);
    }

    public double EditorDialogX
    {
        get => _editorDialogX;
        private set => SetProperty(ref _editorDialogX, value);
    }

    public double EditorDialogY
    {
        get => _editorDialogY;
        private set => SetProperty(ref _editorDialogY, value);
    }

    public bool IsValueInputOpen
    {
        get => _isValueInputOpen;
        private set => SetProperty(ref _isValueInputOpen, value);
    }

    public PageItemModel? ActiveValueInputItem
    {
        get => _activeValueInputItem;
        private set => SetProperty(ref _activeValueInputItem, value);
    }

    public string EditorDialogTitle
    {
        get => _editorDialogTitle;
        private set => SetProperty(ref _editorDialogTitle, value);
    }

    public string EditorDialogError
    {
        get => _editorDialogError;
        private set => SetProperty(ref _editorDialogError, value);
    }

    public void SelectPage(PageModel? page)
    {
        if (page is null)
        {
            return;
        }

        SelectedPage = page;
    }

    public void SelectItem(PageItemModel? item)
    {
        if (item is null)
        {
            ClearItemSelection();
            return;
        }

        SetSelectedItems([item], item);
    }

    public void SetMasterItem(PageItemModel item)
    {
        if (!_selectedItems.Contains(item))
        {
            SelectItem(item);
            return;
        }

        UpdateSelectionFlags(item);
    }

    public void ToggleItemSelection(PageItemModel item)
    {
        var items = _selectedItems.ToList();
        PageItemModel? master = SelectedItem;

        if (!items.Remove(item))
        {
            items.Add(item);
            master = item;
        }
        else if (ReferenceEquals(master, item))
        {
            master = items.LastOrDefault();
        }

        SetSelectedItems(items, master);
    }

    public void AddItemsToSelection(IReadOnlyList<PageItemModel> items)
    {
        var merged = _selectedItems.Concat(items).Distinct().ToList();
        SetSelectedItems(merged, items.LastOrDefault() ?? merged.LastOrDefault());
    }

    public bool IsItemSelected(PageItemModel item) => _selectedItems.Contains(item);

    public IReadOnlyList<PageItemModel> GetSelectedItems() => _selectedItems.ToList();

    public void ClearItemSelection() => SetSelectedItems([], null);

    public void StartSelection(double x, double y)
    {
        SelectionState.StartX = x;
        SelectionState.StartY = y;
        SelectionState.X = x;
        SelectionState.Y = y;
        SelectionState.Width = 0;
        SelectionState.Height = 0;
        SelectionState.IsSelecting = true;
        SelectionState.ShowPicker = false;
        CancelEditorDialog();
    }

    public void UpdateSelection(double x, double y)
    {
        var anchorX = SelectionState.StartX;
        var anchorY = SelectionState.StartY;
        SelectionState.X = Math.Min(anchorX, x);
        SelectionState.Y = Math.Min(anchorY, y);
        SelectionState.Width = Math.Abs(x - anchorX);
        SelectionState.Height = Math.Abs(y - anchorY);
    }

    public void FinishSelection(double x, double y, bool addToSelection)
    {
        UpdateSelection(x, y);

        if (SelectionState.Width < 24 || SelectionState.Height < 24)
        {
            CancelSelection();
            if (!addToSelection)
            {
                ClearItemSelection();
            }
            return;
        }

        SelectionState.IsSelecting = false;

        var selectedItems = SelectedPage.Items
            .Where(item => IntersectsSelection(item, SelectionState.X, SelectionState.Y, SelectionState.Width, SelectionState.Height))
            .ToList();

        if (selectedItems.Count > 0)
        {
            if (addToSelection)
            {
                AddItemsToSelection(selectedItems);
            }
            else
            {
                SetSelectedItems(selectedItems, selectedItems.Last());
            }

            SelectionState.ShowPicker = false;
            StatusText = _selectedItems.Count == 1 ? $"{_selectedItems[0].Title} ausgewaehlt" : $"{_selectedItems.Count} Widgets ausgewaehlt";
            return;
        }

        if (addToSelection)
        {
            CancelSelection();
            return;
        }

        ClearItemSelection();
        SelectionState.PopupX = Math.Max(8, SelectionState.X + 12);
        SelectionState.PopupY = Math.Max(8, SelectionState.Y + 12);
        SelectionState.ShowPicker = true;
    }

    public void BeginSelectionAdd(ControlKind kind)
    {
        if (!SelectionState.ShowPicker || kind == ControlKind.Signal)
        {
            return;
        }

        var draft = CreateItem(kind, SelectionState.X, SelectionState.Y, SelectionState.Width, SelectionState.Height);
        draft.Name = GetSuggestedControlName(kind, SelectedPage, null, null);
        draft.Id = Guid.NewGuid().ToString("N");
        draft.SetHierarchy(SelectedPage.Name, null);

        OpenEditorDialog(EditorDialogMode.AddCanvas, draft, null, SelectionState.PopupX + 16, SelectionState.PopupY + 16, $"Create {kind}");
        SelectionState.ShowPicker = false;
    }

    public void OpenListPopup(PageItemModel listControl, double x, double y)
    {
        _listPopupTarget = listControl;
        SelectionState.ListPopupX = x;
        SelectionState.ListPopupY = y;
        SelectionState.ShowListPicker = true;
    }

    public void BeginListAdd(ControlKind kind)
    {
        if (_listPopupTarget is null || !_listPopupTarget.IsListControl || kind == ControlKind.ListControl || kind == ControlKind.Signal)
        {
            return;
        }

        var draft = CreateItem(kind, 0, 0, _listPopupTarget.ChildContentWidth, _listPopupTarget.ListItemHeight);
        draft.Name = GetSuggestedControlName(kind, SelectedPage, _listPopupTarget, null);
        draft.Id = Guid.NewGuid().ToString("N");
        draft.SetHierarchy(SelectedPage.Name, _listPopupTarget);

        OpenEditorDialog(EditorDialogMode.AddList, draft, _listPopupTarget, SelectionState.ListPopupX + 16, SelectionState.ListPopupY + 16, $"Create {kind}");
        CancelListPopup();
    }

    public void ToggleTableCellSelection(PageItemModel table, int row, int column, bool toggle)
    {
        if (table is null || !table.IsTableControl)
        {
            return;
        }

        var target = table.TableCellSlots.FirstOrDefault(c => c.Row == row && c.Column == column);
        if (target is null)
        {
            return;
        }

        // Zellen mit bereits platziertem Widget sind nicht mehr selektierbar.
        if (target.ChildItem is not null)
        {
            return;
        }

        // Ohne Toggle: nur diese Zelle auswaehlen.
        if (!toggle)
        {
            foreach (var cell in table.TableCellSlots)
            {
                cell.IsSelected = ReferenceEquals(cell, target);
            }
            UpdateLastSelectedTableCell(table, row, column);
            return;
        }

        // Mit Toggle (Strg/Shift): nur zusammenhaengende rechteckige Bereiche erlauben.
        var currentlySelected = table.TableCellSlots.Where(c => c.IsSelected).ToList();

        // Wenn noch nichts ausgewaehlt ist, wie Single-Select verhalten.
        if (currentlySelected.Count == 0)
        {
            target.IsSelected = true;
            UpdateLastSelectedTableCell(table, row, column);
            return;
        }

        // Wenn die Zielzelle bereits selektiert ist: Deselektieren nur, wenn dadurch kein Loch entsteht.
        if (target.IsSelected)
        {
            target.IsSelected = false;
            if (!IsContiguousRectangle(table))
            {
                // Rueckgaengig machen, Selektion unveraendert lassen.
                target.IsSelected = true;
            }
            UpdateLastSelectedTableCell(table, row, column);
            return;
        }

        // Neue Zelle hinzunehmen und pruefen, ob Gesamtmenge noch ein Rechteck ohne Luecken ist.
        target.IsSelected = true;
        if (!IsContiguousRectangle(table))
        {
            // Ungueltige Kombination (nicht zusammenhaengend oder mit Luecken), wieder entfernen.
            target.IsSelected = false;
        }

        UpdateLastSelectedTableCell(table, row, column);
    }

    public void AddItemToSelectedTableCells(PageItemModel table)
        => AddControlToSelectedTableCells(table, ControlKind.Item);

    public void AddControlToSelectedTableCells(PageItemModel table, ControlKind kind)
    {
        if (table is null || !table.IsTableControl)
        {
            return;
        }

        // Keine verschachtelten Container-Controls im Table zulassen.
        if (kind == ControlKind.TableControl || kind == ControlKind.ListControl)
        {
            return;
        }

        var selected = table.TableCellSlots.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var minRow = selected.Min(c => c.Row);
        var maxRow = selected.Max(c => c.Row);
        var minColumn = selected.Min(c => c.Column);
        var maxColumn = selected.Max(c => c.Column);

        var rowSpan = Math.Max(1, maxRow - minRow + 1);
        var columnSpan = Math.Max(1, maxColumn - minColumn + 1);

        var baseCellHeight = table.Height / Math.Max(1, table.TableRows);
        var draftHeight = Math.Max(baseCellHeight * rowSpan, table.ListItemHeight);
        var draft = CreateItem(kind, 0, 0, table.ChildContentWidth, draftHeight);
        draft.Name = GetSuggestedControlName(kind, SelectedPage, table, null);
        draft.Id = Guid.NewGuid().ToString("N");
        draft.TableCellRow = minRow;
        draft.TableCellColumn = minColumn;
        draft.TableCellRowSpan = rowSpan;
        draft.TableCellColumnSpan = columnSpan;
        draft.SetHierarchy(SelectedPage.Name, table);

        table.Items.Add(draft);
        table.UpdateTableCellContentFromChildren();

        // Neues Control im Editor sichtbar machen: selektieren und Status aktualisieren.
        SelectItem(draft);
        StatusText = $"Control '{draft.Name}' ({kind}) in Table '{table.Name}' hinzugefuegt ({minRow},{minColumn}..{maxRow},{maxColumn})";

        // Theme-Regeln auch fuer neu hinzugefuegte Table-Widgets sofort anwenden.
        draft.ApplyTheme(IsDarkTheme);

        // Nach dem Hinzufuegen die bisherige Zellselektion vollstaendig zuruecksetzen.
        foreach (var cell in table.TableCellSlots)
        {
            cell.IsSelected = false;
            cell.IsLastSelected = false;
        }
    }

    public void SelectTableRectangle(PageItemModel table, int startRow, int startColumn, int endRow, int endColumn)
    {
        if (table is null || !table.IsTableControl)
        {
            return;
        }

        var minRow = Math.Min(startRow, endRow);
        var maxRow = Math.Max(startRow, endRow);
        var minColumn = Math.Min(startColumn, endColumn);
        var maxColumn = Math.Max(startColumn, endColumn);

        // Keine Auswahl-Rechtecke zulassen, die belegte Zellen enthalten.
        for (var row = minRow; row <= maxRow; row++)
        {
            for (var col = minColumn; col <= maxColumn; col++)
            {
                var slot = table.TableCellSlots.FirstOrDefault(c => c.Row == row && c.Column == col);
                if (slot is null || slot.ChildItem is not null)
                {
                    return;
                }
            }
        }

        foreach (var cell in table.TableCellSlots)
        {
            cell.IsSelected = cell.Row >= minRow && cell.Row <= maxRow
                               && cell.Column >= minColumn && cell.Column <= maxColumn;
        }

        UpdateLastSelectedTableCell(table, endRow, endColumn);
    }

    private static bool IsContiguousRectangle(PageItemModel table)
    {
        var selected = table.TableCellSlots.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return true;
        }

        var minRow = selected.Min(c => c.Row);
        var maxRow = selected.Max(c => c.Row);
        var minColumn = selected.Min(c => c.Column);
        var maxColumn = selected.Max(c => c.Column);

        // Alle Zellen im Rechteck [minRow..maxRow] x [minColumn..maxColumn] muessen selektiert sein.
        for (var row = minRow; row <= maxRow; row++)
        {
            for (var col = minColumn; col <= maxColumn; col++)
            {
                var cell = table.TableCellSlots.FirstOrDefault(c => c.Row == row && c.Column == col);
                if (cell is null || !cell.IsSelected)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void UpdateLastSelectedTableCell(PageItemModel table, int row, int column)
    {
        // Wenn die angeklickte Zelle nicht selektiert ist, suche eine beliebige andere selektierte Zelle.
        var target = table.TableCellSlots.FirstOrDefault(c => c.Row == row && c.Column == column && c.IsSelected)
                     ?? table.TableCellSlots.FirstOrDefault(c => c.IsSelected);

        foreach (var cell in table.TableCellSlots)
        {
            cell.IsLastSelected = ReferenceEquals(cell, target);
        }
    }

    public void OpenItemEditor(PageItemModel item, double x, double y)
    {
        if (IsEditorDialogOpen
            && _editorDialogMode == EditorDialogMode.Edit
            && ReferenceEquals(_editorDialogItem, item))
        {
            return;
        }

        OpenEditorDialog(EditorDialogMode.Edit, item, item.ParentItem, x, y, $"Edit {item.Name}");
    }

    public void OpenValueInput(PageItemModel item)
    {
        if (item is null || !item.CanOpenValueEditor)
        {
            return;
        }

        ActiveValueInputItem = item;
        IsValueInputOpen = true;
        StatusText = $"Input active: {item.Title}";
    }

    public PageItemModel? ResolveValueInputTarget(string? targetPath, PageItemModel sourceItem)
    {
        if (sourceItem is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(targetPath) || string.Equals(targetPath, "this", StringComparison.OrdinalIgnoreCase))
        {
            return sourceItem;
        }

        if (TryFindValueInputItem(targetPath, out var existingItem) && existingItem is not null)
        {
            return existingItem;
        }

        if (!TryResolveDataTargetItem(targetPath, out var targetItem) || targetItem is null)
        {
            return null;
        }

        var targetText = targetItem.Params.Has("Text")
            ? targetItem.Params["Text"].Value?.ToString() ?? string.Empty
            : string.Empty;

        var proxy = new PageItemModel
        {
            Kind = ControlKind.Item,
            Name = targetItem.Name ?? "InteractionTarget",
            Title = !string.IsNullOrWhiteSpace(targetText) ? targetText : targetItem.Name ?? string.Empty,
            Unit = targetItem.Params.Has("Unit") ? targetItem.Params["Unit"].Value?.ToString() ?? string.Empty : string.Empty,
            TargetParameterFormat = targetItem.Params.Has("Format")
                ? targetItem.Params["Format"].Value?.ToString() ?? string.Empty
                : sourceItem.TargetParameterFormat,
            ShowFooter = true,
            ShowCaption = true,
            ShowBodyCaption = true
        };

        proxy.ApplyTargetSelection(targetItem.Path ?? targetPath);
        return proxy;
    }

    public void OpenValueInputForTargetPath(string? targetPath, PageItemModel sourceItem)
    {
        var target = ResolveValueInputTarget(targetPath, sourceItem);
        if (target is null)
        {
            return;
        }

        OpenValueInput(target);
    }

    public void CancelValueInput()
    {
        ActiveValueInputItem = null;
        IsValueInputOpen = false;
    }

    public bool TryChangeCurrentUserPassword(string oldPassword, string newPassword, string repeatPassword, out string error)
    {
        if (string.IsNullOrEmpty(newPassword))
        {
            error = "New password must not be empty.";
            return false;
        }

        if (!string.Equals(newPassword, repeatPassword, StringComparison.Ordinal))
        {
            error = "New passwords do not match.";
            return false;
        }

        if (!UserLevel.TryChangePassword(_currentUser, oldPassword ?? string.Empty, newPassword, out error))
        {
            return false;
        }

        StatusText = "Password changed.";
        return true;
    }

    public void ResetAllUserPasswords()
    {
        UserLevel.ResetPasswordsToDefaults();
        StatusText = "Passwords reset to defaults.";
    }

    public bool TrySetUserByPassword(string? password)
    {
        var user = UserLevel.GetByPasswordOrDefault(password);

        _currentUser = user;
        ViewLimit = user.ViewLimit;
        OnPropertyChanged(nameof(CurrentUserCaption));
        OnPropertyChanged(nameof(CurrentUserColor));
        OnPropertyChanged(nameof(FooterPanelBackground));
        OnPropertyChanged(nameof(FooterPanelForeground));
        OnPropertyChanged(nameof(IsLogoutAvailable));
        OnPropertyChanged(nameof(IsChangePasswordAvailable));
        OnPropertyChanged(nameof(IsResetPasswordsAvailable));
        OnPropertyChanged(nameof(IsEditModeToggleVisible));
        StatusText = $"User level: {user.Caption}";
        return true;
    }

    private bool TryFindValueInputItem(string targetPath, out PageItemModel? match)
    {
        match = null;
        var resolvedTargetPath = targetPath;
        if (TryResolveDataTargetItem(targetPath, out var resolvedItem) && !string.IsNullOrWhiteSpace(resolvedItem?.Path))
        {
            resolvedTargetPath = resolvedItem.Path!;
        }

        match = Pages
            .SelectMany(page => EnumeratePageItems(page.Items))
            .FirstOrDefault(item => item.CanOpenValueEditor
                && !string.IsNullOrWhiteSpace(item.TargetPath)
                && string.Equals(item.TargetPath, resolvedTargetPath, StringComparison.Ordinal));

        return match is not null;
    }

    private static bool TryResolveDataTargetItem(string targetPath, out Item? item)
    {
        return TryResolveDataItem(targetPath, out item);
    }

    public void CommitEditorDialog()
        => CommitEditorDialog(closeAfterSave: true);

    public void ApplyEditorDialog()
        => CommitEditorDialog(closeAfterSave: false);

    private void CommitEditorDialog(bool closeAfterSave)
    {
        if (_editorDialogItem is null)
        {
            return;
        }

        var page = _editorDialogMode == EditorDialogMode.Edit
            ? FindOwningPage(_editorDialogItem) ?? SelectedPage
            : SelectedPage;

        if (!TryApplyEditorDialogValues(_editorDialogItem, page, _editorDialogMode == EditorDialogMode.Edit ? _editorDialogItem : null, out var error))
        {
            EditorDialogError = error;
            StatusText = error;
            return;
        }

        _editorDialogItem.ApplyTheme(IsDarkTheme);

        switch (_editorDialogMode)
        {
            case EditorDialogMode.AddCanvas:
                _editorDialogItem.X = SnapCoordinate(_editorDialogItem.X, _editorDialogItem.Width, _canvasWidth);
                _editorDialogItem.Y = SnapCoordinate(_editorDialogItem.Y, _editorDialogItem.Height, _canvasHeight);
                _editorDialogItem.Width = SnapLength(_editorDialogItem.Width, _editorDialogItem.MinWidth, MaxAvailableWidth(_editorDialogItem.X));
                _editorDialogItem.Height = SnapLength(_editorDialogItem.Height, _editorDialogItem.MinHeight, MaxAvailableHeight(_editorDialogItem.Y));
                SelectedPage.Items.Add(_editorDialogItem);
                SelectItem(_editorDialogItem);
                StatusText = $"{_editorDialogItem.Kind} '{_editorDialogItem.Name}' added to {SelectedPage.Name}";
                CancelSelection();
                break;
            case EditorDialogMode.AddList:
                if (_editorDialogParentItem is not null)
                {
                    NormalizeListChild(_editorDialogParentItem, _editorDialogItem);
                    _editorDialogItem.SetHierarchy(SelectedPage.Name, _editorDialogParentItem);
                    _editorDialogParentItem.Items.Add(_editorDialogItem);
                    _editorDialogParentItem.ApplyListHeightRules();
                    StatusText = $"{_editorDialogItem.Kind} '{_editorDialogItem.Name}' added to {_editorDialogParentItem.Name}";
                }
                break;
            case EditorDialogMode.Edit:
                StatusText = $"Control saved: {_editorDialogItem.Path}";
                break;
        }

        if (!closeAfterSave)
        {
            if (_editorDialogMode is EditorDialogMode.AddCanvas or EditorDialogMode.AddList)
            {
                _editorDialogMode = EditorDialogMode.Edit;
                EditorDialogTitle = $"Edit {_editorDialogItem.Name}";
            }

            EditorDialogError = string.Empty;
            return;
        }

        ResetEditorDialog();
    }

    public void CancelEditorDialog()
    {
        ResetEditorDialog();
        CancelValueInput();
    }

    public bool MoveItemIntoList(PageItemModel draggedItem, PageItemModel listControl)
    {
        if (!listControl.IsListControl || draggedItem.IsListControl)
        {
            return false;
        }

        if (!SelectedPage.Items.Remove(draggedItem))
        {
            return false;
        }

        NormalizeListChild(listControl, draggedItem);
        draggedItem.SetHierarchy(SelectedPage.Name, listControl);
        draggedItem.ApplyTheme(IsDarkTheme);
        listControl.Items.Add(draggedItem);
        listControl.ApplyListHeightRules();
        SelectItem(listControl);
        StatusText = $"{draggedItem.Title} in {listControl.Title} verschoben";
        return true;
    }

    public bool DeleteItem(PageItemModel item)
    {
        if (item is null)
        {
            return false;
        }

        var ownerList = item.ParentListControl;
        var removed = ownerList?.IsListControl == true
            ? ownerList.Items.Remove(item)
            : SelectedPage.Items.Remove(item);

        if (!removed)
        {
            return false;
        }

        if (ownerList?.IsListControl == true)
        {
            ownerList.ApplyListHeightRules();
            SelectItem(ownerList);
        }
        else
        {
            ClearItemSelection();
        }

        CancelEditorDialog();
        CancelValueInput();
        StatusText = $"Control gelöscht: {item.Path}";
        return true;
    }

    void IEditorUiHost.OpenItemEditor(object item, double x, double y)
    {
        if (item is PageItemModel pageItem)
        {
            OpenItemEditor(pageItem, x, y);
        }
    }

    bool IEditorUiHost.DeleteItem(object item)
        => item is PageItemModel pageItem && DeleteItem(pageItem);

    bool IEditorUiHost.IsEditMode => IsEditMode;

    string? IEditorUiHost.PrimaryTextBrush => PrimaryTextBrush;

    public void CancelListPopup()
    {
        _listPopupTarget = null;
        SelectionState.ShowListPicker = false;
    }

    public void CancelSelection()
    {
        SelectionState.IsSelecting = false;
        SelectionState.ShowPicker = false;
        SelectionState.Width = 0;
        SelectionState.Height = 0;
        CancelListPopup();
    }

    public void UpdateCanvasSize(double width, double height)
    {
        _canvasWidth = Math.Max(0, width);
        _canvasHeight = Math.Max(0, height);
        RefreshGridLines();

        // Aktualisiere den logischen Arbeitsbereich fuer ScrollViewer
        // basierend auf den aktuellen Widget-Positionen.
        if (Pages.Count > 0)
        {
            var page = SelectedPage;
            double maxRight = 0;
            double maxBottom = 0;

            foreach (var item in EnumeratePageItems(page.Items))
            {
                maxRight = Math.Max(maxRight, item.X + item.Width);
                maxBottom = Math.Max(maxBottom, item.Y + item.Height);
            }

            // Immer mindestens so gross wie der sichtbare Bereich.
            WorkspaceWidth = Math.Max(_canvasWidth, maxRight + 32);
            WorkspaceHeight = Math.Max(_canvasHeight, maxBottom + 32);
        }
    }

    public double SnapCoordinate(double value, double itemSize, double canvasSize)
    {
        var snapped = ShowGrid ? Math.Round(value / GridSize) * GridSize : value;
        return Clamp(snapped, 0, Math.Max(0, canvasSize - itemSize));
    }

    public double SnapLength(double value, double min, double max)
    {
        var snapped = ShowGrid ? Math.Round(value / GridSize) * GridSize : value;
        return Clamp(snapped, min, max);
    }

    public void SaveLayout()
    {
        if (Pages.Any(page => !string.IsNullOrWhiteSpace(page.UiFilePath)))
        {
            var savedTargets = new List<string>();
            if (TrySaveSelectedPageYaml(out var uiSaveTarget))
            {
                savedTargets.Add(uiSaveTarget);
            }

            if (TrySaveBookManifest(out var bookSaveTarget))
            {
                savedTargets.Add(bookSaveTarget);
            }

            StatusText = savedTargets.Count > 0
                ? $"Saved: {string.Join(" | ", savedTargets)}"
                : "No book files to save";
            return;
        }

        var document = new LayoutDocument
        {
            TabStripPlacement = TabStripPlacement.ToString(),
            Pages = Pages.Select(ToDocument).ToList()
        };

        var yamlTargetPath = Path.ChangeExtension(LayoutFilePath, ".yaml");
        TrySaveYamlLayoutFromObject(yamlTargetPath, JsonSerializer.SerializeToNode(document, _jsonOptions) as JsonObject);
        StatusText = $"Layout saved: {yamlTargetPath}";
    }

    private bool TrySaveSelectedPageYaml(out string savedTarget)
    {
        savedTarget = string.Empty;
        var uiFilePath = SelectedPage.UiFilePath;
        if (string.IsNullOrWhiteSpace(uiFilePath))
        {
            return false;
        }

        var template = SelectedPage.UiLayoutDefinition;
        var documentObject = CloneJsonObject(template?.DocumentProperties);
        documentObject["Page"] = SelectedPage.Name;
        documentObject["Title"] = !string.IsNullOrWhiteSpace(template?.Title) ? template!.Title : SelectedPage.Name;
        documentObject["Layout"] = ToUiNodeDocument(
            template?.Layout,
            SelectedPage.Items,
            defaultType: "Canvas");

        var directory = Path.GetDirectoryName(uiFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        TrySavePageYaml(uiFilePath, documentObject);
        savedTarget = $"Page.yaml: {Path.GetFileName(Path.GetDirectoryName(uiFilePath))}";
        return true;
    }

    private bool TrySaveBookManifest(out string savedTarget)
    {
        savedTarget = string.Empty;
        if (!Pages.Any(page => !string.IsNullOrWhiteSpace(page.UiFilePath)))
        {
            return false;
        }

        var bookManifestPath = GetBookManifestPath();
        if (string.IsNullOrWhiteSpace(bookManifestPath))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(bookManifestPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        WriteBookManifest(bookManifestPath);
        savedTarget = $"Book.udlb: {TabStripPlacement}";
        return true;
    }

    private static JsonObject ToUiNodeDocument(BookUiNode? templateNode, IEnumerable<PageItemModel> items, string defaultType)
    {
        var nodeObject = CloneJsonObject(templateNode?.Properties);
        nodeObject["Type"] = !string.IsNullOrWhiteSpace(templateNode?.Type) ? templateNode!.Type : defaultType;
        nodeObject["Children"] = new JsonArray(items.Select(item => (JsonNode?)ToUiNodeDocument(item)).ToArray());
        return nodeObject;
    }

    private static JsonObject ToUiNodeDocument(PageItemModel item)
    {
        var nodeObject = CloneJsonObject(item.UiProperties);
        nodeObject["Type"] = !string.IsNullOrWhiteSpace(item.UiNodeType) ? item.UiNodeType : GetDefaultUiType(item.Kind);
        nodeObject["Text"] = GetUiText(item);
        nodeObject["X"] = item.X;
        nodeObject["Y"] = item.Y;
        nodeObject["Width"] = item.Width;
        nodeObject["Height"] = item.Height;
        nodeObject["Name"] = item.Name;
        nodeObject["Id"] = item.Id;
        nodeObject["ControlCornerRadius"] = item.ControlCornerRadius;
        nodeObject["ControlCaption"] = item.ControlCaption;
        nodeObject["CaptionVisible"] = item.CaptionVisible;
        nodeObject["ShowCaption"] = item.ShowCaption;
        nodeObject["HeaderBorderWidth"] = item.HeaderBorderWidth;
        nodeObject["HeaderCornerRadius"] = item.HeaderCornerRadius;
        SetOptionalJsonValue(nodeObject, "HeaderForeColor", item.HeaderForeColor);
        SetOptionalJsonValue(nodeObject, "HeaderBackColor", item.HeaderBackColor);
        SetOptionalJsonValue(nodeObject, "HeaderBorderColor", item.HeaderBorderColor);
        nodeObject["BodyCaption"] = item.BodyCaption;
        nodeObject["BodyCaptionPosition"] = item.BodyCaptionPosition;
        nodeObject["BodyCaptionVisible"] = item.BodyCaptionVisible;
        nodeObject["ShowBodyCaption"] = item.ShowBodyCaption;
        nodeObject["ShowFooter"] = item.ShowFooter;
        SetOptionalJsonValue(nodeObject, "BodyForeColor", item.BodyForeColor);
        SetOptionalJsonValue(nodeObject, "BodyBackColor", item.BodyBackColor);
        SetOptionalJsonValue(nodeObject, "BodyBorderColor", item.BodyBorderColor);
        nodeObject["BodyBorderWidth"] = item.BodyBorderWidth;
        nodeObject["BodyCornerRadius"] = item.BodyCornerRadius;
        nodeObject["Footer"] = item.Footer;
        SetOptionalJsonValue(nodeObject, "FooterForeColor", item.FooterForeColor);
        SetOptionalJsonValue(nodeObject, "FooterBackColor", item.FooterBackColor);
        SetOptionalJsonValue(nodeObject, "FooterBorderColor", item.FooterBorderColor);
        nodeObject["FooterBorderWidth"] = item.FooterBorderWidth;
        nodeObject["FooterCornerRadius"] = item.FooterCornerRadius;
        nodeObject["ToolTipText"] = item.ToolTipText;
        nodeObject["View"] = item.View;
        nodeObject["Enabled"] = item.Enabled;
        nodeObject["ButtonText"] = item.ButtonText;
        SetOptionalJsonValue(nodeObject, "ButtonIcon", item.ButtonIcon);
        nodeObject["ButtonOnlyIcon"] = item.ButtonOnlyIcon;
        nodeObject["ButtonIconAlign"] = item.ButtonIconAlign;
        nodeObject["ButtonTextAlign"] = item.ButtonTextAlign;
        nodeObject["ButtonCommand"] = item.ButtonCommand;
        nodeObject["ButtonBodyBackground"] = item.ButtonBodyBackground;
        nodeObject["ButtonBodyForegroundColor"] = item.ButtonBodyForegroundColor;
        SetOptionalJsonValue(nodeObject, "ButtonIconColor", item.ButtonIconColor);
        nodeObject["UseThemeColor"] = item.UseThemeColor;
        SetOptionalJsonValue(nodeObject, "BackgroundColor", item.BackgroundColor);
        SetOptionalJsonValue(nodeObject, "BorderColor", item.BorderColor);
        SetOptionalJsonValue(nodeObject, "ContainerBorder", item.ContainerBorder);
        SetOptionalJsonValue(nodeObject, "ContainerBackgroundColor", item.ContainerBackgroundColor);
        nodeObject["ContainerBorderWidth"] = item.ContainerBorderWidth;
        nodeObject["BorderWidth"] = item.BorderWidth;
        nodeObject["CornerRadius"] = item.CornerRadius;
        SetOptionalJsonValue(nodeObject, "PrimaryForegroundColor", item.PrimaryForegroundColor);
        SetOptionalJsonValue(nodeObject, "SecondaryForegroundColor", item.SecondaryForegroundColor);
        SetOptionalJsonValue(nodeObject, "AccentBackgroundColor", item.AccentBackgroundColor);
        SetOptionalJsonValue(nodeObject, "AccentForegroundColor", item.AccentForegroundColor);
        nodeObject["TargetPath"] = TargetPathHelper.ToPersistedLayoutTargetPath(item.TargetPath);
        nodeObject["TargetParameterPath"] = item.TargetParameterPath;
        nodeObject["TargetParameterFormat"] = item.TargetParameterFormat;
            nodeObject["Unit"] = item.Unit;
        nodeObject["TargetLog"] = item.TargetLog;
        nodeObject["RefreshRateMs"] = item.RefreshRateMs;
        nodeObject["HistorySeconds"] = item.HistorySeconds;
        nodeObject["ViewSeconds"] = item.ViewSeconds;
        nodeObject["ChartSeriesDefinitions"] = TargetPathHelper.ToPersistedChartSeriesDefinitions(item.ChartSeriesDefinitions);
        if (TryBuildInteractionRulesJson(item.InteractionRules, out var interactionRulesJson))
        {
            nodeObject["InteractionRules"] = interactionRulesJson;
        }
        else
        {
            nodeObject.Remove("InteractionRules");
        }
        nodeObject["UdlClientHost"] = item.UdlClientHost;
        nodeObject["UdlClientPort"] = item.UdlClientPort;
        nodeObject["UdlClientAutoConnect"] = item.UdlClientAutoConnect;
        nodeObject["UdlClientDebugLogging"] = item.UdlClientDebugLogging;
        nodeObject["UdlAttachedItemPaths"] = item.UdlAttachedItemPaths;
        nodeObject["IsReadOnly"] = item.IsReadOnly;
        nodeObject["IsAutoHeight"] = item.IsAutoHeight;
        nodeObject["ListItemHeight"] = item.ListItemHeight;
        nodeObject["ControlHeight"] = item.ControlHeight;
        // Table layout
        nodeObject["Rows"] = item.TableRows;
        nodeObject["Columns"] = item.TableColumns;
        nodeObject["TableCellRow"] = item.TableCellRow;
        nodeObject["TableCellColumn"] = item.TableCellColumn;
        nodeObject["TableCellRowSpan"] = item.TableCellRowSpan;
        nodeObject["TableCellColumnSpan"] = item.TableCellColumnSpan;
        nodeObject["ControlBorderWidth"] = item.ControlBorderWidth;
        SetOptionalJsonValue(nodeObject, "ControlBorderColor", item.ControlBorderColor);

        if (item.Items.Count > 0)
        {
            nodeObject["Children"] = new JsonArray(item.Items.Select(child => (JsonNode?)ToUiNodeDocument(child)).ToArray());
        }
        else
        {
            nodeObject.Remove("Children");
        }

        return nodeObject;
    }

    private static JsonObject BuildYamlControlDefinition(PageItemModel item)
    {
        var node = new JsonObject
        {
            ["Type"] = !string.IsNullOrWhiteSpace(item.UiNodeType) ? item.UiNodeType : GetDefaultUiType(item.Kind),
            ["View"] = item.View,
            ["Enabled"] = item.Enabled
        };

        var identity = new JsonObject
        {
            ["Name"] = item.Name,
            ["Path"] = item.Path,
            ["Id"] = item.Id
        };
        node["Identity"] = identity;

        var rect = new JsonObject
        {
            ["X"] = item.X,
            ["Y"] = item.Y,
            ["Width"] = item.Width,
            ["Height"] = item.Height
        };
        node["Bounds"] = rect;

        var design = new JsonObject
        {
            ["CornerRadius"] = item.CornerRadius,
            ["BorderWidth"] = item.BorderWidth,
            ["BorderColor"] = item.BorderColor is null ? null : JsonValue.Create(item.BorderColor),
            ["BackColor"] = item.BackgroundColor is null ? null : JsonValue.Create(item.BackgroundColor),
            ["ToolTip"] = item.ToolTipText
        };
        node["Design"] = design;

        var header = new JsonObject
        {
            ["ControlCaption"] = item.ControlCaption,
            ["HeaderForeColor"] = item.HeaderForeColor is null ? null : JsonValue.Create(item.HeaderForeColor),
            ["CaptionVisible"] = item.CaptionVisible,
            ["HeaderCornerRadius"] = item.HeaderCornerRadius,
            ["HeaderBorderWidth"] = item.HeaderBorderWidth,
            ["HeaderBorderColor"] = item.HeaderBorderColor is null ? null : JsonValue.Create(item.HeaderBorderColor),
            ["HeaderBackColor"] = item.HeaderBackColor is null ? null : JsonValue.Create(item.HeaderBackColor)
        };
        node["Header"] = header;

        var body = new JsonObject
        {
            ["BodyCaption"] = item.BodyCaption,
            ["BodyCaptionPosition"] = item.BodyCaptionPosition,
            ["BodyForeColor"] = item.BodyForeColor is null ? null : JsonValue.Create(item.BodyForeColor),
            ["BodyCaptionVisible"] = item.BodyCaptionVisible,
            ["BodyCornerRadius"] = item.BodyCornerRadius,
            ["BodyBorderWidth"] = item.BodyBorderWidth,
            ["BodyBorderColor"] = item.BodyBorderColor is null ? null : JsonValue.Create(item.BodyBorderColor),
            ["BodyBackColor"] = item.BodyBackColor is null ? null : JsonValue.Create(item.BodyBackColor)
        };
        node["Body"] = body;

        var footer = new JsonObject
        {
            ["ShowFooter"] = item.ShowFooter,
            ["FooterCornerRadius"] = item.FooterCornerRadius,
            ["FooterBorderWidth"] = item.FooterBorderWidth,
            ["FooterBorderColor"] = item.FooterBorderColor is null ? null : JsonValue.Create(item.FooterBorderColor),
            ["FooterBackColor"] = item.FooterBackColor is null ? null : JsonValue.Create(item.FooterBackColor)
        };
        node["Footer"] = footer;

        if (TryBuildInteractionRulesJson(item.InteractionRules, out var interactionRules))
        {
            node["InteractionRules"] = interactionRules;
        }

        var control = new JsonObject();

        switch (item.Kind)
        {
            case ControlKind.Item or ControlKind.Signal:
                control["Unit"] = item.Unit;
                control["TargetPath"] = TargetPathHelper.ToPersistedLayoutTargetPath(item.TargetPath);
                control["TargetParameterPath"] = item.TargetParameterPath;
                control["TargetParameterFormat"] = item.TargetParameterFormat;
                control["IsReadOnly"] = item.IsReadOnly;
                control["RefreshRateMs"] = item.RefreshRateMs;
                control["Children"] = new JsonArray();
                break;
            case ControlKind.Button:
                control["ButtonText"] = item.ButtonText;
                control["ButtonIcon"] = item.ButtonIcon;
                control["ButtonIconColor"] = item.ButtonIconColor is null ? null : JsonValue.Create(item.ButtonIconColor);
                control["ButtonBackColor"] = item.ButtonBodyBackground is null ? null : JsonValue.Create(item.ButtonBodyBackground);
                control["ButtonOnlyIcon"] = item.ButtonOnlyIcon;
                control["ButtonIconAlign"] = item.ButtonIconAlign;
                control["ButtonTextAlign"] = item.ButtonTextAlign;
                break;
            case ControlKind.ListControl:
                control["ControlHeight"] = item.ControlHeight;
                var listChildren = new JsonArray(item.Items.Select(child => (JsonNode?)BuildYamlControlDefinition(child)).ToArray());
                control["Children"] = listChildren;
                break;
            case ControlKind.TableControl:
                control["Rows"] = item.TableRows;
                control["Columns"] = item.TableColumns;
                var cells = new JsonArray();
                foreach (var child in item.Items.Where(c => c.IsTableChildControl))
                {
                    var cell = new JsonObject
                    {
                        ["Row"] = child.TableCellRow,
                        ["Column"] = child.TableCellColumn,
                        ["RowSpan"] = child.TableCellRowSpan,
                        ["ColumnSpan"] = child.TableCellColumnSpan,
                        ["Child"] = BuildYamlControlDefinition(child)
                    };
                    cells.Add(cell);
                }

                control["Cells"] = cells;
                break;
            case ControlKind.LogControl:
                control["TargetLog"] = item.TargetLog;
                break;
            case ControlKind.ChartControl:
                control["RefreshRateMs"] = item.RefreshRateMs;
                control["HistorySeconds"] = item.HistorySeconds;
                control["ViewSeconds"] = item.ViewSeconds;
                if (!string.IsNullOrWhiteSpace(item.ChartSeriesDefinitions))
                {
                    control["ChartSeriesDefinitions"] = BuildChartSeriesDefinitionsArray(item.ChartSeriesDefinitions);
                }

                break;
            case ControlKind.UdlClientControl:
                control["UdlClientHost"] = item.UdlClientHost;
                control["UdlClientPort"] = item.UdlClientPort;
                control["UdlClientAutoConnect"] = item.UdlClientAutoConnect;
                control["UdlClientDebugLogging"] = item.UdlClientDebugLogging;
                control["UdlAttachedItemPaths"] = item.UdlAttachedItemPaths;
                break;
        }

        if (control.Count > 0)
        {
            node["Properties"] = control;
        }

        return node;
    }

    private static JsonArray BuildChartSeriesDefinitionsArray(string definitions)
    {
        var array = new JsonArray();
        if (string.IsNullOrWhiteSpace(definitions))
        {
            return array;
        }

        var lines = TargetPathHelper.ToPersistedChartSeriesDefinitions(definitions)
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            array.Add(line);
        }

        return array;
    }

    private static string GetUiText(PageItemModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.BodyCaption))
        {
            return item.BodyCaption;
        }

        if (!string.IsNullOrWhiteSpace(item.Name))
        {
            return item.Name;
        }

        return item.Kind switch
        {
            ControlKind.Button => "Button",
            ControlKind.ListControl => "ListControl",
            ControlKind.TableControl => "TableControl",
            ControlKind.LogControl => "LogControl",
            ControlKind.ChartControl => "ChartControl",
            ControlKind.UdlClientControl => "UdlClient",
            _ => "Item"
        };
    }

    protected static void ApplyKnownUiProperties(PageItemModel item, JsonObject properties, string pageName, string? fallbackType)
    {
        item.Name = GetStringProperty(properties, "Name") ?? item.Name;
        item.Id = GetStringProperty(properties, "Id") ?? item.Id;
        item.Enabled = GetBoolProperty(properties, "Enabled") ?? item.Enabled;
        item.View = GetIntProperty(properties, "View") ?? item.View;
        item.ControlCaption = GetFirstStringProperty(properties, "ControlCaption", "Header") ?? item.ControlCaption;
        item.CaptionVisible = GetFirstBoolProperty(properties, "CaptionVisible", "ShowCaption") ?? item.CaptionVisible;
        item.BodyCaption = GetFirstStringProperty(properties, "BodyCaption", "Title") ?? item.BodyCaption;
        item.BodyCaptionPosition = GetStringProperty(properties, "BodyCaptionPosition") ?? item.BodyCaptionPosition;
        item.BodyCaptionVisible = GetFirstBoolProperty(properties, "BodyCaptionVisible", "ShowBodyCaption") ?? item.BodyCaptionVisible;
        item.ShowFooter = GetBoolProperty(properties, "ShowFooter") ?? item.ShowFooter;
        item.Footer = GetStringProperty(properties, "Footer") ?? item.Footer;
        item.ToolTipText = GetStringProperty(properties, "ToolTipText") ?? item.ToolTipText;
        item.ButtonText = GetStringProperty(properties, "ButtonText") ?? item.BodyCaption;
        item.ButtonIcon = GetStringProperty(properties, "ButtonIcon") ?? item.ButtonIcon;
        item.ButtonOnlyIcon = GetBoolProperty(properties, "ButtonOnlyIcon") ?? item.ButtonOnlyIcon;
        item.ButtonIconAlign = GetStringProperty(properties, "ButtonIconAlign") ?? item.ButtonIconAlign;
        item.ButtonTextAlign = GetStringProperty(properties, "ButtonTextAlign") ?? item.ButtonTextAlign;
        item.ButtonCommand = GetStringProperty(properties, "ButtonCommand") ?? item.ButtonCommand;

        var buttonBodyBackground = GetStringProperty(properties, "ButtonBodyBackground");
        if (buttonBodyBackground is not null)
        {
            // Alte Layouts hatten "Transparent" als Platzhalter für "Theme".
            // Beim Einlesen behandeln wir diesen Wert jetzt wie "leer" (Theme-Default).
            item.ButtonBodyBackground = string.Equals(buttonBodyBackground.Trim(), "Transparent", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : buttonBodyBackground;
        }

        item.ButtonBodyForegroundColor = GetStringProperty(properties, "ButtonBodyForegroundColor") ?? item.ButtonBodyForegroundColor;
        item.ButtonIconColor = GetStringProperty(properties, "ButtonIconColor") ?? item.ButtonIconColor;
        item.UseThemeColor = GetBoolProperty(properties, "UseThemeColor") ?? item.UseThemeColor;
        item.BackgroundColor = GetStringProperty(properties, "BackgroundColor") ?? item.BackgroundColor;
        item.BorderColor = GetStringProperty(properties, "BorderColor") ?? item.BorderColor;
        item.ContainerBorder = GetStringProperty(properties, "ContainerBorder") ?? item.ContainerBorder;
        item.ContainerBackgroundColor = GetStringProperty(properties, "ContainerBackgroundColor") ?? item.ContainerBackgroundColor;
        item.ContainerBorderWidth = GetDoubleProperty(properties, "ContainerBorderWidth") ?? item.ContainerBorderWidth;
        item.BorderWidth = GetDoubleProperty(properties, "BorderWidth") ?? item.BorderWidth;
        item.CornerRadius = GetDoubleProperty(properties, "CornerRadius") ?? item.CornerRadius;
        item.PrimaryForegroundColor = GetStringProperty(properties, "PrimaryForegroundColor") ?? item.PrimaryForegroundColor;
        item.SecondaryForegroundColor = GetStringProperty(properties, "SecondaryForegroundColor") ?? item.SecondaryForegroundColor;
        item.AccentBackgroundColor = GetStringProperty(properties, "AccentBackgroundColor") ?? item.AccentBackgroundColor;
        item.AccentForegroundColor = GetStringProperty(properties, "AccentForegroundColor") ?? item.AccentForegroundColor;
        item.HeaderForeColor = GetFirstStringProperty(properties, "HeaderForeColor", "SecondaryForegroundColor") ?? item.HeaderForeColor;
        item.HeaderBackColor = GetStringProperty(properties, "HeaderBackColor") ?? item.HeaderBackColor;
        item.HeaderBorderColor = GetStringProperty(properties, "HeaderBorderColor") ?? item.HeaderBorderColor;
        item.HeaderBorderWidth = GetDoubleProperty(properties, "HeaderBorderWidth") ?? item.HeaderBorderWidth;
        item.HeaderCornerRadius = GetDoubleProperty(properties, "HeaderCornerRadius") ?? item.HeaderCornerRadius;
        item.BodyForeColor = GetFirstStringProperty(properties, "BodyForeColor", "PrimaryForegroundColor") ?? item.BodyForeColor;
        item.BodyBackColor = GetFirstStringProperty(properties, "BodyBackColor", "ContainerBackgroundColor") ?? item.BodyBackColor;
        item.BodyBorderColor = GetFirstStringProperty(properties, "BodyBorderColor", "ContainerBorder") ?? item.BodyBorderColor;
        item.BodyBorderWidth = GetFirstDoubleProperty(properties, "BodyBorderWidth", "ContainerBorderWidth") ?? item.BodyBorderWidth;
        item.BodyCornerRadius = GetFirstDoubleProperty(properties, "BodyCornerRadius", "ControlCornerRadius") ?? item.BodyCornerRadius;
        item.FooterForeColor = GetStringProperty(properties, "FooterForeColor") ?? item.FooterForeColor;
        item.FooterBackColor = GetStringProperty(properties, "FooterBackColor") ?? item.FooterBackColor;
        item.FooterBorderColor = GetStringProperty(properties, "FooterBorderColor") ?? item.FooterBorderColor;
        item.FooterBorderWidth = GetDoubleProperty(properties, "FooterBorderWidth") ?? item.FooterBorderWidth;
        item.FooterCornerRadius = GetDoubleProperty(properties, "FooterCornerRadius") ?? item.FooterCornerRadius;
        item.TargetPath = TargetPathHelper.NormalizeConfiguredTargetPath(GetStringProperty(properties, "TargetPath") ?? item.TargetPath);
        item.TargetParameterPath = GetStringProperty(properties, "TargetParameterPath") ?? item.TargetParameterPath;
        item.TargetParameterFormat = GetStringProperty(properties, "TargetParameterFormat") ?? item.TargetParameterFormat;
            item.Unit = GetFirstStringProperty(properties, "Unit", "Footer") ?? item.Unit;
        item.TargetLog = GetStringProperty(properties, "TargetLog") ?? item.TargetLog;
        item.RefreshRateMs = GetIntProperty(properties, "RefreshRateMs") ?? item.RefreshRateMs;
        item.HistorySeconds = GetIntProperty(properties, "HistorySeconds") ?? item.HistorySeconds;
        item.ViewSeconds = GetIntProperty(properties, "ViewSeconds") ?? item.ViewSeconds;
        item.ChartSeriesDefinitions = TargetPathHelper.NormalizeChartSeriesDefinitions(GetStringProperty(properties, "ChartSeriesDefinitions") ?? item.ChartSeriesDefinitions);
        item.InteractionRules = ReadInteractionRulesProperty(properties) ?? item.InteractionRules;
        item.UdlClientHost = GetStringProperty(properties, "UdlClientHost") ?? item.UdlClientHost;
        item.UdlClientPort = GetIntProperty(properties, "UdlClientPort") ?? item.UdlClientPort;
        item.UdlClientAutoConnect = GetBoolProperty(properties, "UdlClientAutoConnect") ?? item.UdlClientAutoConnect;
        item.UdlClientDebugLogging = GetBoolProperty(properties, "UdlClientDebugLogging") ?? item.UdlClientDebugLogging;
        item.UdlAttachedItemPaths = GetStringProperty(properties, "UdlAttachedItemPaths") ?? item.UdlAttachedItemPaths;
        item.IsReadOnly = GetBoolProperty(properties, "IsReadOnly") ?? item.IsReadOnly;
        item.IsAutoHeight = GetBoolProperty(properties, "IsAutoHeight") ?? item.IsAutoHeight;
        item.ListItemHeight = GetDoubleProperty(properties, "ListItemHeight") ?? item.ListItemHeight;
        item.ControlHeight = GetDoubleProperty(properties, "ControlHeight") ?? item.ControlHeight;
        // Table layout
        item.TableRows = GetIntProperty(properties, "Rows") ?? item.TableRows;
        item.TableColumns = GetIntProperty(properties, "Columns") ?? item.TableColumns;
        item.TableCellRow = GetIntProperty(properties, "TableCellRow") ?? item.TableCellRow;
        item.TableCellColumn = GetIntProperty(properties, "TableCellColumn") ?? item.TableCellColumn;
        item.TableCellRowSpan = GetIntProperty(properties, "TableCellRowSpan") ?? item.TableCellRowSpan;
        item.TableCellColumnSpan = GetIntProperty(properties, "TableCellColumnSpan") ?? item.TableCellColumnSpan;
        item.ControlBorderWidth = GetDoubleProperty(properties, "ControlBorderWidth") ?? item.ControlBorderWidth;
        item.ControlBorderColor = GetStringProperty(properties, "ControlBorderColor") ?? item.ControlBorderColor;
        item.ControlCornerRadius = GetDoubleProperty(properties, "ControlCornerRadius") ?? item.ControlCornerRadius;
        item.UiNodeType = GetStringProperty(properties, "Type") ?? fallbackType ?? item.UiNodeType;

        if (string.IsNullOrWhiteSpace(item.ControlCaption) && item.IsItem)
        {
              item.ControlCaption = item.Name;
        }
    }

    private static bool TryBuildInteractionRulesJson(string definitions, out JsonArray array)
    {
        var rules = ItemInteractionRuleCodec.ParseDefinitions(definitions);
        array = new JsonArray(rules.Select(static rule => (JsonNode?)new JsonObject
        {
            ["Event"] = rule.Event.ToString(),
            ["Action"] = rule.Action.ToString(),
            ["TargetPath"] = TargetPathHelper.ToPersistedLayoutTargetPath(rule.TargetPath),
            ["Argument"] = rule.Argument
        }).ToArray());

        return rules.Count > 0;
    }

    private static string? ReadInteractionRulesProperty(JsonObject properties)
    {
        if (properties["InteractionRules"] is JsonArray array)
        {
            return ItemInteractionRuleCodec.SerializeDefinitions(array
                .OfType<JsonObject>()
                .Select(static ruleObject => new ItemInteractionRule
                {
                    Event = Enum.TryParse<ItemInteractionEvent>(GetStringValue(ruleObject, "Event"), ignoreCase: true, out var eventKind) ? eventKind : ItemInteractionEvent.BodyLeftClick,
                    Action = Enum.TryParse<ItemInteractionAction>(GetStringValue(ruleObject, "Action"), ignoreCase: true, out var actionKind) ? actionKind : ItemInteractionAction.OpenValueEditor,
                    TargetPath = TargetPathHelper.NormalizeConfiguredTargetPath(GetStringValue(ruleObject, "TargetPath") ?? "this"),
                    Argument = GetStringValue(ruleObject, "Argument") ?? string.Empty
                }));
        }

        return GetStringProperty(properties, "InteractionRules");
    }

    private static string? GetStringValue(JsonObject properties, string propertyName)
    {
        var value = properties[propertyName];
        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var result) => result,
            _ => null
        };
    }

    private static string? GetStringProperty(JsonObject properties, string propertyName)
    {
        var value = properties[propertyName];
        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var result) => result,
            _ => null
        };
    }

    private static string? GetFirstStringProperty(JsonObject properties, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetStringProperty(properties, propertyName);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static double? GetDoubleProperty(JsonObject properties, string propertyName)
    {
        var value = properties[propertyName];
        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<double>(out var result) => result,
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) && double.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    private static double? GetFirstDoubleProperty(JsonObject properties, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetDoubleProperty(properties, propertyName);
            if (value.HasValue)
            {
                return value;
            }
        }

        return null;
    }

    private static int? GetIntProperty(JsonObject properties, string propertyName)
    {
        var value = properties[propertyName];
        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<int>(out var result) => result,
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    private static bool? GetBoolProperty(JsonObject properties, string propertyName)
    {
        var value = properties[propertyName];
        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var result) => result,
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) && bool.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    private static bool? GetFirstBoolProperty(JsonObject properties, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetBoolProperty(properties, propertyName);
            if (value.HasValue)
            {
                return value;
            }
        }

        return null;
    }

    private static void SetOptionalJsonValue(JsonObject nodeObject, string propertyName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            nodeObject.Remove(propertyName);
            return;
        }

        nodeObject[propertyName] = value;
    }

    protected static string GetDefaultUiType(ControlKind kind)
    {
        return kind switch
        {
            ControlKind.Button => "Button",
            ControlKind.ListControl => "ListControl",
            ControlKind.TableControl => "TableControl",
            ControlKind.LogControl => "LogControl",
            ControlKind.ChartControl => "ChartControl",
            ControlKind.UdlClientControl => "UdlClientControl",
            _ => "Item"
        };
    }

    protected static JsonObject CloneJsonObject(JsonObject? source)
    {
        return source?.DeepClone() as JsonObject ?? new JsonObject();
    }

    private void TrySavePageYaml(string uiFilePath, JsonObject documentObject)
    {
        var yamlPath = Path.ChangeExtension(uiFilePath, ".yaml");

        try
        {
            var directory = Path.GetDirectoryName(yamlPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var root = new JsonObject();
            root["Page"] = SelectedPage.Name;
            var title = !string.IsNullOrWhiteSpace(SelectedPage.DisplayText)
                ? SelectedPage.DisplayText
                : GetStringProperty(documentObject, "Title") ?? SelectedPage.Name;
            // In der YAML-Repräsentation verwenden wir "Caption" als Page-Titel.
            root["Caption"] = title;

            var pageViews = SelectedPage.Views.Count > 0
                ? SelectedPage.Views
                : new Dictionary<int, string> { [1] = "HomeScreen" };

            var views = new JsonObject(pageViews
                .OrderBy(static entry => entry.Key)
                .Select(static entry => new KeyValuePair<string, JsonNode?>(entry.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), entry.Value)));
            root["Views"] = views;

            var controls = new JsonArray(SelectedPage.Items.Select(item => (JsonNode?)BuildYamlControlDefinition(item)).ToArray());
            root["Controls"] = controls;

            using var writer = new StreamWriter(yamlPath, append: false);
            WriteYamlObject(root, writer, indent: 0);
        }
        catch
        {
            // YAML ist ein optionales Begleitformat; IO-Fehler werden ignoriert.
        }
    }

    private static void TrySaveYamlLayoutFromObject(string jsonPath, JsonObject? rootObject)
    {
        if (string.IsNullOrWhiteSpace(jsonPath) || rootObject is null)
        {
            return;
        }

        var yamlPath = Path.ChangeExtension(jsonPath, ".yaml");

        try
        {
            var directory = Path.GetDirectoryName(yamlPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var writer = new StreamWriter(yamlPath, append: false);
            WriteYamlObject(rootObject, writer, indent: 0);
        }
        catch
        {
            // YAML ist aktuell ein optionales Begleitformat; IO-Fehler werden ignoriert.
        }
    }

    private static void WriteYamlObject(JsonObject obj, TextWriter writer, int indent)
    {
        foreach (var property in obj)
        {
            WriteYamlProperty(property.Key, property.Value, writer, indent);
        }
    }

    private static void WriteYamlProperty(string key, JsonNode? value, TextWriter writer, int indent)
    {
        var indentText = new string(' ', indent);
        switch (value)
        {
            case JsonObject childObj:
                writer.WriteLine($"{indentText}{key}:");
                WriteYamlObject(childObj, writer, indent + 2);
                break;
            case JsonArray array:
                writer.WriteLine($"{indentText}{key}:");
                WriteYamlArray(array, writer, indent + 2);
                break;
            default:
                writer.WriteLine($"{indentText}{key}: {FormatYamlScalar(value)}");
                break;
        }
    }

    private static void WriteYamlArray(JsonArray array, TextWriter writer, int indent)
    {
        var indentText = new string(' ', indent);
        foreach (var element in array)
        {
            switch (element)
            {
                case JsonObject obj:
                    writer.WriteLine($"{indentText}-");
                    WriteYamlObject(obj, writer, indent + 2);
                    break;
                case JsonArray nestedArray:
                    writer.WriteLine($"{indentText}-");
                    WriteYamlArray(nestedArray, writer, indent + 2);
                    break;
                default:
                    writer.WriteLine($"{indentText}- {FormatYamlScalar(element)}");
                    break;
            }
        }
    }

    private static string FormatYamlScalar(JsonNode? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<bool>(out var boolResult))
            {
                return boolResult ? "true" : "false";
            }

            if (jsonValue.TryGetValue<long>(out var longResult))
            {
                return longResult.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (jsonValue.TryGetValue<double>(out var doubleResult))
            {
                return doubleResult.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (jsonValue.TryGetValue<string>(out var stringResult))
            {
                return QuoteYamlString(stringResult);
            }
        }

        return QuoteYamlString(value.ToJsonString());
    }

    private static string QuoteYamlString(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "''";
        }

        var normalized = text
            .Replace("\r\n", "\\n")
            .Replace("\r", "\\n")
            .Replace("\n", "\\n");

        normalized = normalized.Replace("'", "''");
        return $"'{normalized}'";
    }

    private void SetTabStripPlacement(string? placement)
    {
        TabStripPlacement = ParseTabStripPlacement(placement);
    }

    private void ToggleHeaderCollapsed()
    {
        IsHeaderCollapsed = !IsHeaderCollapsed;
        StatusText = IsHeaderCollapsed ? "Header collapsed" : "Header expanded";
    }

    private void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;
        if (IsEditMode)
        {
            StatusText = "Edit mode enabled";
        }
    }

    protected void ApplyBookManifestSettings(string? bookRootDirectory)
    {
        var manifest = ReadBookManifest(GetBookManifestPath(bookRootDirectory));
        TabStripPlacement = ParseTabStripPlacement(manifest.TabStripPlacement);

        if (!string.IsNullOrWhiteSpace(manifest.Theme))
        {
            IsDarkTheme = string.Equals(manifest.Theme, "Dark", StringComparison.OrdinalIgnoreCase);
        }
    }

    protected virtual string? CurrentProjectRootDirectory => null;

    protected string? GetBookManifestPath(string? bookRootDirectory = null)
    {
        var rootDirectory = string.IsNullOrWhiteSpace(bookRootDirectory) ? CurrentProjectRootDirectory : bookRootDirectory;
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return null;
        }

        return Path.Combine(rootDirectory, "Book.udlb");
    }

    private void WriteBookManifest(string path)
    {
        try
        {
            using var writer = new StreamWriter(path, false);
            writer.WriteLine("Design:");
            writer.WriteLine($"  TabStripPlacement: {QuoteYamlString(TabStripPlacement.ToString())}");
            writer.WriteLine($"  SaveDate: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)}");
            writer.WriteLine($"  Theme: {(IsDarkTheme ? "Dark" : "Light")}");
            writer.WriteLine("Passwords:");
            writer.WriteLine("  Service: \"service\"");
            writer.WriteLine("  Admin: \"admin\"");
        }
        catch
        {
            // Manifest persistence is best-effort.
        }
    }

    private static BookManifestSettings ReadBookManifest(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new BookManifestSettings();
        }

        try
        {
            string? tabStripPlacement = null;
            string? theme = null;
            var lines = File.ReadAllLines(path);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("TabStripPlacement:", StringComparison.OrdinalIgnoreCase))
                {
                    tabStripPlacement = line[(line.IndexOf(':') + 1)..].Trim().Trim('\'', '"');
                    continue;
                }

                if (line.StartsWith("Theme:", StringComparison.OrdinalIgnoreCase))
                {
                    theme = line[(line.IndexOf(':') + 1)..].Trim().Trim('\'', '"');
                }
            }

            return new BookManifestSettings
            {
                TabStripPlacement = tabStripPlacement,
                Theme = theme
            };
        }
        catch
        {
            return new BookManifestSettings();
        }
    }

    private sealed class BookManifestSettings
    {
        public string? TabStripPlacement { get; init; }
        public string? Theme { get; init; }
    }

    protected static JsonObject LoadJsonObject(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new JsonObject();
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
    }

    protected static string? GetPageDisplayText(BookProjectPage page)
    {
        var metadata = LoadJsonObject(page.MetadataFile);
        return GetStringProperty(metadata, "Title")
            ?? GetStringProperty(metadata, "Text")
            ?? GetStringProperty(metadata, "Name");
    }

    private static Dock ParseTabStripPlacement(string? value)
    {
        return Enum.TryParse<Dock>(value, true, out var parsed)
            ? parsed
            : Dock.Right;
    }

    public void LoadLayout()
    {
        if (!File.Exists(LayoutFilePath))
        {
            StatusText = $"No layout file found: {LayoutFilePath}";
            return;
        }

        var json = File.ReadAllText(LayoutFilePath);
        var document = JsonSerializer.Deserialize<LayoutDocument>(json, _jsonOptions);

        if (document is null || document.Pages.Count == 0)
        {
            StatusText = "Layout file is empty or invalid";
            return;
        }

        TabStripPlacement = ParseTabStripPlacement(document.TabStripPlacement);
        SetPages(document.Pages.Select(ToModel).ToList());
        StatusText = $"Layout loaded: {LayoutFilePath}";
    }

    public PageItemModel CreateItem(ControlKind kind, double x, double y, double width, double height)
    {
        var item = kind switch
        {
            ControlKind.Button => new PageItemModel
            {
                Kind = ControlKind.Button,
                ControlCaption = string.Empty,
                BodyCaption = "Button",
                BodyCaptionVisible = false,
                Footer = "Action button",
                ShowFooter = false,
                ButtonText = "Button",
                ButtonTextAlign = "Center",
                ButtonIconAlign = "Left",
                BodyCornerRadius = 8,
                X = x,
                Y = y,
                Width = Math.Max(width, 140),
                Height = Math.Max(height, 56)
            },
            ControlKind.Signal => CreateDefaultItem(x, y, width, height),
            ControlKind.Item => CreateDefaultItem(x, y, width, height),
            ControlKind.ListControl => new PageItemModel
            {
                Kind = ControlKind.ListControl,
                ControlCaption = string.Empty,
                BodyCaption = "ListControl",
                Footer = "Drop controls here",
                X = x,
                Y = y,
                Width = Math.Max(width, 260),
                Height = Math.Max(height, 220),
                IsAutoHeight = true,
                ListItemHeight = 72,
                ContainerBorderWidth = 0,
                ControlBorderWidth = 0,
                ControlCornerRadius = 0
            },
            ControlKind.TableControl => new PageItemModel
            {
                Kind = ControlKind.TableControl,
                ControlCaption = string.Empty,
                BodyCaption = "TableControl",
                Footer = string.Empty,
                X = x,
                Y = y,
                Width = Math.Max(width, 260),
                Height = Math.Max(height, 220),
                ContainerBorderWidth = 0,
                ControlBorderWidth = 0,
                ControlCornerRadius = 0
            },
            ControlKind.LogControl => new PageItemModel
            {
                Kind = ControlKind.LogControl,
                ControlCaption = "ProcessLog",
                BodyCaption = string.Empty,
                Footer = string.Empty,
                TargetLog = "Logs/Host",
                X = x,
                Y = y,
                Width = Math.Max(width, 420),
                Height = Math.Max(height, 260),
                ContainerBorderWidth = 0
            },
            ControlKind.ChartControl => new PageItemModel
            {
                Kind = ControlKind.ChartControl,
                ControlCaption = string.Empty,
                BodyCaption = "Chart",
                Footer = "Live numeric trend",
                X = x,
                Y = y,
                Width = Math.Max(width, 520),
                Height = Math.Max(height, 260),
                ContainerBorderWidth = 0,
                HistorySeconds = 120,
                ViewSeconds = 30,
                RefreshRateMs = 100
            },
            ControlKind.UdlClientControl => new PageItemModel
            {
                Kind = ControlKind.UdlClientControl,
                Name = "UdlClientControl",
                ControlCaption = string.Empty,
                BodyCaption = string.Empty,
                BodyCaptionVisible = false,
                ShowFooter = false,
                Footer = "Disconnected",
                UdlClientHost = "192.168.178.151",
                UdlClientPort = 9001,
                UdlClientAutoConnect = false,
                UdlClientDebugLogging = false,
                X = x,
                Y = y,
                Width = Math.Max(width, 420),
                Height = Math.Max(height, 170),
                ContainerBorderWidth = 0
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        item.View = SelectedPage.ActualViewId;
        return item;
    }

    private static PageItemModel CreateDefaultItem(double x, double y, double width, double height)
    {
        var item = new PageItemModel
        {
            Kind = ControlKind.Item,
            ControlCaption = "Item",
            BodyCaption = "Value",
            Unit = string.Empty,
            ShowFooter = false,
            BodyCornerRadius = 8,
            X = x,
            Y = y,
            Width = Math.Max(width, 150),
            Height = Math.Max(height, 72)
        };

        item.ApplyTargetSelection(DemoTargetPath);
        return item;
    }

    private void AlignLeft()
    {
        if (!HasMultiSelection || SelectedItem is null)
        {
            return;
        }

        var target = SelectedItem.X;
        foreach (var item in _selectedItems.Where(item => !ReferenceEquals(item, SelectedItem)))
        {
            item.X = SnapCoordinate(target, item.Width, _canvasWidth);
        }

        StatusText = "Links am Master ausgerichtet";
    }

    private void AlignRight()
    {
        if (!HasMultiSelection || SelectedItem is null)
        {
            return;
        }

        var target = SelectedItem.X + SelectedItem.Width;
        foreach (var item in _selectedItems.Where(item => !ReferenceEquals(item, SelectedItem)))
        {
            item.X = SnapCoordinate(target - item.Width, item.Width, _canvasWidth);
        }

        StatusText = "Rechts am Master ausgerichtet";
    }

    private void AlignTop()
    {
        if (!HasMultiSelection || SelectedItem is null)
        {
            return;
        }

        var target = SelectedItem.Y;
        foreach (var item in _selectedItems.Where(item => !ReferenceEquals(item, SelectedItem)))
        {
            item.Y = SnapCoordinate(target, item.Height, _canvasHeight);
        }

        StatusText = "Oben am Master ausgerichtet";
    }

    private void AlignBottom()
    {
        if (!HasMultiSelection || SelectedItem is null)
        {
            return;
        }

        var target = SelectedItem.Y + SelectedItem.Height;
        foreach (var item in _selectedItems.Where(item => !ReferenceEquals(item, SelectedItem)))
        {
            item.Y = SnapCoordinate(target - item.Height, item.Height, _canvasHeight);
        }

        StatusText = "Unten am Master ausgerichtet";
    }

    private void AlignHorizontalCenter()
    {
        if (!HasMultiSelection || SelectedItem is null)
        {
            return;
        }

        var center = SelectedItem.X + SelectedItem.Width / 2;
        foreach (var item in _selectedItems.Where(item => !ReferenceEquals(item, SelectedItem)))
        {
            item.X = SnapCoordinate(center - item.Width / 2, item.Width, _canvasWidth);
        }

        StatusText = "Horizontal am Master zentriert";
    }

    private void AlignVerticalCenter()
    {
        if (!HasMultiSelection || SelectedItem is null)
        {
            return;
        }

        var center = SelectedItem.Y + SelectedItem.Height / 2;
        foreach (var item in _selectedItems.Where(item => !ReferenceEquals(item, SelectedItem)))
        {
            item.Y = SnapCoordinate(center - item.Height / 2, item.Height, _canvasHeight);
        }

        StatusText = "Vertikal am Master zentriert";
    }

    private void MatchWidth()
    {
        if (!HasMultiSelection || SelectedItem is null)
        {
            return;
        }

        foreach (var item in _selectedItems.Where(item => !ReferenceEquals(item, SelectedItem)))
        {
            item.Width = SnapLength(SelectedItem.Width, item.MinWidth, MaxAvailableWidth(item.X));
        }

        StatusText = "Breiten an Master angeglichen";
    }

    private void MatchHeight()
    {
        if (!HasMultiSelection || SelectedItem is null)
        {
            return;
        }

        foreach (var item in _selectedItems.Where(item => !ReferenceEquals(item, SelectedItem)))
        {
            item.Height = SnapLength(SelectedItem.Height, item.MinHeight, MaxAvailableHeight(item.Y));
        }

        StatusText = "Hoehen an Master angeglichen";
    }

    protected void SetPages(IReadOnlyList<PageModel> pages)
    {
        Pages.Clear();

        foreach (var page in pages)
        {
            AttachPage(page);
            AttachHierarchy(page);
            Pages.Add(page);
        }

        _selectedPage = Pages[0];

        foreach (var page in Pages)
        {
            page.IsSelected = false;
        }

        _selectedPage.IsSelected = true;
        ApplyThemeToAllItems();
        ClearItemSelection();
        CancelSelection();
        CancelEditorDialog();
        OnPropertyChanged(nameof(SelectedPage));
        OnPropertyChanged(nameof(FooterText));
    }

    private void AttachPage(PageModel page)
    {
        page.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PageModel.Name))
            {
                AttachHierarchy(page);
            }

            if (page == SelectedPage && (args.PropertyName == nameof(PageModel.ItemSummary) || args.PropertyName == nameof(PageModel.Name)))
            {
                OnPropertyChanged(nameof(FooterText));
            }
        };
    }

    private void OpenEditorDialog(EditorDialogMode mode, PageItemModel item, PageItemModel? parentItem, double x, double y, string title)
    {
        ResetEditorDialogSubscriptions();
        _editorDialogMode = mode;
        _editorDialogItem = item;
        _editorDialogParentItem = parentItem;
        EditorDialogTitle = title;
        EditorDialogError = string.Empty;
        const double dialogWidth = 760;
        const double dialogHeight = 820;
        var availableWidth = _canvasWidth > 0 ? _canvasWidth : 1200;
        var availableHeight = _canvasHeight > 0 ? _canvasHeight : 900;
        EditorDialogX = Math.Max(24, (availableWidth - dialogWidth) / 2);
        EditorDialogY = Math.Max(24, (availableHeight - dialogHeight) / 2);
        RebuildEditorDialogSections(item);
        RefreshEditorDialogChoiceOptions(item);
        IsEditorDialogOpen = true;
    }

    private void RebuildEditorDialogSections(PageItemModel item)
    {
        EditorDialogSections.Clear();
        foreach (var section in BuildSectionsForItem(item))
        {
            EditorDialogSections.Add(section);
            foreach (var field in section.Fields)
            {
                field.PropertyChanged += OnEditorDialogFieldChanged;
            }
        }

        EditorDialogActionFields.Clear();
        foreach (var field in BuildActionFieldsForItem(item))
        {
            field.PropertyChanged += OnEditorDialogFieldChanged;
            EditorDialogActionFields.Add(field);
        }

        OnPropertyChanged(nameof(HasEditorDialogActionFields));
        OnPropertyChanged(nameof(ShowEditorDialogActionPlaceholder));
    }

    private void ResetEditorDialogSubscriptions()
    {
        foreach (var section in EditorDialogSections)
        {
            foreach (var field in section.Fields)
            {
                field.PropertyChanged -= OnEditorDialogFieldChanged;
            }
        }

        foreach (var field in EditorDialogActionFields)
        {
            field.PropertyChanged -= OnEditorDialogFieldChanged;
        }
    }


    private void OnDataRegistryStructureChanged(object? sender, DataChangedEventArgs e)
    {
        if (Interlocked.Exchange(ref _dataRegistryRefreshQueued, 1) == 1)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _dataRegistryRefreshQueued, 0);
            RefreshDataRegistryDiagnostics();
            RefreshOpenEditorDialogChoiceOptions();
        }, DispatcherPriority.Background);
    }

    private void RefreshOpenEditorDialogChoiceOptions()
    {
        if (!IsEditorDialogOpen || _editorDialogItem is null)
        {
            return;
        }

        RefreshEditorDialogChoiceOptions(_editorDialogItem);
    }

    private void RefreshEditorDialogChoiceOptions(PageItemModel item)
    {
        var wasRefreshing = _isRefreshingEditorDialogFields;
        _isRefreshingEditorDialogFields = true;
        try
        {
            foreach (var field in EnumerateEditorDialogFields())
            {
                if (field.IsAttachItemList)
                {
                    var attachOptions = field.Definition.OptionsFactory is null
                        ? []
                        : field.Definition.OptionsFactory(item);
                    field.RefreshAttachItemOptions(attachOptions);
                    continue;
                }

                if (field.IsTargetTree)
                {
                    var targetOptions = field.Definition.OptionsFactory is null
                        ? []
                        : field.Definition.OptionsFactory(item);
                    field.RefreshTargetTreeOptions(targetOptions);
                    continue;
                }

                if (field.IsInteractionRuleList)
                {
                    field.RefreshInteractionRuleTargetOptions(GetSelectableTargetOptions(item));
                    continue;
                }

                if (!field.IsChoice)
                {
                    continue;
                }

                var selectedTargetPath = GetSelectedTargetPath(item);
                var choiceOptions = field.Key switch
                {
                    "TargetParameterPath" => GetTargetParameterOptions(selectedTargetPath),
                    _ when field.Definition.OptionsFactory is not null => field.Definition.OptionsFactory(item),
                    _ => []
                };

                var selectFirstWhenInvalid = field.Key == "TargetParameterPath" && !string.IsNullOrWhiteSpace(selectedTargetPath);
                RefreshDialogFieldOptions(field, choiceOptions, selectFirstWhenInvalid);
            }
        }
        finally
        {
            _isRefreshingEditorDialogFields = wasRefreshing;
        }

        UpdateEditorDialogChoiceDiagnostics();
    }

    private string GetSelectedTargetPath(PageItemModel item)
        => FindDialogField("TargetPath")?.Value ?? item.TargetPath ?? string.Empty;

    private void RefreshDataRegistryDiagnostics()
    {
        var keys = HostRegistries.Data.GetAllKeys()
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var preview = keys.Count == 0
            ? "-"
            : string.Join(", ", keys.Take(3));

        if (keys.Count > 3)
        {
            preview += ", ...";
        }

        DataRegistrySummary = $"HostRegistry: {keys.Count} Items | {preview}";
    }

    private void UpdateEditorDialogChoiceDiagnostics()
    {
        var targetField = FindDialogField("TargetPath");
        var targetLogField = FindDialogField("TargetLog");
        var parameterField = FindDialogField("TargetParameterPath");

        if (targetField is null && targetLogField is null)
        {
            EditorDialogChoiceSummary = IsEditorDialogOpen
                ? "Dialog Choices: no target field"
                : "Dialog Choices: closed";
            return;
        }

        if (targetField is not null)
        {
            EditorDialogChoiceSummary = $"Dialog Choices: Target={targetField.Options.Count}, Parameter={parameterField?.Options.Count ?? 0}";
            return;
        }

        EditorDialogChoiceSummary = $"Dialog Choices: TargetLog={targetLogField?.Options.Count ?? 0}";
    }

    private static void RefreshDialogFieldOptions(EditorDialogField field, IEnumerable<string> options, bool selectFirstWhenInvalid)
    {
        var currentValue = field.Value;
        var normalizedOptions = options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        field.Options.Clear();
        foreach (var option in normalizedOptions)
        {
            field.Options.Add(option);
        }

        var hasCurrentValue = normalizedOptions.Contains(currentValue, StringComparer.OrdinalIgnoreCase);
        if (hasCurrentValue)
        {
            var preservedValue = normalizedOptions.First(option => string.Equals(option, currentValue, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(field.Value, preservedValue, StringComparison.Ordinal))
            {
                field.Value = preservedValue;
            }
            return;
        }

        if (selectFirstWhenInvalid)
        {
            field.Value = normalizedOptions.FirstOrDefault() ?? string.Empty;
        }
    }
    private void OnEditorDialogFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isRefreshingEditorDialogFields
            || e.PropertyName != nameof(EditorDialogField.Value)
            || sender is not EditorDialogField field
            || _editorDialogItem is null)
        {
            return;
        }

        if (string.Equals(field.Key, "Name", StringComparison.Ordinal))
        {
            var pathField = FindDialogField("Path");
            if (pathField is not null)
            {
                pathField.Value = BuildPreviewPath(_editorDialogItem, field.Value);
            }

            EditorDialogTitle = _editorDialogMode == EditorDialogMode.Edit
                ? $"Edit {field.Value}"
                : $"Create {_editorDialogItem.Kind}";
        }

        if (string.Equals(field.Key, "TargetPath", StringComparison.Ordinal))
        {
            field.Definition.Apply(_editorDialogItem, field.Value);

            var parameterField = FindDialogField("TargetParameterPath");
            if (parameterField is not null)
            {
                parameterField.Options.Clear();
                foreach (var option in GetTargetParameterOptions(field.Value))
                {
                    parameterField.Options.Add(option);
                }

                if (!parameterField.Options.Contains(parameterField.Value))
                {
                    parameterField.Value = parameterField.Options.FirstOrDefault() ?? string.Empty;
                }
            }

            RefreshEditorDialogFieldValues(_editorDialogItem, "Name", "Path", "Unit", "ControlCaption", "TargetPath", "TargetParameterPath");
            RefreshEditorDialogChoiceOptions(_editorDialogItem);

            var nameField = FindDialogField("Name");
            if (nameField is not null)
            {
                EditorDialogTitle = _editorDialogMode == EditorDialogMode.Edit
                    ? $"Edit {nameField.Value}"
                    : $"Create {_editorDialogItem.Kind}";
            }
        }

        if (string.Equals(field.Key, "TargetParameterFormatKind", StringComparison.Ordinal))
        {
            var parameterField = FindDialogField("TargetParameterFormatParameter");
            if (parameterField is not null)
            {
                if (!FormatUsesParameter(field.Value))
                {
                    parameterField.Value = string.Empty;
                }

                parameterField.ToolTipText = GetFormatParameterToolTip(field.Value);
            }
        }

        if (string.Equals(field.Key, "ButtonCommand", StringComparison.Ordinal))
        {
            var footerField = FindDialogField("Footer");
            if (footerField is not null)
            {
                footerField.Value = GetCommandDescription(field.Value);
            }
        }

        EditorDialogError = string.Empty;
    }

    private void RefreshEditorDialogFieldValues(PageItemModel item, params string[] keys)
    {
        _isRefreshingEditorDialogFields = true;
        try
        {
            foreach (var key in keys)
            {
                var field = FindDialogField(key);
                if (field is null)
                {
                    continue;
                }

                var value = field.Definition.ReadValue(item);
                if (!string.Equals(field.Value, value, StringComparison.Ordinal))
                {
                    field.Value = value;
                }
            }
        }
        finally
        {
            _isRefreshingEditorDialogFields = false;
        }
    }

    private IReadOnlyList<EditorDialogSection> BuildSectionsForItem(PageItemModel item)
    {
        return BuildBindingSectionsForItem(item)
            .Select(sectionBinding =>
            {
                var section = new EditorDialogSection(
                    sectionBinding.Title,
                    isExpanded: string.Equals(sectionBinding.Title, "Identity", StringComparison.Ordinal)
                        || string.Equals(sectionBinding.Title, "Widget", StringComparison.Ordinal)
                        || string.Equals(sectionBinding.Title, "Properties", StringComparison.Ordinal));
                foreach (var binding in sectionBinding.Bindings)
                {
                    section.Fields.Add(binding.CreateField(item));
                }

                return section;
            })
            .ToList();
    }

    private IReadOnlyList<EditorDialogField> BuildActionFieldsForItem(PageItemModel item)
    {
        // Allow Action/InteractionRules for Item, Signal and Button controls
        if (item.Kind is not (ControlKind.Item or ControlKind.Signal or ControlKind.Button))
        {
            return [];
        }

        return
        [
            BindInteractionRuleList(
                "InteractionRules",
                "Interactions",
                current => current.InteractionRules,
                (current, value) =>
                {
                    current.InteractionRules = value;
                    return null;
                },
                _ => "Default without rules: left click opens the value editor or triggers the button action. Event and Action are stored as enum strings in export and import.")
                .CreateField(item)
        ];
    }

    private IReadOnlyList<(string Title, IReadOnlyList<EditorDialogBindingDefinition> Bindings)> BuildBindingSectionsForItem(PageItemModel item)
    {
        var identity = new List<EditorDialogBindingDefinition>
        {
            BindChoice("View", "View", GetViewOptionLabel, ApplyViewOption, GetViewOptions),
            BindText("Name", "Name", current => current.Name, (current, value) => { current.Name = value; return null; }),
            BindReadOnly("Path", "Path", current => current.Path),
            BindReadOnly("Id", "Id", current => current.Id),
            BindChoice("Enabled", "Enabled", current => current.Enabled ? "True" : "False", (current, value) => { current.Enabled = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase); return null; }, _ => new[] { "True", "False" })
        };

        var design = new List<EditorDialogBindingDefinition>
        {
            BindDouble("CornerRadius", "CornerRadius", current => current.CornerRadius, (current, value) => current.CornerRadius = value),
            BindDouble("BorderWidth", "BorderWidth", current => current.BorderWidth, (current, value) => current.BorderWidth = value),
            BindText("BorderColor", "BorderColor", current => current.BorderColor ?? string.Empty, (current, value) => { current.BorderColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
            BindText("BackgroundColor", "BackColor", current => current.BackgroundColor ?? string.Empty, (current, value) => { current.BackgroundColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
            BindText("ToolTipText", "ToolTip", current => current.ToolTipText, (current, value) => { current.ToolTipText = value; return null; }),
        };

        var header = new List<EditorDialogBindingDefinition>
        {
            BindText("ControlCaption", "Caption", current => current.ControlCaption, (current, value) => { current.ControlCaption = value; return null; }),
            BindText("HeaderForeColor", "CaptionForeColor", current => current.HeaderForeColor ?? string.Empty, (current, value) => { current.HeaderForeColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
            BindChoice("CaptionVisible", "CaptionVisible", current => current.CaptionVisible ? "True" : "False", (current, value) => { current.CaptionVisible = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase); return null; }, _ => new[] { "True", "False" }),
            BindDouble("HeaderCornerRadius", "CornerRadius", current => current.HeaderCornerRadius, (current, value) => current.HeaderCornerRadius = value),
            BindDouble("HeaderBorderWidth", "BorderWidth", current => current.HeaderBorderWidth, (current, value) => current.HeaderBorderWidth = value),
            BindText("HeaderBorderColor", "BorderColor", current => current.HeaderBorderColor ?? string.Empty, (current, value) => { current.HeaderBorderColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
            BindText("HeaderBackColor", "BackColor", current => current.HeaderBackColor ?? string.Empty, (current, value) => { current.HeaderBackColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
        };

        var body = new List<EditorDialogBindingDefinition>
        {
            BindText("BodyCaption", "Caption", current => current.BodyCaption, (current, value) => { current.BodyCaption = value; return null; }),
            BindChoice("BodyCaptionPosition", "Position", current => current.BodyCaptionPosition, (current, value) => { current.BodyCaptionPosition = value; return null; }, _ => new[] { "Top", "Left" }),
            BindText("BodyForeColor", "CaptionForeColor", current => current.BodyForeColor ?? string.Empty, (current, value) => { current.BodyForeColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
            BindChoice("BodyCaptionVisible", "CaptionVisible", current => current.BodyCaptionVisible ? "True" : "False", (current, value) => { current.BodyCaptionVisible = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase); return null; }, _ => new[] { "True", "False" }),
            BindDouble("BodyCornerRadius", "CornerRadius", current => current.BodyCornerRadius, (current, value) => current.BodyCornerRadius = value),
            BindDouble("BodyBorderWidth", "BorderWidth", current => current.BodyBorderWidth, (current, value) => current.BodyBorderWidth = value),
            BindText("BodyBorderColor", "BorderColor", current => current.BodyBorderColor ?? string.Empty, (current, value) => { current.BodyBorderColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
            BindText("BodyBackColor", "BackColor", current => current.BodyBackColor ?? string.Empty, (current, value) => { current.BodyBackColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
        };

        var footer = new List<EditorDialogBindingDefinition>
        {
            BindChoice("ShowFooter", "FooterVisible", current => current.ShowFooter ? "True" : "False", (current, value) => { current.ShowFooter = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase); return null; }, _ => new[] { "True", "False" }),
            BindDouble("FooterCornerRadius", "CornerRadius", current => current.FooterCornerRadius, (current, value) => current.FooterCornerRadius = value),
            BindDouble("FooterBorderWidth", "BorderWidth", current => current.FooterBorderWidth, (current, value) => current.FooterBorderWidth = value),
            BindText("FooterBorderColor", "BorderColor", current => current.FooterBorderColor ?? string.Empty, (current, value) => { current.FooterBorderColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
            BindText("FooterBackColor", "BackColor", current => current.FooterBackColor ?? string.Empty, (current, value) => { current.FooterBackColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
        };

        var commonSpecific = new List<EditorDialogBindingDefinition>
        {
            BindText("Unit", "Unit", current => current.Unit, (current, value) => { current.Unit = value; return null; })
        };

        var sections = new List<(string Title, IReadOnlyList<EditorDialogBindingDefinition> Bindings)>
        {
            ("Identity", identity),
            ("Design", design),
            ("Header", header),
            ("Body", body),
            ("Footer", footer)
        };

        switch (item.Kind)
        {
            case ControlKind.Button:
                sections.Add(("Properties", new List<EditorDialogBindingDefinition>
                {
                    BindText("ButtonText", "ButtonText", current => current.ButtonText, (current, value) => { current.ButtonText = value; return null; }),
                    BindText("ButtonIcon", "Icon", current => current.ButtonIcon, (current, value) => { current.ButtonIcon = value; return null; }),
                    BindText("ButtonIconColor", "IconColor", current => current.ButtonIconColor, (current, value) => { current.ButtonIconColor = value; return null; }, EditorPropertyType.Color),
                    BindText("ButtonBackColor", "BackColor", current => current.ButtonBodyBackground, (current, value) => { current.ButtonBodyBackground = value; return null; }, EditorPropertyType.Color),
                    BindChoice("ButtonOnlyIcon", "OnlyIcon", current => current.ButtonOnlyIcon ? "True" : "False", (current, value) => { current.ButtonOnlyIcon = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase); return null; }, _ => new[] { "False", "True" }),
                    BindChoice("ButtonIconAlign", "IconAlign", current => current.ButtonIconAlign, (current, value) => { current.ButtonIconAlign = value; return null; }, _ => AlignmentOptions),
                    BindChoice("ButtonTextAlign", "Align", current => current.ButtonTextAlign, (current, value) => { current.ButtonTextAlign = value; return null; }, _ => AlignmentOptions)
                }));
                break;
            case ControlKind.Item:
            case ControlKind.Signal:
                sections.Add(("Properties", new List<EditorDialogBindingDefinition>(commonSpecific)
                {
                    BindTargetTree("TargetPath", "Target", current => current.TargetPath, (current, value) => { current.ApplyTargetSelection(value); return null; }, current => GetSelectableTargetOptions(current)),
                    BindChoice("TargetParameterPath", "TargetParameter", current => current.TargetParameterPath, (current, value) => { current.TargetParameterPath = value; return null; }, current => GetTargetParameterOptions(current.TargetPath)),
                    BindChoice("TargetParameterFormatKind", "Format", current => SplitParameterFormat(current.TargetParameterFormat).Kind, (current, value) => { current.TargetParameterFormat = ComposeParameterFormat(value, SplitParameterFormat(current.TargetParameterFormat).Parameter); return null; }, _ => ParameterFormatOptions),
                    BindText("TargetParameterFormatParameter", "FormatParameter", current => SplitParameterFormat(current.TargetParameterFormat).Parameter, (current, value) => { current.TargetParameterFormat = ComposeParameterFormat(SplitParameterFormat(current.TargetParameterFormat).Kind, value); return null; }, EditorPropertyType.Text, GetFormatParameterToolTip),
                    BindChoice("IsReadOnly", "Readonly", current => current.IsReadOnly ? "True" : "False", (current, value) => { current.IsReadOnly = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase); return null; }, _ => new[] { "False", "True" }),
                    BindInt("RefreshRateMs", "RefreshRate ms", current => current.RefreshRateMs, (current, value) => current.RefreshRateMs = value)
                }));
                break;
            case ControlKind.ChartControl:
                sections.Add(("Properties", new List<EditorDialogBindingDefinition>
                {
                    BindChartSeriesList("ChartSeriesDefinitions", "Series", current => current.ChartSeriesDefinitions, (current, value) => { current.ChartSeriesDefinitions = value; return null; }, GetChartSeriesToolTip),
                    BindInt("RefreshRateMs", "RefreshRate ms", current => current.RefreshRateMs, (current, value) => current.RefreshRateMs = value),
                    BindInt("HistorySeconds", "History s", current => current.HistorySeconds, (current, value) => current.HistorySeconds = value),
                    BindInt("ViewSeconds", "View s", current => current.ViewSeconds, (current, value) => current.ViewSeconds = value)
                }));
                break;
            case ControlKind.ListControl:
                sections.Add(("Properties", new List<EditorDialogBindingDefinition>
                {
                    BindDouble("ControlHeight", "ControlHeight", current => current.ControlHeight, (current, value) => current.ControlHeight = value)
                }));
                break;
            case ControlKind.TableControl:
                sections.Add(("Properties", new List<EditorDialogBindingDefinition>
                {
                    BindInt("Rows", "Rows", current => current.TableRows, (current, value) => current.TableRows = value),
                    BindInt("Columns", "Columns", current => current.TableColumns, (current, value) => current.TableColumns = value)
                }));
                break;
            case ControlKind.LogControl:
                sections.Add(("Properties", new List<EditorDialogBindingDefinition>
                {
                    BindChoice("TargetLog", "TargetLog", current => current.TargetLog, (current, value) => { current.TargetLog = value; return null; }, _ => GetProcessLogTargetOptions())
                }));
                break;
            case ControlKind.UdlClientControl:
                sections.Add(("Properties", new List<EditorDialogBindingDefinition>
                {
                    BindText("UdlClientHost", "Host", current => current.UdlClientHost, (current, value) => { current.UdlClientHost = value; return null; }),
                    BindInt("UdlClientPort", "Port", current => current.UdlClientPort, (current, value) => current.UdlClientPort = value),
                    BindChoice("UdlClientAutoConnect", "AutoConnect", current => current.UdlClientAutoConnect ? "True" : "False", (current, value) => { current.UdlClientAutoConnect = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase); return null; }, _ => new[] { "False", "True" }),
                    BindChoice("UdlClientDebugLogging", "DebugLogging", current => current.UdlClientDebugLogging ? "True" : "False", (current, value) => { current.UdlClientDebugLogging = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase); return null; }, _ => new[] { "False", "True" }),
                    BindAttachItemList("UdlAttachedItemPaths", "AttachToUi", current => current.UdlAttachedItemPaths, (current, value) => { current.UdlAttachedItemPaths = value; return null; }, GetUdlAttachItemOptions)
                }));
                break;
        }

        var controlIndex = sections.FindIndex(s => string.Equals(s.Title, "Properties", StringComparison.Ordinal));
        if (controlIndex > 1)
        {
            var controlSection = sections[controlIndex];
            sections.RemoveAt(controlIndex);
            sections.Insert(1, controlSection);
        }

        return sections;
    }

    private static EditorDialogBindingDefinition BindReadOnly(string key, string label, Func<PageItemModel, string> read)
        => new(key, label, EditorPropertyType.Text, read, isReadOnly: true);

    private static EditorDialogBindingDefinition BindText(string key, string label, Func<PageItemModel, string> read, Func<PageItemModel, string, string?> apply, EditorPropertyType propertyType = EditorPropertyType.Text, Func<PageItemModel, string>? toolTipFactory = null)
        => new(key, label, propertyType, read, apply, toolTipFactory: toolTipFactory);

    private static EditorDialogBindingDefinition BindMultiline(string key, string label, Func<PageItemModel, string> read, Func<PageItemModel, string, string?> apply, Func<PageItemModel, string>? toolTipFactory = null)
        => new(key, label, EditorPropertyType.MultilineText, read, apply, toolTipFactory: toolTipFactory);

    private static EditorDialogBindingDefinition BindChartSeriesList(string key, string label, Func<PageItemModel, string> read, Func<PageItemModel, string, string?> apply, Func<PageItemModel, string>? toolTipFactory = null)
        => new(key, label, EditorPropertyType.ChartSeriesList, read, apply, toolTipFactory: toolTipFactory);

    private static EditorDialogBindingDefinition BindAttachItemList(string key, string label, Func<PageItemModel, string> read, Func<PageItemModel, string, string?> apply, Func<PageItemModel, IEnumerable<string>> optionsFactory)
        => new(key, label, EditorPropertyType.AttachItemList, read, apply, optionsFactory: optionsFactory);

    private static EditorDialogBindingDefinition BindInteractionRuleList(string key, string label, Func<PageItemModel, string> read, Func<PageItemModel, string, string?> apply, Func<PageItemModel, string>? toolTipFactory = null)
        => new(key, label, EditorPropertyType.InteractionRuleList, read, apply, toolTipFactory: toolTipFactory);

    private static EditorDialogBindingDefinition BindTargetTree(string key, string label, Func<PageItemModel, string> read, Func<PageItemModel, string, string?> apply, Func<PageItemModel, IEnumerable<string>> optionsFactory)
        => new(key, label, EditorPropertyType.TargetTree, read, apply, optionsFactory: optionsFactory);

    private static EditorDialogBindingDefinition BindChoice(string key, string label, Func<PageItemModel, string> read, Func<PageItemModel, string, string?> apply, Func<PageItemModel, IEnumerable<string>> optionsFactory)
        => new(key, label, EditorPropertyType.Choice, read, apply, optionsFactory: optionsFactory);

    private IEnumerable<string> GetViewOptions(PageItemModel item)
    {
        var page = FindOwningPage(item) ?? SelectedPage;
        var options = page.Views.Count > 0
            ? page.Views.OrderBy(static entry => entry.Key).Select(static entry => FormatViewOption(entry.Key, entry.Value)).ToList()
            : new List<string>();

        var currentOption = GetViewOptionLabel(item);
        if (!options.Contains(currentOption, StringComparer.Ordinal))
        {
            options.Add(currentOption);
        }

        return options;
    }

    private string GetViewOptionLabel(PageItemModel item)
    {
        var page = FindOwningPage(item) ?? SelectedPage;
        page.Views.TryGetValue(item.View, out var caption);
        return FormatViewOption(item.View, caption);
    }

    private string? ApplyViewOption(PageItemModel item, string raw)
    {
        if (!TryParseViewOption(raw, item, out var viewId))
        {
            return "Invalid view selection.";
        }

        item.View = viewId;
        return null;
    }

    private bool TryParseViewOption(string? raw, PageItemModel item, out int viewId)
    {
        viewId = 1;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (int.TryParse(trimmed, out viewId))
        {
            return viewId > 0;
        }

        if (trimmed.StartsWith("View ", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = trimmed.Substring(5);
            var separatorIndex = suffix.IndexOf(" - ", StringComparison.Ordinal);
            var numberText = separatorIndex >= 0 ? suffix[..separatorIndex] : suffix;
            if (int.TryParse(numberText, out viewId))
            {
                return viewId > 0;
            }
        }

        var page = FindOwningPage(item) ?? SelectedPage;
        var match = page.Views.FirstOrDefault(entry => string.Equals(entry.Value, trimmed, StringComparison.Ordinal));
        if (match.Key > 0)
        {
            viewId = match.Key;
            return true;
        }

        return false;
    }

    private static string FormatViewOption(int viewId, string? caption)
    {
        var safeViewId = viewId <= 0 ? 1 : viewId;
        return string.IsNullOrWhiteSpace(caption)
            ? $"View {safeViewId}"
            : $"View {safeViewId} - {caption}";
    }

    private static EditorDialogBindingDefinition BindDouble(string key, string label, Func<PageItemModel, double> read, Action<PageItemModel, double> apply)
        => new(key, label, EditorPropertyType.Double, current => read(current).ToString("0.##"), (current, raw) => TryApplyDouble(raw, current, apply));

    private static EditorDialogBindingDefinition BindInt(string key, string label, Func<PageItemModel, int> read, Action<PageItemModel, int> apply)
        => new(key, label, EditorPropertyType.Integer, current => read(current).ToString(), (current, raw) => TryApplyInt(raw, current, apply));

    private static string? TryApplyDouble(string raw, PageItemModel item, Action<PageItemModel, double> apply)
    {
        if (!TryParseDouble(raw, out var value, out var error))
        {
            return error;
        }

        apply(item, value);
        return null;
    }

    private static string? TryApplyInt(string raw, PageItemModel item, Action<PageItemModel, int> apply)
    {
        if (!TryParseInt(raw, out var value, out var error))
        {
            return error;
        }

        apply(item, value);
        return null;
    }

    private bool TryApplyEditorDialogValues(PageItemModel item, PageModel page, PageItemModel? excludeItem, out string error)
    {
        var nameField = FindDialogField("Name");
        if (nameField is null)
        {
            error = "Name-Feld fehlt.";
            return false;
        }

        if (!TryValidateControlName(nameField.Value, page, excludeItem, out var normalizedName, out error))
        {
            return false;
        }

        foreach (var field in EnumerateEditorDialogFields())
        {
            if (field.IsReadOnly)
            {
                continue;
            }

            var valueToApply = string.Equals(field.Key, "Name", StringComparison.Ordinal) ? normalizedName : field.Value;
            var applyError = field.Definition.Apply(item, valueToApply);
            if (!string.IsNullOrWhiteSpace(applyError))
            {
                error = applyError!;
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private EditorDialogField? FindDialogField(string key)
        => EnumerateEditorDialogFields().FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.Ordinal));

    private string BuildPreviewPath(PageItemModel item, string proposedName)
    {
        var segments = new List<string> { item.PageName };
        if (item.ParentItem is not null && !string.IsNullOrWhiteSpace(item.ParentItem.Name))
        {
            segments.Add(item.ParentItem.Name);
        }

        if (!string.IsNullOrWhiteSpace(proposedName))
        {
            segments.Add(proposedName.Trim());
        }

        return string.Join(".", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
    }

    private void ResetEditorDialog()
    {
        ResetEditorDialogSubscriptions();
        EditorDialogSections.Clear();
        EditorDialogActionFields.Clear();
        _editorDialogMode = EditorDialogMode.None;
        _editorDialogItem = null;
        _editorDialogParentItem = null;
        EditorDialogTitle = string.Empty;
        EditorDialogError = string.Empty;
        IsEditorDialogOpen = false;
        EditorDialogChoiceSummary = "Dialog Choices: closed";
        OnPropertyChanged(nameof(HasEditorDialogActionFields));
        OnPropertyChanged(nameof(ShowEditorDialogActionPlaceholder));
    }

    private IEnumerable<EditorDialogField> EnumerateEditorDialogFields()
        => EditorDialogSections.SelectMany(section => section.Fields).Concat(EditorDialogActionFields);

    private void SetSelectedItems(IReadOnlyList<PageItemModel> items, PageItemModel? primaryItem)
    {
        _selectedItems.Clear();
        foreach (var item in items.Distinct())
        {
            _selectedItems.Add(item);
        }

        UpdateSelectionFlags(primaryItem is not null && _selectedItems.Contains(primaryItem) ? primaryItem : _selectedItems.LastOrDefault());
    }

    private void UpdateSelectionFlags(PageItemModel? masterItem)
    {
        foreach (var item in EnumeratePageItems(SelectedPage.Items))
        {
            item.IsSelected = false;
            item.IsMasterSelected = false;
        }

        foreach (var item in _selectedItems)
        {
            item.IsSelected = true;
        }

        SelectedItem = masterItem;
        if (SelectedItem is not null)
        {
            SelectedItem.IsMasterSelected = true;
        }

        OnPropertyChanged(nameof(SelectedItemsCount));
        OnPropertyChanged(nameof(HasMultiSelection));
        OnPropertyChanged(nameof(ShowAlignmentPanel));
        OnPropertyChanged(nameof(FooterText));
    }

    private void ApplyThemeToAllItems()
    {
        foreach (var page in Pages)
        {
            foreach (var item in page.Items)
            {
                ApplyThemeRecursive(item);
            }
        }
    }

    private void ApplyThemeRecursive(PageItemModel item)
    {
        item.ApplyTheme(IsDarkTheme);
        foreach (var child in item.Items)
        {
            ApplyThemeRecursive(child);
        }
    }

    private void RefreshGridLines()
    {
        GridLines.Clear();
        if (!ShowGrid || _canvasWidth <= 0 || _canvasHeight <= 0)
        {
            return;
        }

        for (var x = GridSize; x < _canvasWidth; x += GridSize)
        {
            GridLines.Add(new EditorGridLine { X = x, Y = 0, Width = 1, Height = _canvasHeight });
        }

        for (var y = GridSize; y < _canvasHeight; y += GridSize)
        {
            GridLines.Add(new EditorGridLine { X = 0, Y = y, Width = _canvasWidth, Height = 1 });
        }
    }

    private void NormalizeListChild(PageItemModel listControl, PageItemModel item)
    {
        item.X = 0;
        item.Y = 0;
        listControl.ApplyListControlDefaultsToChild(item);
    }

    private void AttachHierarchy(PageModel page)
    {
        foreach (var item in page.Items)
        {
            AttachHierarchy(page.Name, null, item);
        }
    }

    private static void AttachHierarchy(string pageName, PageItemModel? parentItem, PageItemModel item)
    {
        item.SetHierarchy(pageName, parentItem, parentItem?.ActiveViewId ?? 1);
        foreach (var child in item.Items)
        {
            AttachHierarchy(pageName, item, child);
        }
    }

    private static IEnumerable<PageItemModel> EnumeratePageItems(IEnumerable<PageItemModel> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in EnumeratePageItems(item.Items))
            {
                yield return child;
            }
        }
    }

    private double MaxAvailableWidth(double x) => Math.Max(150, _canvasWidth - x);
    private double MaxAvailableHeight(double y) => Math.Max(72, _canvasHeight - y);

    private static bool IntersectsSelection(PageItemModel item, double selectionX, double selectionY, double selectionWidth, double selectionHeight)
    {
        if (!item.IsVisibleInActiveView)
        {
            return false;
        }

        var selectionRight = selectionX + selectionWidth;
        var selectionBottom = selectionY + selectionHeight;
        var itemRight = item.X + item.Width;
        var itemBottom = item.Y + item.Height;
        return item.X < selectionRight && itemRight > selectionX && item.Y < selectionBottom && itemBottom > selectionY;
    }

    private static PageDocument ToDocument(PageModel page)
    {
        return new PageDocument
        {
            Index = page.Index,
            Name = page.Name,
            Items = page.Items.Select(ToDocument).ToList()
        };
    }

    private static PageItemDocument ToDocument(PageItemModel item)
    {
        return new PageItemDocument
        {
            Kind = item.Kind,
            Name = item.Name,
            Id = item.Id,
            ControlCaption = item.ControlCaption,
            ShowCaption = item.ShowCaption,
            CaptionVisible = item.CaptionVisible,
            BodyCaption = item.BodyCaption,
            BodyCaptionPosition = item.BodyCaptionPosition,
            ShowBodyCaption = item.ShowBodyCaption,
            BodyCaptionVisible = item.BodyCaptionVisible,
            ShowFooter = item.ShowFooter,
            Header = item.Header,
            Title = item.Title,
            Footer = item.Footer,
            HeaderForeColor = item.HeaderForeColor,
            HeaderBackColor = item.HeaderBackColor,
            HeaderBorderColor = item.HeaderBorderColor,
            HeaderBorderWidth = item.HeaderBorderWidth,
            HeaderCornerRadius = item.HeaderCornerRadius,
            BodyForeColor = item.BodyForeColor,
            BodyBackColor = item.BodyBackColor,
            BodyBorderColor = item.BodyBorderColor,
            BodyBorderWidth = item.BodyBorderWidth,
            BodyCornerRadius = item.BodyCornerRadius,
            FooterForeColor = item.FooterForeColor,
            FooterBackColor = item.FooterBackColor,
            FooterBorderColor = item.FooterBorderColor,
            FooterBorderWidth = item.FooterBorderWidth,
            FooterCornerRadius = item.FooterCornerRadius,
            ToolTipText = item.ToolTipText,
            ButtonText = item.ButtonText,
            ButtonIcon = item.ButtonIcon,
            ButtonOnlyIcon = item.ButtonOnlyIcon,
            ButtonIconAlign = item.ButtonIconAlign,
            ButtonTextAlign = item.ButtonTextAlign,
            ButtonCommand = item.ButtonCommand,
            ButtonBodyBackground = item.ButtonBodyBackground,
            ButtonBodyForegroundColor = item.ButtonBodyForegroundColor,
            ButtonIconColor = item.ButtonIconColor,
            UseThemeColor = item.UseThemeColor,
            BackgroundColor = item.BackgroundColor,
            BorderColor = item.BorderColor,
            ContainerBorder = item.ContainerBorder,
            ContainerBackgroundColor = item.ContainerBackgroundColor,
            ContainerBorderWidth = item.ContainerBorderWidth,
            BorderWidth = item.BorderWidth,
            CornerRadius = item.CornerRadius,
            PrimaryForegroundColor = item.PrimaryForegroundColor,
            SecondaryForegroundColor = item.SecondaryForegroundColor,
            AccentBackgroundColor = item.AccentBackgroundColor,
            AccentForegroundColor = item.AccentForegroundColor,
            TargetPath = TargetPathHelper.ToPersistedLayoutTargetPath(item.TargetPath),
            TargetParameterPath = item.TargetParameterPath,
            TargetParameterFormat = item.TargetParameterFormat,
            Unit = item.Unit,
            TargetLog = item.TargetLog,
            RefreshRateMs = item.RefreshRateMs,
            HistorySeconds = item.HistorySeconds,
            ViewSeconds = item.ViewSeconds,
            ChartSeriesDefinitions = TargetPathHelper.ToPersistedChartSeriesDefinitions(item.ChartSeriesDefinitions),
            InteractionRules = ToInteractionRuleDocuments(item.InteractionRules),
            UdlClientHost = item.UdlClientHost,
            UdlClientPort = item.UdlClientPort,
            UdlClientAutoConnect = item.UdlClientAutoConnect,
            UdlClientDebugLogging = item.UdlClientDebugLogging,
            UdlAttachedItemPaths = item.UdlAttachedItemPaths,
            IsReadOnly = item.IsReadOnly,
            IsAutoHeight = item.IsAutoHeight,
            ListItemHeight = item.ListItemHeight,
            ControlHeight = item.ControlHeight,
            ControlBorderWidth = item.ControlBorderWidth,
            ControlBorderColor = item.ControlBorderColor,
            ControlCornerRadius = item.ControlCornerRadius,
            X = item.X,
            Y = item.Y,
            Width = item.Width,
            Height = item.Height,
            Items = item.Items.Select(ToDocument).ToList()
        };
    }

    private static PageModel ToModel(PageDocument page)
    {
        var model = new PageModel { Index = page.Index, Name = page.Name };
        foreach (var item in page.Items)
        {
            model.Items.Add(ToModel(item));
        }

        return model;
    }

    private static PageItemModel ToModel(PageItemDocument item)
    {
        var model = new PageItemModel
        {
            Kind = item.Kind == ControlKind.Signal ? ControlKind.Item : item.Kind,
            Name = item.Name,
            Id = item.Id,
            ControlCaption = string.IsNullOrWhiteSpace(item.ControlCaption) ? item.Header : item.ControlCaption,
            ShowCaption = item.ShowCaption,
            CaptionVisible = item.ShowCaption,
            BodyCaption = string.IsNullOrWhiteSpace(item.BodyCaption) ? item.Title : item.BodyCaption,
            BodyCaptionPosition = item.BodyCaptionPosition,
            ShowBodyCaption = item.ShowBodyCaption,
            BodyCaptionVisible = item.ShowBodyCaption,
            ShowFooter = item.ShowFooter,
            Footer = item.Footer,
            HeaderForeColor = item.HeaderForeColor,
            HeaderBackColor = item.HeaderBackColor,
            HeaderBorderColor = item.HeaderBorderColor,
            HeaderBorderWidth = item.HeaderBorderWidth,
            HeaderCornerRadius = item.HeaderCornerRadius,
            BodyForeColor = item.BodyForeColor,
            BodyBackColor = item.BodyBackColor,
            BodyBorderColor = item.BodyBorderColor,
            BodyBorderWidth = item.BodyBorderWidth,
            BodyCornerRadius = item.BodyCornerRadius,
            FooterForeColor = item.FooterForeColor,
            FooterBackColor = item.FooterBackColor,
            FooterBorderColor = item.FooterBorderColor,
            FooterBorderWidth = item.FooterBorderWidth,
            FooterCornerRadius = item.FooterCornerRadius,
            ToolTipText = item.ToolTipText,
            ButtonText = item.ButtonText,
            ButtonIcon = item.ButtonIcon,
            ButtonOnlyIcon = item.ButtonOnlyIcon,
            ButtonIconAlign = item.ButtonIconAlign,
            ButtonTextAlign = item.ButtonTextAlign,
            ButtonCommand = item.ButtonCommand,
            ButtonBodyBackground = item.ButtonBodyBackground,
            ButtonBodyForegroundColor = item.ButtonBodyForegroundColor,
            ButtonIconColor = item.ButtonIconColor,
            UseThemeColor = item.UseThemeColor,
            BackgroundColor = item.BackgroundColor,
            BorderColor = item.BorderColor,
            ContainerBorder = item.ContainerBorder,
            ContainerBackgroundColor = item.ContainerBackgroundColor,
            ContainerBorderWidth = item.ContainerBorderWidth,
            BorderWidth = item.BorderWidth,
            CornerRadius = item.CornerRadius,
            PrimaryForegroundColor = item.PrimaryForegroundColor,
            SecondaryForegroundColor = item.SecondaryForegroundColor,
            AccentBackgroundColor = item.AccentBackgroundColor,
            AccentForegroundColor = item.AccentForegroundColor,
            TargetPath = TargetPathHelper.NormalizeConfiguredTargetPath(item.TargetPath),
            TargetParameterPath = item.TargetParameterPath,
            TargetParameterFormat = item.TargetParameterFormat,
            Unit = item.Unit,
            TargetLog = item.TargetLog,
            RefreshRateMs = item.RefreshRateMs,
            HistorySeconds = item.HistorySeconds,
            ViewSeconds = item.ViewSeconds,
            ChartSeriesDefinitions = TargetPathHelper.NormalizeChartSeriesDefinitions(item.ChartSeriesDefinitions),
            InteractionRules = FromInteractionRuleDocuments(item.InteractionRules),
            UdlClientHost = item.UdlClientHost,
            UdlClientPort = item.UdlClientPort,
            UdlClientAutoConnect = item.UdlClientAutoConnect,
            UdlClientDebugLogging = item.UdlClientDebugLogging,
            UdlAttachedItemPaths = item.UdlAttachedItemPaths,
            IsReadOnly = item.IsReadOnly,
            IsAutoHeight = item.IsAutoHeight,
            ListItemHeight = item.ControlHeight > 0 ? item.ControlHeight : item.ListItemHeight,
            ControlBorderWidth = item.ControlBorderWidth,
            ControlBorderColor = item.ControlBorderColor,
            ControlCornerRadius = item.ControlCornerRadius,
            X = item.X,
            Y = item.Y,
            Width = Math.Max(item.Width, item.Kind switch { ControlKind.Button => 140, ControlKind.Signal => 150, ControlKind.Item => 150, ControlKind.ListControl => 240, ControlKind.LogControl => 320, ControlKind.ChartControl => 360, _ => 140 }),
            Height = Math.Max(item.Height, item.Kind switch { ControlKind.Button => 56, ControlKind.Signal => 72, ControlKind.Item => 72, ControlKind.ListControl => 180, ControlKind.TableControl => 180, ControlKind.LogControl => 220, ControlKind.ChartControl => 220, _ => 72 })
        };

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            model.Name = item.Kind switch { ControlKind.Button => "Button", ControlKind.ListControl => "ListControl", ControlKind.TableControl => "TableControl", ControlKind.LogControl => "LogControl", ControlKind.ChartControl => "ChartControl", _ => "Item" };
        }

        if (model.Kind == ControlKind.LogControl
            && string.IsNullOrWhiteSpace(model.ControlCaption)
            && !string.IsNullOrWhiteSpace(model.BodyCaption))
        {
            model.ControlCaption = model.BodyCaption;
            model.BodyCaption = string.Empty;
        }

        foreach (var child in item.Items)
        {
            model.Items.Add(ToModel(child));
        }

        model.ApplyTargetSelection(model.TargetPath);
        model.SyncChildWidths();
        model.ApplyListHeightRules();
        return model;
    }

    private static List<ItemInteractionRuleDocument> ToInteractionRuleDocuments(string definitions)
        => ItemInteractionRuleCodec.ParseDefinitions(definitions)
            .Select(static rule => new ItemInteractionRuleDocument
            {
                Event = rule.Event,
                Action = rule.Action,
                TargetPath = TargetPathHelper.ToPersistedLayoutTargetPath(rule.TargetPath),
                Argument = rule.Argument
            })
            .ToList();

    private static string FromInteractionRuleDocuments(IEnumerable<ItemInteractionRuleDocument>? documents)
        => ItemInteractionRuleCodec.SerializeDefinitions((documents ?? []).Select(static rule => new ItemInteractionRule
        {
            Event = rule.Event,
            Action = rule.Action,
            TargetPath = TargetPathHelper.NormalizeConfiguredTargetPath(rule.TargetPath),
            Argument = rule.Argument
        }));

    internal static (string Kind, string Parameter) SplitParameterFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return ("Text", string.Empty);
        }

        var trimmed = format.Trim();
        if (trimmed.StartsWith("numeric:", StringComparison.OrdinalIgnoreCase))
        {
            return ("Numeric", trimmed[8..].Trim());
        }

        if (string.Equals(trimmed, "numeric", StringComparison.OrdinalIgnoreCase))
        {
            return ("Numeric", "0.##");
        }

        if (trimmed.StartsWith("hex:", StringComparison.OrdinalIgnoreCase))
        {
            return ("Hex", trimmed[4..].Trim());
        }

        if (string.Equals(trimmed, "hex", StringComparison.OrdinalIgnoreCase))
        {
            return ("Hex", string.Empty);
        }

        if (trimmed.StartsWith("h", StringComparison.OrdinalIgnoreCase) && trimmed.Length >= 2)
        {
            return ("Hex", trimmed[1..]);
        }

        if (trimmed.StartsWith("D", StringComparison.Ordinal) && trimmed.Length >= 2 && int.TryParse(trimmed[1..], out var decPrecision))
        {
            return ("Numeric", new string('0', decPrecision));
        }

        if (trimmed.StartsWith("F", StringComparison.Ordinal) && trimmed.Length >= 2 && int.TryParse(trimmed[1..], out var floatPrecision))
        {
            var decimals = new string('0', floatPrecision);
            return ("Numeric", floatPrecision > 0 ? $"0.{decimals}" : "0");
        }

        var parts = trimmed.Split(':', 2, StringSplitOptions.TrimEntries);
        var kind = string.IsNullOrWhiteSpace(parts[0]) ? "Text" : parts[0];
        var parameter = parts.Length > 1 ? parts[1] : string.Empty;
        return (kind, parameter);
    }

    private static string ComposeParameterFormat(string? kind, string? parameter)
    {
        var normalizedKind = string.IsNullOrWhiteSpace(kind) ? "Text" : kind.Trim();
        var normalizedParameter = string.IsNullOrWhiteSpace(parameter) ? string.Empty : parameter.Trim();

        if (string.Equals(normalizedKind, "Text", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (string.Equals(normalizedKind, "Numeric", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(normalizedParameter) ? "numeric" : $"numeric:{normalizedParameter}";
        }

        if (string.Equals(normalizedKind, "Hex", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(normalizedParameter) ? "hex" : $"hex:{normalizedParameter}";
        }

        return FormatUsesParameter(normalizedKind) && !string.IsNullOrWhiteSpace(normalizedParameter)
            ? $"{normalizedKind}:{normalizedParameter}"
            : normalizedKind;
    }

    private static bool FormatUsesParameter(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        return string.Equals(kind, "Numeric", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "Hex", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "bool", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "b4", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "b8", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "b16", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFormatParameterToolTip(PageItemModel item)
        => GetFormatParameterToolTip(SplitParameterFormat(item.TargetParameterFormat).Kind);

    private static string GetChartSeriesToolTip(PageItemModel item)
        => string.Empty;

    private static string GetFormatParameterToolTip(string? kind)
    {
        if (string.Equals(kind, "bool", StringComparison.OrdinalIgnoreCase))
        {
            return "Optionale Labels fuer true,false. Beispiel: AN,AUS";
        }

        if (string.Equals(kind, "b4", StringComparison.OrdinalIgnoreCase))
        {
            return "Optionale Bit-Labels fuer 4 Bits. Beispiel: DI1,DI2,Alert,4";
        }

        if (string.Equals(kind, "b8", StringComparison.OrdinalIgnoreCase))
        {
            return "Optionale Bit-Labels fuer 8 Bits. Beispiel: DI1,DI2,Alert,4,5,6,7,8";
        }

        if (string.Equals(kind, "b16", StringComparison.OrdinalIgnoreCase))
        {
            return "Optionale Bit-Labels fuer 16 Bits. Beispiel: 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16";
        }

        if (string.Equals(kind, "Numeric", StringComparison.OrdinalIgnoreCase))
        {
            return "Zahlenformat als Muster. Beispiel: 0 | 0.00 | 000.000";
        }

        if (string.Equals(kind, "Hex", StringComparison.OrdinalIgnoreCase))
        {
            return "Optionale Stellenzahl ohne 0x. Beispiel: 2 | 4 | 8";
        }

        if (string.Equals(kind, "Text", StringComparison.OrdinalIgnoreCase))
        {
            return "Kein Zusatzparameter. Wert wird direkt angezeigt.";
        }

        return "Optionaler Parameter fuer das ausgewaehlte Format.";
    }

    private static IEnumerable<string> GetProcessLogTargetOptions()
    {
        return HostRegistries.Data.GetAllKeys()
            .Select(key => HostRegistries.Data.TryGet(key, out var item) ? (Key: key, Item: item) : (Key: (string?)null, Item: null))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Item?.Value is ProcessLog)
            .Select(entry => entry.Key!)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> GetCommandRegistryOptions()
    {
        return HostRegistries.Commands.GetAll()
            .Select(command => command.Name)
            .Prepend(string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetCommandDescription(string? commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return string.Empty;
        }

        return HostRegistries.Commands.TryGet(commandName, out var command) && command is not null
            ? command.Description ?? string.Empty
            : string.Empty;
    }

    private static IEnumerable<string> GetTargetParameterOptions(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return [];
        }

        if (!TryResolveDataItem(targetPath, out var item) || item is null)
        {
            return [];
        }

        return item.Params.GetDictionary().Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? EmptyToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseDouble(string text, out double value, out string error)
    {
        if (double.TryParse(text, out value))
        {
            error = string.Empty;
            return true;
        }

        error = $"Ungueltiger Zahlenwert: {text}";
        return false;
    }

    private static bool TryParseInt(string text, out int value, out string error)
    {
        if (int.TryParse(text, out value))
        {
            error = string.Empty;
            return true;
        }

        error = $"Ungueltiger Integer-Wert: {text}";
        return false;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (max < min)
        {
            max = min;
        }

        return Math.Max(min, Math.Min(max, value));
    }

    private string GetSuggestedControlName(ControlKind kind, PageModel page, PageItemModel? parentItem, PageItemModel? excludeItem)
    {
        var baseName = kind switch { ControlKind.Button => "Button", ControlKind.ListControl => "ListControl", ControlKind.TableControl => "TableControl", ControlKind.LogControl => "LogControl", ControlKind.ChartControl => "ChartControl", ControlKind.UdlClientControl => "UdlClientControl", _ => "Item" };
        var candidate = baseName;
        var index = 1;
        while (!IsControlNameUnique(page, candidate, excludeItem))
        {
            index++;
            candidate = $"{baseName}{index}";
        }

        return candidate;
    }

    private static string NormalizeControlName(string? name)
        => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();

    private bool TryValidateControlName(string? proposedName, PageModel page, PageItemModel? excludeItem, out string normalizedName, out string error)
    {
        normalizedName = NormalizeControlName(proposedName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            error = "Name darf nicht leer sein.";
            return false;
        }

        if (!IsControlNameUnique(page, normalizedName, excludeItem))
        {
            error = $"Name '{normalizedName}' ist auf {page.Name} bereits vergeben.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool IsControlNameUnique(PageModel page, string name, PageItemModel? excludeItem)
    {
        return !EnumeratePageItems(page.Items)
            .Where(item => !ReferenceEquals(item, excludeItem))
            .Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetUdlAttachItemOptions(PageItemModel item)
    {
        var normalizedName = NormalizeControlName(item.Name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return [];
        }

        var prefix = $"Runtime/UdlClient/{normalizedName}/";
        return HostRegistries.Data.GetAllKeys()
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(key => key[prefix.Length..])
            .Where(static key => IsRootAttachPath(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsRootAttachPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/').Trim('/');
        return !normalized.Contains('/');
    }

    private static IEnumerable<string> GetFooterSubItemOptions(PageItemModel item)
    {
        if (string.IsNullOrWhiteSpace(item.TargetPath) || !TryResolveDataItem(item.TargetPath, out var targetItem) || targetItem is null)
        {
            return [];
        }

        return EnumerateRelativeChildItemPaths(targetItem)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string GetFooterSubItemSelection(PageItemModel item)
    {
        if (string.IsNullOrWhiteSpace(item.TargetPath) || item.Items.Count == 0)
        {
            return string.Empty;
        }

        var selected = item.Items
            .Select(child => GetRelativeTargetPath(item.TargetPath, child.TargetPath))
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.Join(Environment.NewLine, selected!);
    }

    private string? ApplyFooterSubItems(PageItemModel item, string value)
    {
        if (string.IsNullOrWhiteSpace(item.TargetPath) || !TryResolveDataItem(item.TargetPath, out var targetRoot) || targetRoot is null)
        {
            item.Items.Clear();
            return null;
        }

        var selectedPaths = value
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (selectedPaths.Length == 0)
        {
            item.Items.Clear();
            return null;
        }

        var existingByRelativePath = item.Items
            .Select(child => (Child: child, RelativePath: GetRelativeTargetPath(item.TargetPath, child.TargetPath)))
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.RelativePath))
            .GroupBy(entry => entry.RelativePath!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Child, StringComparer.OrdinalIgnoreCase);

        var syncedChildren = new List<PageItemModel>();
        foreach (var relativePath in selectedPaths)
        {
            if (!TryResolveRelativeChild(targetRoot, relativePath, out var resolvedTarget) || resolvedTarget is null)
            {
                continue;
            }

            var isNew = !existingByRelativePath.TryGetValue(relativePath, out var childItem);
            childItem ??= CreateFooterSubItem(item);
            ConfigureFooterSubItem(item, childItem, relativePath, resolvedTarget, isNew);
            syncedChildren.Add(childItem);
        }

        item.Items.Clear();
        foreach (var child in syncedChildren)
        {
            item.Items.Add(child);
        }

        item.SyncFooterSubItemLayout();
        EnsureFooterSubItemHostHeight(item);
        return null;
    }

    private PageItemModel CreateFooterSubItem(PageItemModel parentItem)
    {
        var item = new PageItemModel
        {
            Kind = ControlKind.Item,
            ControlCaption = string.Empty,
            BodyCaption = string.Empty,
            Unit = string.Empty,
            CaptionVisible = false,
            ShowFooter = false,
            BodyCaptionVisible = true,
            BodyCaptionPosition = "Left",
            BodyCornerRadius = 8,
            Width = System.Math.Max(parentItem.FooterSubItemWidth, 50),
            Height = System.Math.Max(parentItem.FooterSubItemHeight, 1)
        };

        item.ApplyTheme(IsDarkTheme);
        return item;
    }

    private void ConfigureFooterSubItem(PageItemModel parentItem, PageItemModel childItem, string relativePath, Item targetItem, bool isNew)
    {
        var previousBodyCaption = childItem.BodyCaption;
        childItem.ApplyTargetSelection(targetItem.Path ?? string.Empty);

        if (isNew)
        {
            var relativeName = relativePath.Replace('/', '.');
            childItem.Name = string.IsNullOrWhiteSpace(parentItem.Name)
                ? relativeName
                : $"{parentItem.Name}.{relativeName}";
        }

        childItem.CaptionVisible = false;
        childItem.ShowFooter = false;
        childItem.BodyCaptionVisible = true;
        childItem.BodyCaptionPosition = "Left";

        if (isNew
            || string.IsNullOrWhiteSpace(previousBodyCaption)
            || string.Equals(previousBodyCaption, "Value", StringComparison.OrdinalIgnoreCase))
        {
            childItem.BodyCaption = childItem.ControlCaption;
        }

        childItem.SetHierarchy(parentItem.PageName, parentItem);
        childItem.ApplyTheme(IsDarkTheme);
    }

    private static void EnsureFooterSubItemHostHeight(PageItemModel item)
    {
        if (!item.HasFooterSubItems)
        {
            return;
        }

        var minimumHeight = item.ItemHeaderHeight + item.ItemBodyCaptionHeight + item.FooterPanelHeight + 26;
        item.Height = System.Math.Max(item.Height, minimumHeight);
    }

    private static string? GetRelativeTargetPath(string? rootTargetPath, string? childTargetPath)
    {
        if (string.IsNullOrWhiteSpace(rootTargetPath) || string.IsNullOrWhiteSpace(childTargetPath))
        {
            return null;
        }

        if (!childTargetPath.StartsWith(rootTargetPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return childTargetPath[(rootTargetPath.Length + 1)..];
    }

    private static IEnumerable<string> EnumerateRelativeChildItemPaths(Item rootItem)
    {
        foreach (var child in rootItem.GetDictionary().Values.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(child.Name))
            {
                continue;
            }

            yield return child.Name;

            foreach (var descendantPath in EnumerateRelativeChildItemPaths(child))
            {
                yield return $"{child.Name}/{descendantPath}";
            }
        }
    }

    private IEnumerable<string> GetSelectableTargetOptions(PageItemModel? item = null)
    {
        var excludedPrefixes = GetNonSelectableTargetPrefixes();
        var allOptions = HostRegistries.Data.GetAllKeys()
            .SelectMany(static key => EnumerateSelectablePaths(key))
            .Where(static key => !key.StartsWith("Runtime/UdlClient/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !HasExcludedTargetPrefix(path, excludedPrefixes))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var filterPrefix = item is null ? string.Empty : GetTargetTreeFilterPrefix(item, allOptions);
        if (string.IsNullOrWhiteSpace(filterPrefix))
        {
            return allOptions;
        }

        return allOptions
            .Where(path => path.StartsWith(filterPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private HashSet<string> GetNonSelectableTargetPrefixes()
    {
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in Pages)
        {
            var pageName = NormalizeTargetPathSegment(page.Name);
            if (string.IsNullOrWhiteSpace(pageName))
            {
                continue;
            }

            foreach (var pageItem in EnumeratePageItems(page.Items))
            {
                if (pageItem.Kind != ControlKind.UdlClientControl)
                {
                    continue;
                }

                var itemName = NormalizeTargetPathSegment(pageItem.Name);
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    continue;
                }

                prefixes.Add($"UdlBook/{pageName}/{itemName}/Status");
            }
        }

        return prefixes;
    }

    private static bool HasExcludedTargetPrefix(string path, IReadOnlyCollection<string> excludedPrefixes)
    {
        if (string.IsNullOrWhiteSpace(path) || excludedPrefixes.Count == 0)
        {
            return false;
        }

        var normalizedPath = path.Replace('\\', '/').Trim('/');
        foreach (var prefix in excludedPrefixes)
        {
            if (string.Equals(normalizedPath, prefix, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string GetTargetTreeFilterPrefix(PageItemModel item, IReadOnlyList<string> allOptions)
    {
        var pageName = NormalizeTargetPathSegment(item.PageName);
        if (string.IsNullOrWhiteSpace(pageName))
        {
            var owningPage = FindOwningPage(item) ?? SelectedPage;
            pageName = NormalizeTargetPathSegment(owningPage.Name);
        }

        if (string.IsNullOrWhiteSpace(pageName))
        {
            return string.Empty;
        }

        var currentPrefix = TryExtractPageTargetPrefix(item.TargetPath, pageName);
        if (!string.IsNullOrWhiteSpace(currentPrefix))
        {
            return currentPrefix;
        }

        return allOptions
            .Select(path => TryExtractPageTargetPrefix(path, pageName))
            .Where(static prefix => !string.IsNullOrWhiteSpace(prefix))
            .GroupBy(static prefix => prefix!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key.Length)
            .Select(static group => group.Key)
            .FirstOrDefault() ?? string.Empty;
    }

    private static string NormalizeTargetPathSegment(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Trim('/');

    private static string? TryExtractPageTargetPrefix(string? path, string pageName)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(pageName))
        {
            return null;
        }

        var segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var index = 1; index < segments.Length; index++)
        {
            if (string.Equals(segments[index], pageName, StringComparison.OrdinalIgnoreCase))
            {
                return string.Join('/', segments.Take(index + 1)) + "/";
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSelectablePaths(string key)
    {
        if (!HostRegistries.Data.TryGet(key, out var item) || item is null)
        {
            return [key];
        }

        return EnumerateItemPaths(item);
    }

    private static IEnumerable<string> EnumerateItemPaths(Item item)
    {
        if (!string.IsNullOrWhiteSpace(item.Path))
        {
            yield return item.Path!;
        }

        foreach (var child in item.GetDictionary().Values.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var childPath in EnumerateItemPaths(child))
            {
                yield return childPath;
            }
        }
    }

    private static bool TryResolveDataItem(string targetPath, out Item? item)
    {
        foreach (var candidatePath in TargetPathHelper.EnumerateResolutionCandidates(targetPath))
        {
            if (HostRegistries.Data.TryGet(candidatePath, out item) && item is not null)
            {
                return true;
            }

            var rootKey = HostRegistries.Data.GetAllKeys()
                .Where(key => candidatePath.StartsWith(key + "/", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(key => key.Length)
                .FirstOrDefault();

            if (rootKey is not null
                && HostRegistries.Data.TryGet(rootKey, out var rootItem)
                && rootItem is not null
                && TryResolveRelativeChild(rootItem, candidatePath[(rootKey.Length + 1)..], out item))
            {
                return true;
            }
        }

        item = null;
        return false;
    }

    private static bool TryResolveRelativeChild(Item rootItem, string relativePath, out Item? item)
    {
        var current = rootItem;
        foreach (var segment in relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!current.Has(segment))
            {
                item = null;
                return false;
            }

            current = current[segment];
        }

        item = current;
        return true;
    }

    private PageModel? FindOwningPage(PageItemModel item)
    {
        return Pages.FirstOrDefault(page => EnumeratePageItems(page.Items).Any(candidate => ReferenceEquals(candidate, item)));
    }

    protected static List<PageModel> CreateDefaultPages()
    {
        return
        [
            new PageModel
            {
                Index = 1,
                Name = "Page1"
            }
        ];
    }

    public void RefreshPageBindings(string pageName)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => RefreshPageBindings(pageName));
            return;
        }

        if (string.IsNullOrWhiteSpace(pageName))
        {
            return;
        }

        var page = Pages.FirstOrDefault(candidate => string.Equals(candidate.Name, pageName, StringComparison.Ordinal));
        if (page is null)
        {
            return;
        }

        foreach (var item in EnumeratePageItems(page.Items))
        {
            item.ResolveTarget();
            item.RefreshTargetBindings();
        }

        if (IsEditorDialogOpen && _editorDialogItem is not null && string.Equals(_editorDialogItem.PageName, pageName, StringComparison.Ordinal))
        {
            RefreshEditorDialogChoiceOptions(_editorDialogItem);
        }
    }
}

































