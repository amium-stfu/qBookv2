
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Amium.EditorUi;
using Amium.Items;
using Amium.Host;
using Amium.Logging;
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
    private int _gridSize = 20;
    private string _statusText;
    private string _dataRegistrySummary;
    private string _editorDialogChoiceSummary;
    private PageItemModel? _selectedItem;
    private PageModel _selectedPage = null!;
    private double _canvasWidth;
    private double _canvasHeight;
    private PageItemModel? _listPopupTarget;
    private EditorDialogMode _editorDialogMode;
    private PageItemModel? _editorDialogItem;
    private PageItemModel? _editorDialogParentItem;
    private string _editorDialogTitle = string.Empty;
    private string _editorDialogError = string.Empty;
    private bool _isEditorDialogOpen;
    private double _editorDialogX;
    private double _editorDialogY;
    private PageItemModel? _activeValueInputItem;
    private bool _isValueInputOpen;
    private Dock _tabStripPlacement = Dock.Right;

    public MainWindowViewModel(bool supportsUdlClientControl = false)
    {
        _supportsUdlClientControl = supportsUdlClientControl;
        Pages = [];
        GridLines = [];
        SelectionState = new SelectionState();
        EditorDialogSections = [];
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
        _editorDialogChoiceSummary = "Dialog Choices: geschlossen";
        _statusText = $"Layout-Datei: {LayoutFilePath}";
        HostRegistries.Data.RegistryChanged += OnDataRegistryStructureChanged;

        SetPages(CreateDefaultPages());
        RefreshDataRegistryDiagnostics();
    }

    public bool SupportsUdlClientControl => _supportsUdlClientControl;

    public ObservableCollection<PageModel> Pages { get; }

    public ObservableCollection<EditorGridLine> GridLines { get; }

    public ObservableCollection<EditorDialogSection> EditorDialogSections { get; }

    public ObservableCollection<HostMessageEntry> Messages { get; }

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
                if (!value)
                {
                    CancelSelection();
                    CancelEditorDialog();
                    CancelValueInput();
                    SaveLayout();
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
                OnPropertyChanged(nameof(HeaderBadgeBackground));
                OnPropertyChanged(nameof(HeaderBadgeForeground));
            }
        }
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
    public string HeaderBadgeBackground => CurrentTheme.HeaderBadgeBackground;
    public string HeaderBadgeForeground => CurrentTheme.HeaderBadgeForeground;
    public bool HasMultiSelection => _selectedItems.Count > 1;
    public bool ShowAlignmentPanel => IsEditMode && HasMultiSelection;
    public int SelectedItemsCount => _selectedItems.Count;
    public string EditModeText => IsEditMode ? "Edit mode aktiv" : "View mode";
    public string FooterText => $"{SelectedPage.Name} aktiv | {SelectedPage.Items.Count} Controls | {SelectedItemsCount} ausgewaehlt | {(IsEditMode ? "Edit" : "Navigation")}";

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
            StatusText = _selectedItems.Count == 1 ? $"{_selectedItems[0].Title} ausgewaehlt" : $"{_selectedItems.Count} Controls ausgewaehlt";
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

        OpenEditorDialog(EditorDialogMode.AddCanvas, draft, null, SelectionState.PopupX + 16, SelectionState.PopupY + 16, $"{kind} anlegen");
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

        OpenEditorDialog(EditorDialogMode.AddList, draft, _listPopupTarget, SelectionState.ListPopupX + 16, SelectionState.ListPopupY + 16, $"{kind} anlegen");
        CancelListPopup();
    }

    public void OpenItemEditor(PageItemModel item, double x, double y)
    {
        OpenEditorDialog(EditorDialogMode.Edit, item, item.ParentItem, x, y, $"{item.Name} bearbeiten");
    }

    public void OpenValueInput(PageItemModel item)
    {
        if (item is null || !item.CanOpenValueEditor)
        {
            return;
        }

        ActiveValueInputItem = item;
        IsValueInputOpen = true;
        StatusText = $"Eingabe aktiv: {item.Title}";
    }

    public void CancelValueInput()
    {
        ActiveValueInputItem = null;
        IsValueInputOpen = false;
    }

    public void CommitEditorDialog()
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
                StatusText = $"{_editorDialogItem.Kind} '{_editorDialogItem.Name}' auf {SelectedPage.Name} eingefuegt";
                CancelSelection();
                break;
            case EditorDialogMode.AddList:
                if (_editorDialogParentItem is not null)
                {
                    NormalizeListChild(_editorDialogParentItem, _editorDialogItem);
                    _editorDialogItem.SetHierarchy(SelectedPage.Name, _editorDialogParentItem);
                    _editorDialogParentItem.Items.Add(_editorDialogItem);
                    _editorDialogParentItem.ApplyListHeightRules();
                    StatusText = $"{_editorDialogItem.Kind} '{_editorDialogItem.Name}' in {_editorDialogParentItem.Name} eingefuegt";
                }
                break;
            case EditorDialogMode.Edit:
                StatusText = $"Control gespeichert: {_editorDialogItem.Path}";
                break;
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
            if (TrySaveSelectedPageUiJson(out var uiSaveTarget))
            {
                savedTargets.Add(uiSaveTarget);
            }

            if (TrySaveBookManifest(out var bookSaveTarget))
            {
                savedTargets.Add(bookSaveTarget);
            }

            StatusText = savedTargets.Count > 0
                ? $"Gespeichert: {string.Join(" | ", savedTargets)}"
                : "Keine speicherbaren Book-Dateien gefunden";
            return;
        }

        var document = new LayoutDocument
        {
            TabStripPlacement = TabStripPlacement.ToString(),
            Pages = Pages.Select(ToDocument).ToList()
        };

        var json = JsonSerializer.Serialize(document, _jsonOptions);
        File.WriteAllText(LayoutFilePath, json);
        StatusText = $"Layout gespeichert: {LayoutFilePath}";
    }

    private bool TrySaveSelectedPageUiJson(out string savedTarget)
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

        var json = JsonSerializer.Serialize(documentObject, _jsonOptions);
        var directory = Path.GetDirectoryName(uiFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(uiFilePath, json);
        savedTarget = $"Page.json: {Path.GetFileName(Path.GetDirectoryName(uiFilePath))}";
        return true;
    }

    private bool TrySaveBookManifest(out string savedTarget)
    {
        savedTarget = string.Empty;
        if (!Pages.Any(page => !string.IsNullOrWhiteSpace(page.UiFilePath)))
        {
            return false;
        }

        var bookJsonPath = GetBookManifestPath();
        if (string.IsNullOrWhiteSpace(bookJsonPath))
        {
            return false;
        }

        var documentObject = LoadJsonObject(bookJsonPath);
        documentObject["TabStripPlacement"] = TabStripPlacement.ToString();

        var directory = Path.GetDirectoryName(bookJsonPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(documentObject, _jsonOptions);
        File.WriteAllText(bookJsonPath, json);
        savedTarget = $"Book.json: {TabStripPlacement}";
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
        nodeObject["ButtonText"] = item.ButtonText;
        SetOptionalJsonValue(nodeObject, "ButtonIcon", item.ButtonIcon);
        nodeObject["ButtonOnlyIcon"] = item.ButtonOnlyIcon;
        nodeObject["ButtonIconAlign"] = item.ButtonIconAlign;
        nodeObject["ButtonTextAlign"] = item.ButtonTextAlign;
        nodeObject["ButtonCommand"] = item.ButtonCommand;
        nodeObject["ButtonBodyBackground"] = item.ButtonBodyBackground;
        nodeObject["ButtonBodyForegroundColor"] = item.ButtonBodyForegroundColor;
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
        nodeObject["TargetPath"] = item.TargetPath;
        nodeObject["TargetParameterPath"] = item.TargetParameterPath;
        nodeObject["TargetParameterFormat"] = item.TargetParameterFormat;
        nodeObject["TargetLog"] = item.TargetLog;
        nodeObject["RefreshRateMs"] = item.RefreshRateMs;
        nodeObject["HistorySeconds"] = item.HistorySeconds;
        nodeObject["ViewSeconds"] = item.ViewSeconds;
        nodeObject["ChartSeriesDefinitions"] = item.ChartSeriesDefinitions;
        nodeObject["UdlClientHost"] = item.UdlClientHost;
        nodeObject["UdlClientPort"] = item.UdlClientPort;
        nodeObject["UdlAttachedItemPaths"] = item.UdlAttachedItemPaths;
        nodeObject["IsReadOnly"] = item.IsReadOnly;
        nodeObject["IsAutoHeight"] = item.IsAutoHeight;
        nodeObject["ListItemHeight"] = item.ListItemHeight;
        nodeObject["ControlHeight"] = item.ControlHeight;
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
        item.ControlCaption = GetFirstStringProperty(properties, "ControlCaption", "Header") ?? item.ControlCaption;
        item.CaptionVisible = GetFirstBoolProperty(properties, "CaptionVisible", "ShowCaption") ?? item.CaptionVisible;
        item.BodyCaption = GetFirstStringProperty(properties, "BodyCaption", "Title") ?? item.BodyCaption;
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
        item.ButtonBodyBackground = GetStringProperty(properties, "ButtonBodyBackground") ?? item.ButtonBodyBackground;
        item.ButtonBodyForegroundColor = GetStringProperty(properties, "ButtonBodyForegroundColor") ?? item.ButtonBodyForegroundColor;
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
        item.TargetPath = GetStringProperty(properties, "TargetPath") ?? item.TargetPath;
        item.TargetParameterPath = GetStringProperty(properties, "TargetParameterPath") ?? item.TargetParameterPath;
        item.TargetParameterFormat = GetStringProperty(properties, "TargetParameterFormat") ?? item.TargetParameterFormat;
        item.TargetLog = GetStringProperty(properties, "TargetLog") ?? item.TargetLog;
        item.RefreshRateMs = GetIntProperty(properties, "RefreshRateMs") ?? item.RefreshRateMs;
        item.HistorySeconds = GetIntProperty(properties, "HistorySeconds") ?? item.HistorySeconds;
        item.ViewSeconds = GetIntProperty(properties, "ViewSeconds") ?? item.ViewSeconds;
        item.ChartSeriesDefinitions = GetStringProperty(properties, "ChartSeriesDefinitions") ?? item.ChartSeriesDefinitions;
        item.UdlClientHost = GetStringProperty(properties, "UdlClientHost") ?? item.UdlClientHost;
        item.UdlClientPort = GetIntProperty(properties, "UdlClientPort") ?? item.UdlClientPort;
        item.UdlAttachedItemPaths = GetStringProperty(properties, "UdlAttachedItemPaths") ?? item.UdlAttachedItemPaths;
        item.IsReadOnly = GetBoolProperty(properties, "IsReadOnly") ?? item.IsReadOnly;
        item.IsAutoHeight = GetBoolProperty(properties, "IsAutoHeight") ?? item.IsAutoHeight;
        item.ListItemHeight = GetDoubleProperty(properties, "ListItemHeight") ?? item.ListItemHeight;
        item.ControlHeight = GetDoubleProperty(properties, "ControlHeight") ?? item.ControlHeight;
        item.ControlBorderWidth = GetDoubleProperty(properties, "ControlBorderWidth") ?? item.ControlBorderWidth;
        item.ControlBorderColor = GetStringProperty(properties, "ControlBorderColor") ?? item.ControlBorderColor;
        item.ControlCornerRadius = GetDoubleProperty(properties, "ControlCornerRadius") ?? item.ControlCornerRadius;
        item.UiNodeType = GetStringProperty(properties, "Type") ?? fallbackType ?? item.UiNodeType;

        if (string.IsNullOrWhiteSpace(item.ControlCaption) && item.IsItem)
        {
            item.ControlCaption = pageName;
        }
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

    private void SetTabStripPlacement(string? placement)
    {
        TabStripPlacement = ParseTabStripPlacement(placement);
    }

    private void ToggleHeaderCollapsed()
    {
        IsHeaderCollapsed = !IsHeaderCollapsed;
        StatusText = IsHeaderCollapsed ? "Kopfbereich eingeklappt" : "Kopfbereich ausgeklappt";
    }

    private void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;
        if (IsEditMode)
        {
            StatusText = "Edit mode aktiviert";
        }
    }

    protected void ApplyBookTabStripPlacement(string? bookRootDirectory)
    {
        var documentObject = LoadJsonObject(GetBookManifestPath(bookRootDirectory));
        TabStripPlacement = ParseTabStripPlacement(GetStringProperty(documentObject, "TabStripPlacement"));
    }

    protected virtual string? CurrentProjectRootDirectory => null;

    protected string? GetBookManifestPath(string? bookRootDirectory = null)
    {
        var rootDirectory = string.IsNullOrWhiteSpace(bookRootDirectory) ? CurrentProjectRootDirectory : bookRootDirectory;
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return null;
        }

        return Path.Combine(rootDirectory, "Book.json");
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
            StatusText = $"Keine Layout-Datei gefunden: {LayoutFilePath}";
            return;
        }

        var json = File.ReadAllText(LayoutFilePath);
        var document = JsonSerializer.Deserialize<LayoutDocument>(json, _jsonOptions);

        if (document is null || document.Pages.Count == 0)
        {
            StatusText = "Layout-Datei ist leer oder ungueltig";
            return;
        }

        TabStripPlacement = ParseTabStripPlacement(document.TabStripPlacement);
        SetPages(document.Pages.Select(ToModel).ToList());
        StatusText = $"Layout geladen: {LayoutFilePath}";
    }

    public PageItemModel CreateItem(ControlKind kind, double x, double y, double width, double height)
    {
        return kind switch
        {
            ControlKind.Button => new PageItemModel
            {
                Kind = ControlKind.Button,
                ControlCaption = string.Empty,
                BodyCaption = "Button",
                Footer = "Action button",
                ButtonText = "Button",
                ButtonTextAlign = "Center",
                ButtonIconAlign = "Left",
                ButtonBodyBackground = "Transparent",
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
            ControlKind.LogControl => new PageItemModel
            {
                Kind = ControlKind.LogControl,
                ControlCaption = string.Empty,
                BodyCaption = "ProcessLog",
                Footer = "Live host log",
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
                BodyCaption = "UdlClient",
                Footer = "Disconnected",
                UdlClientHost = "192.168.178.15",
                UdlClientPort = 9001,
                X = x,
                Y = y,
                Width = Math.Max(width, 420),
                Height = Math.Max(height, 170),
                ContainerBorderWidth = 0
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static PageItemModel CreateDefaultItem(double x, double y, double width, double height)
    {
        var item = new PageItemModel
        {
            Kind = ControlKind.Item,
            ControlCaption = "Item",
            BodyCaption = "Value",
            Footer = "Unit",
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
    }


    private void OnDataRegistryStructureChanged(object? sender, DataChangedEventArgs e)
    {
        var registryCount = HostRegistries.Data.GetAllKeys().Count;
        var message = $"MainWindowViewModel.OnDataRegistryStructureChanged pid={HostRegistries.ProcessId} session={HostRegistries.SessionId} dataRegistryId={HostRegistries.DataRegistryId} key={e.Key} change={e.ChangeKind} count={registryCount}";
        Debug.WriteLine(message);
        HostLogger.Log.Information(message);
        Dispatcher.UIThread.Post(() =>
        {
            RefreshDataRegistryDiagnostics();
            RefreshOpenEditorDialogChoiceOptions();
        });
    }

    private void RefreshOpenEditorDialogChoiceOptions()
    {
        if (!IsEditorDialogOpen || _editorDialogItem is null)
        {
            return;
        }

        var keys = HostRegistries.Data.GetAllKeys().OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
        var message = $"RefreshOpenEditorDialogChoiceOptions open={IsEditorDialogOpen} keys={keys.Count} [{string.Join(", ", keys)}]";
        Debug.WriteLine(message);
        HostLogger.Log.Information(message);

        RefreshEditorDialogChoiceOptions(_editorDialogItem);
    }

    private void RefreshEditorDialogChoiceOptions(PageItemModel item)
    {
        foreach (var field in EditorDialogSections.SelectMany(section => section.Fields))
        {
            if (field.IsAttachItemList)
            {
                field.RefreshAttachItemOptions(GetUdlAttachItemOptions(item));
                continue;
            }

            if (!field.IsChoice)
            {
                continue;
            }

            var selectedTargetPath = GetSelectedTargetPath(item);
            var options = field.Key switch
            {
                "TargetParameterPath" => GetTargetParameterOptions(selectedTargetPath),
                _ when field.Definition.OptionsFactory is not null => field.Definition.OptionsFactory(item),
                _ => []
            };

            var selectFirstWhenInvalid = field.Key == "TargetParameterPath" && !string.IsNullOrWhiteSpace(selectedTargetPath);
            RefreshDialogFieldOptions(field, options, selectFirstWhenInvalid);
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
                ? "Dialog Choices: kein Target-Feld"
                : "Dialog Choices: geschlossen";
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
        if (!hasCurrentValue && selectFirstWhenInvalid)
        {
            field.Value = normalizedOptions.FirstOrDefault() ?? string.Empty;
        }
    }
    private void OnEditorDialogFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EditorDialogField.Value) || sender is not EditorDialogField field || _editorDialogItem is null)
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

            EditorDialogTitle = _editorDialogMode == EditorDialogMode.Edit ? $"{field.Value} bearbeiten" : $"{_editorDialogItem.Kind} anlegen";
        }

        if (string.Equals(field.Key, "TargetPath", StringComparison.Ordinal))
        {
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

    private IReadOnlyList<EditorDialogSection> BuildSectionsForItem(PageItemModel item)
    {
        return BuildBindingSectionsForItem(item)
            .Select(sectionBinding =>
            {
                var section = new EditorDialogSection(sectionBinding.Title);
                foreach (var binding in sectionBinding.Bindings)
                {
                    section.Fields.Add(binding.CreateField(item));
                }

                return section;
            })
            .ToList();
    }

    private IReadOnlyList<(string Title, IReadOnlyList<EditorDialogBindingDefinition> Bindings)> BuildBindingSectionsForItem(PageItemModel item)
    {
        var identity = new List<EditorDialogBindingDefinition>
        {
            BindText("Name", "Name", current => current.Name, (current, value) => { current.Name = value; return null; }),
            BindReadOnly("Path", "Path", current => current.Path),
            BindReadOnly("Id", "Id", current => current.Id)
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
            BindText("Footer", "FooterText", current => current.Footer, (current, value) => { current.Footer = value; return null; })
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
                sections.Add(("Specific", new List<EditorDialogBindingDefinition>(commonSpecific)
                {
                    BindChoice("ButtonCommand", "Command", current => current.ButtonCommand, (current, value) => { current.ButtonCommand = value; return null; }, _ => GetCommandRegistryOptions()),
                    BindText("ButtonText", "ButtonText", current => current.ButtonText, (current, value) => { current.ButtonText = value; return null; }),
                    BindText("ButtonIcon", "Icon", current => current.ButtonIcon, (current, value) => { current.ButtonIcon = value; return null; }),
                    BindChoice("ButtonOnlyIcon", "OnlyIcon", current => current.ButtonOnlyIcon ? "True" : "False", (current, value) => { current.ButtonOnlyIcon = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase); return null; }, _ => new[] { "False", "True" }),
                    BindChoice("ButtonIconAlign", "IconAlign", current => current.ButtonIconAlign, (current, value) => { current.ButtonIconAlign = value; return null; }, _ => AlignmentOptions),
                    BindChoice("ButtonTextAlign", "Align", current => current.ButtonTextAlign, (current, value) => { current.ButtonTextAlign = value; return null; }, _ => AlignmentOptions),
                    BindText("ButtonBodyBackground", "BodyBackground", current => current.ButtonBodyBackground, (current, value) => { current.ButtonBodyBackground = value; return null; }, EditorPropertyType.Color),
                    BindText("ButtonBodyForegroundColor", "BodyForeColor", current => current.ButtonBodyForegroundColor, (current, value) => { current.ButtonBodyForegroundColor = value; return null; }, EditorPropertyType.Color),
                    BindChoice("UseThemeColor", "UseThemeColor", current => current.UseThemeColor ? "True" : "False", (current, value) => { current.UseThemeColor = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase); return null; }, _ => new[] { "True", "False" })
                }));
                break;
            case ControlKind.Item:
            case ControlKind.Signal:
                sections.Add(("Specific", new List<EditorDialogBindingDefinition>(commonSpecific)
                {
                    BindChoice("TargetPath", "Target", current => current.TargetPath, (current, value) => { current.ApplyTargetSelection(value); return null; }, _ => HostRegistries.Data.GetAllKeys().OrderBy(key => key)),
                    BindChoice("TargetParameterPath", "TargetParameter", current => current.TargetParameterPath, (current, value) => { current.TargetParameterPath = value; return null; }, current => GetTargetParameterOptions(current.TargetPath)),
                    BindChoice("TargetParameterFormatKind", "Format", current => SplitParameterFormat(current.TargetParameterFormat).Kind, (current, value) => { current.TargetParameterFormat = ComposeParameterFormat(value, SplitParameterFormat(current.TargetParameterFormat).Parameter); return null; }, _ => ParameterFormatOptions),
                    BindText("TargetParameterFormatParameter", "FormatParameter", current => SplitParameterFormat(current.TargetParameterFormat).Parameter, (current, value) => { current.TargetParameterFormat = ComposeParameterFormat(SplitParameterFormat(current.TargetParameterFormat).Kind, value); return null; }, EditorPropertyType.Text, GetFormatParameterToolTip),
                    BindChoice("IsReadOnly", "Readonly", current => current.IsReadOnly ? "True" : "False", (current, value) => { current.IsReadOnly = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase); return null; }, _ => new[] { "False", "True" }),
                    BindInt("RefreshRateMs", "RefreshRate ms", current => current.RefreshRateMs, (current, value) => current.RefreshRateMs = value)
                }));
                break;
            case ControlKind.ChartControl:
                sections.Add(("Specific", new List<EditorDialogBindingDefinition>(commonSpecific)
                {
                    BindChartSeriesList("ChartSeriesDefinitions", "Series", current => current.ChartSeriesDefinitions, (current, value) => { current.ChartSeriesDefinitions = value; return null; }, GetChartSeriesToolTip),
                    BindText("ContainerBorder", "ContainerBorder", current => current.ContainerBorder ?? string.Empty, (current, value) => { current.ContainerBorder = EmptyToNull(value); return null; }, EditorPropertyType.Color),
                    BindText("ContainerBackgroundColor", "ContainerBg", current => current.ContainerBackgroundColor ?? string.Empty, (current, value) => { current.ContainerBackgroundColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
                    BindDouble("ContainerBorderWidth", "ContainerBorderWidth", current => current.ContainerBorderWidth, (current, value) => current.ContainerBorderWidth = value),
                    BindInt("RefreshRateMs", "RefreshRate ms", current => current.RefreshRateMs, (current, value) => current.RefreshRateMs = value),
                    BindInt("HistorySeconds", "History s", current => current.HistorySeconds, (current, value) => current.HistorySeconds = value),
                    BindInt("ViewSeconds", "View s", current => current.ViewSeconds, (current, value) => current.ViewSeconds = value)
                }));
                break;
            case ControlKind.ListControl:
                sections.Add(("Specific", new List<EditorDialogBindingDefinition>(commonSpecific)
                {
                    BindText("ContainerBorder", "ContainerBorder", current => current.ContainerBorder ?? string.Empty, (current, value) => { current.ContainerBorder = EmptyToNull(value); return null; }, EditorPropertyType.Color),
                    BindText("ContainerBackgroundColor", "ContainerBg", current => current.ContainerBackgroundColor ?? string.Empty, (current, value) => { current.ContainerBackgroundColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
                    BindDouble("ContainerBorderWidth", "ContainerBorderWidth", current => current.ContainerBorderWidth, (current, value) => current.ContainerBorderWidth = value),
                    BindDouble("ControlHeight", "ControlHeight", current => current.ControlHeight, (current, value) => current.ControlHeight = value),
                    BindDouble("ControlBorderWidth", "ControlBorderWidth", current => current.ControlBorderWidth, (current, value) => current.ControlBorderWidth = value),
                    BindText("ControlBorderColor", "ControlBorderColor", current => current.ControlBorderColor ?? string.Empty, (current, value) => { current.ControlBorderColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
                    BindDouble("ControlCornerRadius", "ControlCornerRadius", current => current.ControlCornerRadius, (current, value) => current.ControlCornerRadius = value)
                }));
                break;
            case ControlKind.LogControl:
                sections.Add(("Specific", new List<EditorDialogBindingDefinition>(commonSpecific)
                {
                    BindChoice("TargetLog", "TargetLog", current => current.TargetLog, (current, value) => { current.TargetLog = value; return null; }, _ => GetProcessLogTargetOptions()),
                    BindText("ContainerBorder", "ContainerBorder", current => current.ContainerBorder ?? string.Empty, (current, value) => { current.ContainerBorder = EmptyToNull(value); return null; }, EditorPropertyType.Color),
                    BindText("ContainerBackgroundColor", "ContainerBg", current => current.ContainerBackgroundColor ?? string.Empty, (current, value) => { current.ContainerBackgroundColor = EmptyToNull(value); return null; }, EditorPropertyType.Color),
                    BindDouble("ContainerBorderWidth", "ContainerBorderWidth", current => current.ContainerBorderWidth, (current, value) => current.ContainerBorderWidth = value)
                }));
                break;
            case ControlKind.UdlClientControl:
                sections.Add(("Specific", new List<EditorDialogBindingDefinition>(commonSpecific)
                {
                    BindText("UdlClientHost", "Host", current => current.UdlClientHost, (current, value) => { current.UdlClientHost = value; return null; }),
                    BindInt("UdlClientPort", "Port", current => current.UdlClientPort, (current, value) => current.UdlClientPort = value),
                    BindAttachItemList("UdlAttachedItemPaths", "AttachToUi", current => current.UdlAttachedItemPaths, (current, value) => { current.UdlAttachedItemPaths = value; return null; }, GetUdlAttachItemOptions)
                }));
                break;
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

    private static EditorDialogBindingDefinition BindChoice(string key, string label, Func<PageItemModel, string> read, Func<PageItemModel, string, string?> apply, Func<PageItemModel, IEnumerable<string>> optionsFactory)
        => new(key, label, EditorPropertyType.Choice, read, apply, optionsFactory: optionsFactory);

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

        foreach (var field in EditorDialogSections.SelectMany(section => section.Fields))
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
        => EditorDialogSections.SelectMany(section => section.Fields).FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.Ordinal));

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
        _editorDialogMode = EditorDialogMode.None;
        _editorDialogItem = null;
        _editorDialogParentItem = null;
        EditorDialogTitle = string.Empty;
        EditorDialogError = string.Empty;
        IsEditorDialogOpen = false;
        EditorDialogChoiceSummary = "Dialog Choices: geschlossen";
    }

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
        item.SetHierarchy(pageName, parentItem);
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
            TargetPath = item.TargetPath,
            TargetParameterPath = item.TargetParameterPath,
            TargetParameterFormat = item.TargetParameterFormat,
            TargetLog = item.TargetLog,
            RefreshRateMs = item.RefreshRateMs,
            HistorySeconds = item.HistorySeconds,
            ViewSeconds = item.ViewSeconds,
            ChartSeriesDefinitions = item.ChartSeriesDefinitions,
            UdlClientHost = item.UdlClientHost,
            UdlClientPort = item.UdlClientPort,
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
            TargetPath = item.TargetPath,
            TargetParameterPath = item.TargetParameterPath,
            TargetParameterFormat = item.TargetParameterFormat,
            TargetLog = item.TargetLog,
            RefreshRateMs = item.RefreshRateMs,
            HistorySeconds = item.HistorySeconds,
            ViewSeconds = item.ViewSeconds,
            ChartSeriesDefinitions = item.ChartSeriesDefinitions,
            IsReadOnly = item.IsReadOnly,
            IsAutoHeight = item.IsAutoHeight,
            ListItemHeight = item.ControlHeight > 0 ? item.ControlHeight : item.ListItemHeight,
            ControlBorderWidth = item.ControlBorderWidth,
            ControlBorderColor = item.ControlBorderColor,
            ControlCornerRadius = item.ControlCornerRadius,
            X = item.X,
            Y = item.Y,
            Width = Math.Max(item.Width, item.Kind switch { ControlKind.Button => 140, ControlKind.Signal => 150, ControlKind.Item => 150, ControlKind.ListControl => 240, ControlKind.LogControl => 320, ControlKind.ChartControl => 360, _ => 140 }),
            Height = Math.Max(item.Height, item.Kind switch { ControlKind.Button => 56, ControlKind.Signal => 72, ControlKind.Item => 72, ControlKind.ListControl => 180, ControlKind.LogControl => 220, ControlKind.ChartControl => 220, _ => 72 })
        };

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            model.Name = item.Kind switch { ControlKind.Button => "Button", ControlKind.ListControl => "ListControl", ControlKind.LogControl => "LogControl", ControlKind.ChartControl => "ChartControl", _ => "Item" };
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

    private static (string Kind, string Parameter) SplitParameterFormat(string? format)
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
        => "Eine Serie pro Zeile: TargetPath|Y1 bis TargetPath|Y4 oder optional mit Stil TargetPath|Y1|Step. Immer Value numerisch, X ist DateTime. Beispiel\nPage1/Sinus/AThread Sinus|Y1\nPage1/Task/ATask Counter|Y2|Step";

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

        if (!HostRegistries.Data.TryGet(targetPath, out var item) || item is null)
        {
            var fallbackKey = HostRegistries.Data.GetAllKeys().FirstOrDefault(key => key.StartsWith(targetPath + "/", StringComparison.OrdinalIgnoreCase));
            if (fallbackKey is null || !HostRegistries.Data.TryGet(fallbackKey, out item) || item is null)
            {
                return [];
            }
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
        var baseName = kind switch { ControlKind.Button => "Button", ControlKind.ListControl => "ListControl", ControlKind.LogControl => "LogControl", ControlKind.ChartControl => "ChartControl", ControlKind.UdlClientControl => "UdlClientControl", _ => "Item" };
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

        if (normalizedName.Contains('.'))
        {
            error = "Name darf keinen Punkt enthalten.";
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
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
}

































