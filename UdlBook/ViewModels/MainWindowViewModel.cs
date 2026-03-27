using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Threading;
using Amium.Host;
using Amium.Logging;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;
using System.Text.Json.Nodes;

namespace UdlBook.ViewModels;

public sealed class MainWindowViewModel : Amium.UiEditor.ViewModels.MainWindowViewModel
{
    private readonly string _configPath;
    private readonly string _defaultLayoutPath;
    private readonly Dictionary<string, WatchedPage> _watchedPages = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _bookWatcher;
    private readonly UdlBookAppConfig _config;
    private string _startupPagePath;
    private string _currentLayoutFilePath = string.Empty;
    private string _bookProjectPath;
    private string _loadedBookSummary;
    private string _messagesSummary;
    private string _currentLogText;
    private string _headerTitle = "UdlBook";
    private bool _hasLayout;
    private bool _isDefaultLayout;
    private bool _isBookOperationRunning;

    public MainWindowViewModel()
        : base(true)
    {
        AutoSaveOnEditModeExit = false;
        _configPath = Path.Combine(AppContext.BaseDirectory, "UdlBook.config.yaml");
        _config = UdlBookAppConfig.Load(_configPath);

        var defaultLayoutDirectory = Path.Combine(AppContext.BaseDirectory, "DefaultLayout");
        _defaultLayoutPath = Path.Combine(defaultLayoutDirectory, "Page.json");
        EnsureDefaultLayout(defaultLayoutDirectory, _defaultLayoutPath);
        IsDarkTheme = !string.Equals(_config.DefaultTheme, "Light", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(_config.StartLayout))
        {
            _startupPagePath = _defaultLayoutPath;
        }
        else
        {
            var configuredPath = _config.StartLayout;
            string resolvedPath;
            try
            {
                resolvedPath = Path.GetFullPath(configuredPath);
            }
            catch
            {
                resolvedPath = configuredPath;
            }

            if (Directory.Exists(resolvedPath) || File.Exists(resolvedPath))
            {
                _startupPagePath = resolvedPath;
            }
            else
            {
                // Fallback: konfigurierter StartLayout-Pfad existiert nicht, DefaultLayout laden.
                _startupPagePath = _defaultLayoutPath;
            }
        }
        LoadBookCommand = new Amium.UiEditor.ViewModels.RelayCommand(LoadBook, CanRunBookAction);
        RebuildBookCommand = new Amium.UiEditor.ViewModels.RelayCommand(RebuildBook, CanRunBookAction);
        RefreshLogCommand = new Amium.UiEditor.ViewModels.RelayCommand(RefreshLog);
        _bookProjectPath = Path.GetDirectoryName(_startupPagePath) ?? AppContext.BaseDirectory;
        _loadedBookSummary = "No book loaded";
        _messagesSummary = "Keine Meldungen";
        _currentLogText = string.Empty;
        HostLogger.ProcessLog.EntryAdded += OnHostLogEntryAdded;
        RefreshLog();

        // Ensure the canvas grid starts disabled and keep legend icon color in sync.
        ShowGrid = false;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsEditMode))
            {
                if (!IsEditMode)
                {
                    ShowGrid = false;
                }

                OnPropertyChanged(nameof(LegendEditIconColor));
            }
            else if (e.PropertyName == nameof(TabSelectForeColor))
            {
                OnPropertyChanged(nameof(LegendEditIconColor));
            }
            else if (e.PropertyName == nameof(IsDarkTheme))
            {
                _config.DefaultTheme = IsDarkTheme ? "Dark" : "Light";
                _config.Save(_configPath);
            }
        };

        Dispatcher.UIThread.Post(LoadStartupPage, DispatcherPriority.Background);
    }

    private sealed class WatchedPage
    {
        public string FilePath { get; set; } = string.Empty;
        public int PageIndex { get; set; }
        public string PageName { get; set; } = string.Empty;
        public BookUiPageLayout Layout { get; set; } = default!;
    }

    protected override string? CurrentProjectRootDirectory => BookProjectPath;

    public string BookProjectPath
    {
        get => _bookProjectPath;
        set
        {
            if (SetProperty(ref _bookProjectPath, value))
            {
                LoadBookCommand.RaiseCanExecuteChanged();
                RebuildBookCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string LoadedBookSummary
    {
        get => _loadedBookSummary;
        private set => SetProperty(ref _loadedBookSummary, value);
    }

    public string MessagesSummary
    {
        get => _messagesSummary;
        private set => SetProperty(ref _messagesSummary, value);
    }

    public string CurrentLogFilePath => HostLogger.CurrentLogFilePath;

    public string CurrentLogText
    {
        get => _currentLogText;
        private set => SetProperty(ref _currentLogText, value);
    }

    public string HeaderTitle
    {
        get => _headerTitle;
        private set => SetProperty(ref _headerTitle, value);
    }

    public bool HasLayout
    {
        get => _hasLayout;
        private set
        {
            if (SetProperty(ref _hasLayout, value))
            {
                OnPropertyChanged(nameof(CanSaveLayout));
            }
        }
    }

    public bool IsDefaultLayout
    {
        get => _isDefaultLayout;
        private set
        {
            if (SetProperty(ref _isDefaultLayout, value))
            {
                OnPropertyChanged(nameof(CanSaveLayout));
            }
        }
    }

    public bool CanSaveLayout
    {
        get => HasLayout && !IsDefaultLayout;
    }

    public bool IsCurrentLayoutStartLayout
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_config.StartLayout))
            {
                return false;
            }

            try
            {
                var start = Path.GetFullPath(_config.StartLayout);

                if (IsDirectoryBook)
                {
                    if (string.IsNullOrWhiteSpace(BookProjectPath))
                    {
                        return false;
                    }

                    var currentDirectory = Path.GetFullPath(BookProjectPath);
                    return string.Equals(currentDirectory, start, StringComparison.OrdinalIgnoreCase);
                }

                if (string.IsNullOrWhiteSpace(_currentLayoutFilePath))
                {
                    return false;
                }

                var currentFile = Path.GetFullPath(_currentLayoutFilePath);
                return string.Equals(currentFile, start, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    public string StartLayoutIconColor => IsCurrentLayoutStartLayout ? "#F97316" : PrimaryTextBrush;

    public bool IsBookOperationRunning
    {
        get => _isBookOperationRunning;
        private set
        {
            if (SetProperty(ref _isBookOperationRunning, value))
            {
                LoadBookCommand.RaiseCanExecuteChanged();
                RebuildBookCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string LegendEditIconColor => IsEditMode ? "#DC2626" : TabSelectForeColor;

    public Amium.UiEditor.ViewModels.RelayCommand LoadBookCommand { get; }

    public Amium.UiEditor.ViewModels.RelayCommand RebuildBookCommand { get; }

    public Amium.UiEditor.ViewModels.RelayCommand RefreshLogCommand { get; }

    public bool IsDirectoryBook => _watchedPages.Count > 0;

    public void SetCurrentLayoutAsStartup()
    {
        // Verzeichnis-Buch: StartLayout soll das aktuelle Buch-Verzeichnis sein.
        if (IsDirectoryBook)
        {
            if (string.IsNullOrWhiteSpace(BookProjectPath) || !Directory.Exists(BookProjectPath))
            {
                AddMessage("StartLayout", "Warning", "No book directory is currently loaded.");
                StatusText = "No book directory is currently loaded.";
                return;
            }

            _config.StartLayout = BookProjectPath;
            _config.Save(_configPath);
            _startupPagePath = BookProjectPath;
            StatusText = $"Start directory set to: {BookProjectPath}";
            OnPropertyChanged(nameof(IsCurrentLayoutStartLayout));
            OnPropertyChanged(nameof(StartLayoutIconColor));
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentLayoutFilePath) || !File.Exists(_currentLayoutFilePath))
        {
            AddMessage("StartLayout", "Warning", "No layout file is currently loaded.");
            StatusText = "No layout file is currently loaded.";
            return;
        }

        _config.StartLayout = _currentLayoutFilePath;
        _config.Save(_configPath);
        _startupPagePath = _currentLayoutFilePath;
        StatusText = $"Start layout set to: {_currentLayoutFilePath}";
        OnPropertyChanged(nameof(IsCurrentLayoutStartLayout));
        OnPropertyChanged(nameof(StartLayoutIconColor));
    }

    public void SaveCurrentLayout()
    {
        // Einzel-Layout: Standardverhalten behalten.
        if (!IsDirectoryBook)
        {
            SaveLayout();
            return;
        }

        // Verzeichnis-Modus: alle Pages speichern, während der FileWatcher pausiert ist.
        StopBookWatcher();
        try
        {
            var pagesToSave = Pages
                .Where(page => !string.IsNullOrWhiteSpace(page.UiFilePath))
                .ToList();

            if (pagesToSave.Count == 0)
            {
                SaveLayout();
                return;
            }

            var originalSelectedPage = SelectedPage;

            foreach (var page in pagesToSave)
            {
                SelectedPage = page;
                SaveLayout();
            }

            if (originalSelectedPage is not null)
            {
                SelectedPage = originalSelectedPage;
            }

            StatusText = $"Saved {pagesToSave.Count} pages in directory: {BookProjectPath}";
        }
        finally
        {
            if (Directory.Exists(BookProjectPath))
            {
                StartBookWatcher(BookProjectPath);
            }
        }
    }

    public void SaveCurrentLayoutAs(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        // Einzel-Layout: bisheriges file-basiertes SaveAs-Verhalten beibehalten.
        if (!IsDirectoryBook)
        {
            // Ensure current in-memory layout is persisted to its backing file first.
            SaveLayout();

            var sourceLayoutPath = _currentLayoutFilePath;
            if (string.IsNullOrWhiteSpace(sourceLayoutPath))
            {
                sourceLayoutPath = SelectedPage.UiFilePath ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(sourceLayoutPath) || !File.Exists(sourceLayoutPath))
            {
                AddMessage("Save", "Warning", "No existing layout file to save from.");
                StatusText = "No existing layout file to save.";
                return;
            }

            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(sourceLayoutPath, targetPath, true);

            // Switch the current layout to the new file so further saves go there.
            LoadLayoutFromFile(targetPath);
            StatusText = $"Layout saved as: {targetPath}";
            return;
        }

        // Verzeichnis-Modus: kompletten Buch-Ordner kopieren und neue Kopie laden.
        // Wichtig: Hier kein zusätzliches Book.json erzeugen – es wird nur
        // der aktuelle Ordnerzustand kopiert.
        var sourceRoot = BookProjectPath;
        if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
        {
            AddMessage("SaveAs", "Warning", "No book directory to copy.");
            StatusText = "No book directory to copy.";
            return;
        }

        try
        {
            StopBookWatcher();

            CopyDirectory(sourceRoot, targetPath);

            // Laufzeit vor Umschalten der Buchinstanz stoppen.
            TasksManager.StopAll();
            ThreadsManager.StopAll();
            TimerManager.StopAll();

            BookProjectPath = targetPath;
            ResetMessages();
            LoadBookFromDirectory(targetPath);
            StatusText = $"Book saved as: {targetPath}";
        }
        catch (Exception ex)
        {
            AddMessage("SaveAs", "Error", ex.Message, targetPath);
            StatusText = $"Save As failed: {ex.Message}";
        }
    }

    public void RefreshLog()
    {
        var entries = HostLogger.ProcessLog.GetEntries();
        if (entries.Count == 0)
        {
            CurrentLogText = "No log entries yet.";
            OnPropertyChanged(nameof(CurrentLogFilePath));
            return;
        }

        CurrentLogText = string.Join(Environment.NewLine, entries.Select(FormatLogEntry));
        OnPropertyChanged(nameof(CurrentLogFilePath));
    }

    public void ApplyDestroyedUi(BookProject? project)
    {
        var pages = project is not null
            ? project.Pages.Select((page, index) => new PageModel
            {
                Index = index + 1,
                Name = string.IsNullOrWhiteSpace(page.Name) ? $"Page{index + 1}" : page.Name
            }).ToList()
            : Pages.Select(page => new PageModel
            {
                Index = page.Index,
                Name = page.Name
            }).ToList();

        if (pages.Count > 0)
        {
            SetPages(pages);
        }

        StatusText = "Runtime stopped. Canvas cleared.";
    }

    public void ApplyRunningUi(BookProject project)
    {
        ApplyBookTabStripPlacement(project.RootDirectory);
        SetPages(CreatePagesFromBook(project));
        BookProjectPath = project.RootDirectory;
        LoadedBookSummary = $"{project.ProjectName} | Pages: {project.Pages.Count} | C#: {project.SourceFiles.Count} | UI: {project.UiFiles.Count}";
        StatusText = $"Runtime started: {project.ProjectName}";
    }

    private bool CanRunBookAction()
        => !IsBookOperationRunning && !string.IsNullOrWhiteSpace(BookProjectPath);

    private void OnHostLogEntryAdded(ProcessLogEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var formatted = FormatLogEntry(entry);
            CurrentLogText = string.IsNullOrWhiteSpace(CurrentLogText) || string.Equals(CurrentLogText, "Noch kein Log vorhanden.", StringComparison.Ordinal)
                ? formatted
                : $"{CurrentLogText}{Environment.NewLine}{formatted}";
        });
    }

    private static string FormatLogEntry(ProcessLogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Exception))
        {
            return $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] {entry.Message}";
        }

        return $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] {entry.Message}{Environment.NewLine}{entry.Exception}";
    }

    private void ResetMessages()
    {
        Messages.Clear();
        MessagesSummary = "Keine Meldungen";
    }

    private static void EnsureDefaultLayout(string directory, string pagePath)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

                        if (File.Exists(pagePath))
                        {
                                return;
                        }

                        var content = "{\n" +
                                                    "  \"Page\": \"DefaultPage\",\n" +
                                                    "  \"Title\": \"Default Layout\",\n" +
                                                    "  \"Layout\": {\n" +
                                                    "    \"Type\": \"Canvas\",\n" +
                                                    "    \"Children\": []\n" +
                                                    "  }\n" +
                                                    "}\n";

                        File.WriteAllText(pagePath, content);
        }
        catch
        {
            // Best-effort only; falls die DefaultLayout-Erzeugung fehlschlägt,
            // startet UdlBook trotzdem, meldet aber später fehlende Layouts.
        }
    }

                private static void CopyDirectory(string sourceDirectory, string targetDirectory)
                {
                    var source = Path.GetFullPath(sourceDirectory);
                    var target = Path.GetFullPath(targetDirectory);

                    if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    Directory.CreateDirectory(target);

                    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(source, file);
                        var destinationPath = Path.Combine(target, relativePath);
                        var destinationDir = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrWhiteSpace(destinationDir))
                        {
                            Directory.CreateDirectory(destinationDir);
                        }

                        File.Copy(file, destinationPath, true);
                    }
                }

    private void LoadStartupPage()
    {
        ResetMessages();

        // If the configured start layout points to a directory, treat it as a book directory.
        if (Directory.Exists(_startupPagePath))
        {
            LoadBookFromDirectory(_startupPagePath);
            return;
        }

        if (!File.Exists(_startupPagePath))
        {
            LoadedBookSummary = "Default layout missing";
            AddMessage("UI", "Warning", "Startup-Page.json nicht gefunden.", _startupPagePath);
            StatusText = $"No startup page found: {_startupPagePath}";
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = "UdlBook";
            return;
        }

        try
        {
            var layout = BookUiLayoutLoader.Load(_startupPagePath, "UdlClient");
            var pageName = string.IsNullOrWhiteSpace(layout.PageName) ? "UdlClient" : layout.PageName;
            var model = CreatePageModelFromLayout(_startupPagePath, layout, 1, pageName);

            ApplyBookTabStripPlacement(Path.GetDirectoryName(_startupPagePath) ?? AppContext.BaseDirectory);
            SetPages([model]);
            BookProjectPath = Path.GetDirectoryName(_startupPagePath) ?? AppContext.BaseDirectory;
            _currentLayoutFilePath = _startupPagePath;
            HasLayout = true;
            IsDefaultLayout = string.Equals(_startupPagePath, _defaultLayoutPath, StringComparison.OrdinalIgnoreCase);
            HeaderTitle = Path.GetFileNameWithoutExtension(_startupPagePath) ?? "UdlBook";
            LoadedBookSummary = $"Default layout | {Path.GetFileName(_startupPagePath)}";
            StatusText = $"Default layout loaded: {_startupPagePath}";
            OnPropertyChanged(nameof(IsCurrentLayoutStartLayout));
            OnPropertyChanged(nameof(StartLayoutIconColor));
        }
        catch (Exception ex)
        {
            LoadedBookSummary = "Default layout invalid";
            AddMessage("UI", "Error", ex.Message, _startupPagePath);
            StatusText = $"Default layout could not be loaded: {ex.Message}";
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = "UdlBook";
        }
    }

    public void LoadLayoutFromFile(string uiFilePath)
    {
        ResetMessages();

        if (!File.Exists(uiFilePath))
        {
            LoadedBookSummary = "Layout file missing";
            AddMessage("UI", "Warning", "Layout file not found.", uiFilePath);
            StatusText = $"No layout file found: {uiFilePath}";
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = "UdlBook";
            return;
        }

        try
        {
            var fallbackName = Path.GetFileNameWithoutExtension(uiFilePath);
            var layout = BookUiLayoutLoader.Load(uiFilePath, fallbackName);
            var pageName = string.IsNullOrWhiteSpace(layout.PageName) ? fallbackName : layout.PageName;
            var model = CreatePageModelFromLayout(uiFilePath, layout, 1, pageName);

            var rootDirectory = Path.GetDirectoryName(uiFilePath) ?? AppContext.BaseDirectory;
            ApplyBookTabStripPlacement(rootDirectory);
            SetPages([model]);
            BookProjectPath = rootDirectory;
            _currentLayoutFilePath = uiFilePath;
            HasLayout = true;
            IsDefaultLayout = string.Equals(uiFilePath, _defaultLayoutPath, StringComparison.OrdinalIgnoreCase);
            HeaderTitle = Path.GetFileNameWithoutExtension(uiFilePath) ?? "UdlBook";
            LoadedBookSummary = $"Layout | {Path.GetFileName(uiFilePath)}";
            StatusText = $"Layout loaded: {uiFilePath}";
            OnPropertyChanged(nameof(IsCurrentLayoutStartLayout));
            OnPropertyChanged(nameof(StartLayoutIconColor));
        }
        catch (Exception ex)
        {
            LoadedBookSummary = "Layout invalid";
            AddMessage("UI", "Error", ex.Message, uiFilePath);
            StatusText = $"Layout could not be loaded: {ex.Message}";
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = "UdlBook";
        }
    }

    private void AddMessage(string source, string severity, string message, string? location = null)
    {
        Messages.Add(new HostMessageEntry
        {
            Source = source,
            Severity = severity,
            Message = message,
            Location = location ?? string.Empty
        });

        MessagesSummary = $"{Messages.Count} Meldungen";
    }

    private void LoadBookFromDirectory(string directoryPath)
    {
        try
        {
            var fullDirectory = Path.GetFullPath(directoryPath);
            if (!Directory.Exists(fullDirectory))
            {
                LoadedBookSummary = "Book directory not found";
                AddMessage("Load", "Warning", "Book directory not found.", fullDirectory);
                StatusText = $"No book directory found: {fullDirectory}";
                HasLayout = false;
                IsDefaultLayout = false;
                HeaderTitle = "UdlBook";
                return;
            }

            StopBookWatcher();
            _watchedPages.Clear();

            var jsonFiles = Directory.GetFiles(fullDirectory, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var fallbackName = Path.GetFileNameWithoutExtension(filePath);
                    var layout = BookUiLayoutLoader.Load(filePath, fallbackName);
                    var pageName = string.IsNullOrWhiteSpace(layout.PageName) ? fallbackName : layout.PageName;
                    var pageIndex = GetPageIndex(layout) ?? int.MaxValue;
                    var watchedPage = new WatchedPage
                    {
                        FilePath = filePath,
                        PageIndex = pageIndex,
                        PageName = pageName,
                        Layout = layout
                    };

                    _watchedPages[filePath] = watchedPage;
                }
                catch (Exception ex)
                {
                    AddMessage("UI", "Error", ex.Message, filePath);
                }
            }

            UpdatePagesFromWatchedPages(fullDirectory);
            StartBookWatcher(fullDirectory);
        }
        catch (Exception ex)
        {
            LoadedBookSummary = "Book could not be loaded";
            AddMessage("Load", "Error", ex.Message, directoryPath);
            StatusText = $"Load failed: {ex.Message}";
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = "UdlBook";
        }
    }

    private async void LoadBook()
    {
        if (!CanRunBookAction())
        {
            return;
        }

        IsBookOperationRunning = true;
        ResetMessages();
        try
        {
            if (Directory.Exists(BookProjectPath))
            {
                LoadBookFromDirectory(BookProjectPath);
                BookProjectPath = Path.GetFullPath(BookProjectPath);
                LoadedBookSummary = $"Directory book | {BookProjectPath}";
                StatusText = $"Book directory loaded: {BookProjectPath}";
                return;
            }

            if (!File.Exists(BookProjectPath))
            {
                LoadedBookSummary = "Book definition not found";
                AddMessage("Load", "Warning", "Book definition file not found.", BookProjectPath);
                StatusText = $"No book definition found: {BookProjectPath}";
                return;
            }

            var definitionPath = Path.GetFullPath(BookProjectPath);
            var definitionDirectory = Path.GetDirectoryName(definitionPath) ?? AppContext.BaseDirectory;
            string? layoutPath = null;

            try
            {
                var jsonText = File.ReadAllText(definitionPath);
                var json = JsonNode.Parse(jsonText) as JsonObject;

                if (json is not null)
                {
                    // 1) If the file itself contains a "Layout" node, treat it as a self-contained layout file.
                    if (json["Layout"] is not null)
                    {
                        layoutPath = definitionPath;
                    }
                    else
                    {
                        // 2) Otherwise, look for explicit layout references.
                        layoutPath = (string?)json["LayoutFile"]
                            ?? (string?)json["UiFile"];

                        if (!string.IsNullOrWhiteSpace(layoutPath))
                        {
                            layoutPath = Path.GetFullPath(Path.Combine(definitionDirectory, layoutPath));
                        }
                        else
                        {
                            // 3) Heuristic for simple Book.json-style manifests:
                            //    Prefer Page.json next to the definition, or a single other *.json.
                            var candidate = Path.Combine(definitionDirectory, "Page.json");
                            if (File.Exists(candidate))
                            {
                                layoutPath = candidate;
                            }
                            else
                            {
                                var jsonFiles = Directory.GetFiles(definitionDirectory, "*.json");
                                var otherJsons = jsonFiles
                                    .Where(path => !string.Equals(path, definitionPath, StringComparison.OrdinalIgnoreCase))
                                    .ToArray();

                                if (otherJsons.Length == 1)
                                {
                                    layoutPath = otherJsons[0];
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // If parsing fails, treat the definition file itself as layout.
                AddMessage("Load", "Warning", ex.Message, definitionPath);
            }

            layoutPath ??= definitionPath;
            LoadLayoutFromFile(layoutPath);

            BookProjectPath = definitionDirectory;
            LoadedBookSummary = $"Book | {Path.GetFileName(definitionPath)}";
            StatusText = $"Book loaded: {definitionPath}";
        }
        catch (Exception ex)
        {
            LoadedBookSummary = "Book could not be loaded";
            AddMessage("Load", "Error", ex.Message);
            StatusText = $"Load failed: {ex.Message}";
        }
        finally
        {
            RefreshLog();
            IsBookOperationRunning = false;
        }
    }

    private async void RebuildBook()
    {
        if (!CanRunBookAction())
        {
            return;
        }

        IsBookOperationRunning = true;
        ResetMessages();
        try
        {
            if (string.IsNullOrWhiteSpace(_currentLayoutFilePath))
            {
                StatusText = "No layout to reload.";
                return;
            }

            LoadLayoutFromFile(_currentLayoutFilePath);
            StatusText = $"Layout reloaded: {_currentLayoutFilePath}";
        }
        catch (Exception ex)
        {
            AddMessage("Build", "Error", ex.Message);
            StatusText = $"Rebuild failed: {ex.Message}";
        }
        finally
        {
            RefreshLog();
            IsBookOperationRunning = false;
        }
    }

    private IReadOnlyList<PageModel> CreatePagesFromBook(BookProject project)
    {
        var pages = project.Pages
            .Select((page, index) => CreatePageFromBook(page, index + 1))
            .ToList();

        if (pages.Count == 0)
        {
            pages.Add(new PageModel
            {
                Index = 1,
                Name = project.ProjectName
            });
        }

        return pages;
    }

    private PageModel CreatePageFromBook(BookProjectPage page, int index)
    {
        var pageName = string.IsNullOrWhiteSpace(page.Name) ? $"Page{index}" : page.Name;
        var pageDisplayText = GetPageDisplayText(page) ?? pageName;

        if (string.IsNullOrWhiteSpace(page.UiFile) || !File.Exists(page.UiFile))
        {
            var fallbackModel = new PageModel
            {
                Index = index,
                Name = pageName,
                DisplayText = pageDisplayText,
                UiFilePath = page.UiFile
            };

            fallbackModel.Items.Add(CreateFallbackItem(page.Name, "No Page.json found"));
            return fallbackModel;
        }

        try
        {
            var layout = BookUiLayoutLoader.Load(page.UiFile, page.Name);
            var model = new PageModel
            {
                Index = index,
                Name = pageName,
                DisplayText = pageDisplayText,
                UiFilePath = page.UiFile,
                UiLayoutDefinition = layout
            };

            var items = CreateItemsFromNode(pageName, layout.Layout, 48, 48).ToList();

            foreach (var item in items)
            {
                model.Items.Add(item);
            }

            return model;
        }
        catch (Exception ex)
        {
            AddMessage("UI", "Error", ex.Message, page.UiFile);
            var fallbackModel = new PageModel
            {
                Index = index,
                Name = pageName,
                DisplayText = pageDisplayText,
                UiFilePath = page.UiFile
            };
            fallbackModel.Items.Add(CreateFallbackItem(page.Name, $"UI error: {ex.Message}"));
            return fallbackModel;
        }
    }

    private IEnumerable<PageItemModel> CreateItemsFromNode(string pageName, BookUiNode node, double defaultX, double defaultY)
    {
        if (IsContainerNode(node))
        {
            var x = node.X ?? defaultX;
            var y = node.Y ?? defaultY;
            var spacing = node.Spacing ?? 12d;
            var nextY = y;

            foreach (var child in node.Children)
            {
                foreach (var item in CreateItemsFromNode(pageName, child, x, nextY))
                {
                    yield return item;
                    nextY = Math.Max(nextY, item.Y + item.Height + spacing);
                }
            }

            yield break;
        }

        yield return CreateItemFromUiNode(pageName, node, defaultX, defaultY);
    }

    private PageItemModel CreateItemFromUiNode(string pageName, BookUiNode node, double defaultX, double defaultY)
    {
        var type = node.Type;
        var text = string.IsNullOrWhiteSpace(node.Text) ? type : node.Text;
        var kind = GetControlKindFromUiType(type);
        var isButton = kind == ControlKind.Button;
        var isListControl = kind == ControlKind.ListControl;
        var isChartControl = kind == ControlKind.ChartControl;
        var item = new PageItemModel
        {
            Kind = kind,
            Name = text,
            BodyCaption = text,
            ControlCaption = pageName,
            Footer = isButton ? "Action" : type,
            X = node.X ?? defaultX,
            Y = node.Y ?? defaultY,
            Width = node.Width ?? (isButton ? 320 : (kind == ControlKind.LogControl ? 420 : (isChartControl ? 520 : 260))),
            Height = node.Height ?? (isButton ? 96 : (kind == ControlKind.LogControl ? 260 : (isChartControl ? 260 : (isListControl ? 220 : 84)))),
            IsAutoHeight = isListControl,
            UiNodeType = string.IsNullOrWhiteSpace(type) ? GetDefaultUiType(kind) : type,
            UiProperties = CloneJsonObject(node.Properties)
        };

        ApplyKnownUiProperties(item, node.Properties, pageName, type);
        item.SetHierarchy(pageName, null);

        foreach (var childNode in node.Children)
        {
            var childItems = CreateItemsFromNode(pageName, childNode, 0, 0).ToList();
            foreach (var childItem in childItems)
            {
                if (item.IsListControl)
                {
                    item.AttachChildToList(childItem);
                }
                else
                {
                    childItem.SetHierarchy(pageName, item);
                    item.Items.Add(childItem);
                }
            }
        }

        item.ApplyTheme(IsDarkTheme);
        item.SyncChildWidths();
        item.ApplyListHeightRules();
        return item;
    }

    private static bool IsContainerNode(BookUiNode node)
    {
        return string.Equals(node.Type, "StackPanel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.Type, "Canvas", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(node.Type);
    }

    private static ControlKind GetControlKindFromUiType(string? type)
    {
        if (string.Equals(type, "Button", StringComparison.OrdinalIgnoreCase))
        {
            return ControlKind.Button;
        }

        if (string.Equals(type, "ListControl", StringComparison.OrdinalIgnoreCase))
        {
            return ControlKind.ListControl;
        }

        if (string.Equals(type, "LogControl", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "ProcessLog", StringComparison.OrdinalIgnoreCase))
        {
            return ControlKind.LogControl;
        }

        if (string.Equals(type, "ChartControl", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "Chart", StringComparison.OrdinalIgnoreCase))
        {
            return ControlKind.ChartControl;
        }

        if (string.Equals(type, "UdlClientControl", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "UdlClient", StringComparison.OrdinalIgnoreCase))
        {
            return ControlKind.UdlClientControl;
        }

        return ControlKind.Item;
    }

    private PageItemModel CreateFallbackItem(string pageName, string message)
    {
        var item = new PageItemModel
        {
            Kind = ControlKind.Item,
            Name = message,
            BodyCaption = message,
            ControlCaption = pageName,
            Footer = "Book",
            X = 48,
            Y = 48,
            Width = 320,
            Height = 84
        };

        item.SetHierarchy(pageName, null);
        item.ApplyTheme(IsDarkTheme);
        return item;
    }

    private static int? GetPageIndex(BookUiPageLayout layout)
    {
        try
        {
            if (layout.DocumentProperties.TryGetPropertyValue("PageIndex", out var value) && value is not null)
            {
                if (value is JsonValue jsonValue)
                {
                    if (jsonValue.TryGetValue<int>(out var intValue))
                    {
                        return intValue;
                    }

                    if (jsonValue.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed))
                    {
                        return parsed;
                    }
                }
            }
        }
        catch
        {
            // Ignore PageIndex parsing errors; fall back to default ordering.
        }

        return null;
    }

    private PageModel CreatePageModelFromLayout(string uiFilePath, BookUiPageLayout layout, int index, string pageName)
    {
        var model = new PageModel
        {
            Index = index,
            Name = pageName,
            DisplayText = string.IsNullOrWhiteSpace(layout.Title) ? pageName : layout.Title,
            UiFilePath = uiFilePath,
            UiLayoutDefinition = layout
        };

        foreach (var item in CreateItemsFromNode(pageName, layout.Layout, 24, 24))
        {
            model.Items.Add(item);
        }

        return model;
    }

    private void StartBookWatcher(string directory)
    {
        try
        {
            var watcher = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = false,
                Filter = "*.json",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            watcher.Created += OnBookFileChanged;
            watcher.Changed += OnBookFileChanged;
            watcher.Deleted += OnBookFileDeleted;
            watcher.Renamed += OnBookFileRenamed;
            watcher.EnableRaisingEvents = true;

            _bookWatcher = watcher;
        }
        catch (Exception ex)
        {
            AddMessage("Watch", "Error", ex.Message, directory);
        }
    }

    private void StopBookWatcher()
    {
        if (_bookWatcher is null)
        {
            return;
        }

        try
        {
            _bookWatcher.EnableRaisingEvents = false;
            _bookWatcher.Created -= OnBookFileChanged;
            _bookWatcher.Changed -= OnBookFileChanged;
            _bookWatcher.Deleted -= OnBookFileDeleted;
            _bookWatcher.Renamed -= OnBookFileRenamed;
            _bookWatcher.Dispose();
        }
        catch
        {
            // Ignore dispose errors; watcher is best-effort.
        }
        finally
        {
            _bookWatcher = null;
        }
    }

    private void OnBookFileChanged(object sender, FileSystemEventArgs e)
    {
        // Normalize to full path for mapping consistency.
        var fullPath = Path.GetFullPath(e.FullPath);

        Dispatcher.UIThread.Post(() =>
        {
            if (e.ChangeType is WatcherChangeTypes.Created or WatcherChangeTypes.Changed)
            {
                HandleBookFileUpsert(fullPath);
            }
        });
    }

    private void OnBookFileDeleted(object sender, FileSystemEventArgs e)
    {
        var fullPath = Path.GetFullPath(e.FullPath);

        Dispatcher.UIThread.Post(() =>
        {
            if (_watchedPages.Remove(fullPath))
            {
                UpdatePagesFromWatchedPages(BookProjectPath);
            }
        });
    }

    private void OnBookFileRenamed(object sender, RenamedEventArgs e)
    {
        var oldFullPath = Path.GetFullPath(e.OldFullPath);
        var newFullPath = Path.GetFullPath(e.FullPath);

        Dispatcher.UIThread.Post(() =>
        {
            _watchedPages.Remove(oldFullPath);
            HandleBookFileUpsert(newFullPath);
        });
    }

    private void HandleBookFileUpsert(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        try
        {
            var fallbackName = Path.GetFileNameWithoutExtension(filePath);
            var layout = BookUiLayoutLoader.Load(filePath, fallbackName);
            var pageName = string.IsNullOrWhiteSpace(layout.PageName) ? fallbackName : layout.PageName;
            var pageIndex = GetPageIndex(layout) ?? int.MaxValue;
            var watchedPage = new WatchedPage
            {
                FilePath = filePath,
                PageIndex = pageIndex,
                PageName = pageName,
                Layout = layout
            };

            _watchedPages[filePath] = watchedPage;
            UpdatePagesFromWatchedPages(BookProjectPath);
        }
        catch (Exception ex)
        {
            AddMessage("UI", "Error", ex.Message, filePath);
        }
    }

    private void UpdatePagesFromWatchedPages(string directory)
    {
        var orderedPages = _watchedPages
            .Values
            .OrderBy(page => page.PageIndex)
            .ThenBy(page => page.PageName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(page => page.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (orderedPages.Count == 0)
        {
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = "UdlBook";
            SetPages(Array.Empty<PageModel>());
            LoadedBookSummary = "No pages found";
            StatusText = $"No pages found in directory: {directory}";
            return;
        }

        var pages = new List<PageModel>(orderedPages.Count);
        for (var i = 0; i < orderedPages.Count; i++)
        {
            var watched = orderedPages[i];
            var model = CreatePageModelFromLayout(watched.FilePath, watched.Layout, i + 1, watched.PageName);
            pages.Add(model);
        }

        ApplyBookTabStripPlacement(directory);
        SetPages(pages);
        BookProjectPath = directory;
        _currentLayoutFilePath = orderedPages[0].FilePath;
        HasLayout = true;
        IsDefaultLayout = false;
        HeaderTitle = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "UdlBook";
        LoadedBookSummary = $"Directory book | Pages: {pages.Count}";
        StatusText = $"Directory loaded: {directory}";
        OnPropertyChanged(nameof(IsCurrentLayoutStartLayout));
        OnPropertyChanged(nameof(StartLayoutIconColor));
    }

}