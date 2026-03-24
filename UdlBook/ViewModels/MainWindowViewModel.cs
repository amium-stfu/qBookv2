using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;

namespace UdlBook.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private bool _isDarkTheme;
    private bool _isHeaderCollapsed;
    private string _statusText;
    private Dock _tabStripPlacement = Dock.Right;
    private UdlBookPageViewModel _selectedPage;

    public MainWindowViewModel()
    {
        Pages =
        [
            new UdlBookPageViewModel(
                1,
                "Overview",
                "Overview",
                "Produkt-Shell fuer den abgespeckten UDL-Workflow.",
                "Trennung der Produkte",
                "UdlBook bekommt eine eigene Shell, auch wenn der Innenbereich im Moment noch wenig Funktionalitaet hat.",
                "Shell",
                false,
                string.Empty,
                string.Empty,
                [
                    "Eigenes MainWindow statt Konfigurationszweige im AmiumStudio-Fenster.",
                    "Gemeinsame Avalonia-Bausteine liegen jetzt direkt in Amium.Editor.",
                    "UdlBook kann spaeter gezielt UdlClient-spezifische Controls aufnehmen."
                ],
                "Diese Seite ist die neutrale Startflaeche fuer das UdlBook-Produkt."),
            new UdlBookPageViewModel(
                2,
                "UdlClient",
                "UdlClient",
                "Reservierter Einstiegspunkt fuer das kommende UdlClient-Control in UdlBook.",
                "Naechster konkreter Ausbau",
                "Hier sitzt spaeter die UdlClient-Oberflaeche. AmiumStudio bleibt davon bewusst frei, weil dort Clients codebasiert erzeugt werden.",
                "Client",
                true,
                "UdlClient Control Slot",
                "Diese Flaeche dient vorerst als Platzhalter fuer das erste produkt-spezifische UdlBook-Control.",
                [
                    "UdlBook darf UdlClient-spezifische Visualisierung direkt aufnehmen.",
                    "Die Shell-Struktur ist bereits getrennt, damit das Control spaeter nicht in AmiumStudio hineinleckt.",
                    "Shared-Controls bleiben generisch; die konkrete Einbindung liegt in der Product-Shell."
                ],
                "Erster geplanter Ausbaupunkt: dediziertes UdlClient-Control nur fuer UdlBook."),
            new UdlBookPageViewModel(
                3,
                "Roadmap",
                "Roadmap",
                "Arbeitsstand der Produktentflechtung zwischen UdlBook und AmiumStudio.",
                "Kurzfristige Schritte",
                "Zuerst wird nur die Shell sauber getrennt. Danach folgen produktneutrale Auslagerungen und erst dann die erste echte UdlBook-spezifische Funktion.",
                "Plan",
                false,
                string.Empty,
                string.Empty,
                [
                    "MainWindow und MainWindowViewModel pro Produkt getrennt halten.",
                    "Gemeinsam genutzte Teilansichten spaeter produktneutral im Editor bündeln.",
                    "UdlBook danach gezielt um UdlClient-spezifische Flaechen erweitern."
                ],
                "Diese Roadmap vermeidet spaetere Entflechtung quer durch beide Apps.")
        ];

        SelectPageCommand = new RelayCommand<UdlBookPageViewModel>(SelectPage);
        SelectPageByNameCommand = new RelayCommand<string>(SelectPageByName);
        ToggleHeaderCollapsedCommand = new RelayCommand(ToggleHeaderCollapsed);
        ToggleTabPlacementCommand = new RelayCommand(ToggleTabPlacement);

        _selectedPage = Pages[0];
        _selectedPage.IsSelected = true;
        _statusText = "Eigene UdlBook-Shell aktiv. Innenbereich ist noch bewusst generisch.";
    }

    public ObservableCollection<UdlBookPageViewModel> Pages { get; }

    public RelayCommand<UdlBookPageViewModel> SelectPageCommand { get; }

    public RelayCommand<string> SelectPageByNameCommand { get; }

    public RelayCommand ToggleHeaderCollapsedCommand { get; }

    public RelayCommand ToggleTabPlacementCommand { get; }

    public UdlBookPageViewModel SelectedPage
    {
        get => _selectedPage;
        private set
        {
            if (ReferenceEquals(value, _selectedPage))
            {
                return;
            }

            _selectedPage.IsSelected = false;
            if (SetProperty(ref _selectedPage, value))
            {
                _selectedPage.IsSelected = true;
                OnPropertyChanged(nameof(HeaderSummary));
                OnPropertyChanged(nameof(FooterText));
                StatusText = $"Seite aktiv: {_selectedPage.TabTitle}";
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
                OnPropertyChanged(nameof(WindowBackground));
                OnPropertyChanged(nameof(CardBackground));
                OnPropertyChanged(nameof(CardBorderBrush));
                OnPropertyChanged(nameof(CanvasBackground));
                OnPropertyChanged(nameof(CanvasBorderBrush));
                OnPropertyChanged(nameof(PrimaryTextBrush));
                OnPropertyChanged(nameof(SecondaryTextBrush));
                OnPropertyChanged(nameof(TabSelectNumerBackColor));
                OnPropertyChanged(nameof(TabSelectBackColor));
                OnPropertyChanged(nameof(TabSelectForeColor));
                OnPropertyChanged(nameof(TabNumerBackColor));
                OnPropertyChanged(nameof(TabBackColor));
                OnPropertyChanged(nameof(TabForeColor));
                OnPropertyChanged(nameof(HeaderBadgeBackground));
                OnPropertyChanged(nameof(HeaderBadgeForeground));
                StatusText = value ? "Dark Theme aktiv" : "Light Theme aktiv";
            }
        }
    }

    public bool IsHeaderCollapsed
    {
        get => _isHeaderCollapsed;
        set
        {
            if (SetProperty(ref _isHeaderCollapsed, value))
            {
                OnPropertyChanged(nameof(IsHeaderExpanded));
            }
        }
    }

    public bool IsHeaderExpanded => !IsHeaderCollapsed;

    public Dock TabStripPlacement
    {
        get => _tabStripPlacement;
        set
        {
            if (SetProperty(ref _tabStripPlacement, value))
            {
                OnPropertyChanged(nameof(IsTopTabStripPlacement));
                OnPropertyChanged(nameof(IsRightTabStripPlacement));
                OnPropertyChanged(nameof(TabPlacementGlyph));
                StatusText = value == Dock.Top ? "Tab-Position: Top" : "Tab-Position: Right";
            }
        }
    }

    public bool IsTopTabStripPlacement => TabStripPlacement == Dock.Top;

    public bool IsRightTabStripPlacement => TabStripPlacement == Dock.Right;

    public string TabPlacementGlyph => TabStripPlacement == Dock.Top ? ">" : "^";

    public string HeaderSummary => SelectedPage.Description;

    public string FooterText => $"UdlBook | Seite {SelectedPage.Index}: {SelectedPage.TabTitle}";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private ThemePalette CurrentTheme => IsDarkTheme ? ThemePalette.Dark : ThemePalette.Light;

    public string WindowBackground => CurrentTheme.WindowBackground;

    public string CardBackground => CurrentTheme.CardBackground;

    public string CardBorderBrush => CurrentTheme.CardBorderBrush;

    public string CanvasBackground => CurrentTheme.CanvasBackground;

    public string CanvasBorderBrush => CurrentTheme.CanvasBorderBrush;

    public string PrimaryTextBrush => CurrentTheme.PrimaryTextBrush;

    public string SecondaryTextBrush => CurrentTheme.SecondaryTextBrush;

    public string TabSelectNumerBackColor => CurrentTheme.TabSelectNumerBackColor;

    public string TabSelectBackColor => CurrentTheme.TabSelectBackColor;

    public string TabSelectForeColor => CurrentTheme.TabSelectForeColor;

    public string TabNumerBackColor => CurrentTheme.TabNumerBackColor;

    public string TabBackColor => CurrentTheme.TabBackColor;

    public string TabForeColor => CurrentTheme.TabForeColor;

    public string HeaderBadgeBackground => CurrentTheme.HeaderBadgeBackground;

    public string HeaderBadgeForeground => CurrentTheme.HeaderBadgeForeground;

    private void SelectPage(UdlBookPageViewModel? page)
    {
        if (page is null)
        {
            return;
        }

        SelectedPage = page;
    }

    private void SelectPageByName(string? pageName)
    {
        if (string.IsNullOrWhiteSpace(pageName))
        {
            return;
        }

        var page = Pages.FirstOrDefault(candidate => string.Equals(candidate.Name, pageName, StringComparison.OrdinalIgnoreCase));
        if (page is not null)
        {
            SelectedPage = page;
        }
    }

    private void ToggleHeaderCollapsed()
    {
        IsHeaderCollapsed = !IsHeaderCollapsed;
    }

    private void ToggleTabPlacement()
    {
        TabStripPlacement = TabStripPlacement == Dock.Top ? Dock.Right : Dock.Top;
    }
}