using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Amium.Host;
using Amium.Logging;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;
using System.Text.Json.Nodes;

namespace AutomationExplorer.ViewModels;

public sealed class MainWindowViewModel : Amium.UiEditor.ViewModels.MainWindowViewModel
{
    private const string ProjectEntryFileName = "Project.aaep";
    private const string LegacyProjectEntryFileName = "Project.udlb";
    private const string OlderLegacyProjectEntryFileName = "Book.udlb";
    private const string FoldersDirectoryName = "Folders";
    private const string LegacyFoldersDirectoryName = "Pages";
    private const string FolderLayoutFileName = "Folder.yaml";
    private const string LegacyFolderLayoutFileName = "Page.yaml";
    private const string FolderMetadataFileName = "Folder.meta.yaml";
    private const string LegacyFolderMetadataFileName = "Page.meta.yaml";
    private const string StructuredLayoutWatcherFilter = "*.yaml";
    private const string ScriptsDirectoryName = "Scripts";
    private const string AssetsDirectoryName = "Assets";
    private readonly string _configPath;
    private readonly string _defaultLayoutPath;
    private readonly Dictionary<string, WatchedPage> _watchedPages = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _watcherSuppressedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _watcherUpsertsInProgress = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _bookWatcher;
    private readonly AutomationExplorerAppConfig _config;
    private string _startupPagePath;
    private string _currentLayoutFilePath = string.Empty;
    private string _currentBookEntryPath = string.Empty;
    private string _bookProjectPath;
    private string _loadedBookSummary;
    private string _messagesSummary;
    private string _currentLogText;
    private string _headerTitle = "AutomationExplorer";
    private bool _hasLayout;
    private bool _isDefaultLayout;
    private bool _isBookOperationRunning;
    private bool _isStructuredBook;

    public MainWindowViewModel()
        : base(true)
    {
        AutoSaveOnEditModeExit = false;
        _configPath = Path.Combine(AppContext.BaseDirectory, "AutomationExplorer.config.yaml");
        _config = AutomationExplorerAppConfig.Load(_configPath);

        var defaultLayoutDirectory = Path.Combine(AppContext.BaseDirectory, "DefaultLayout");
        _defaultLayoutPath = Path.Combine(defaultLayoutDirectory, FolderLayoutFileName);
        EnsureDefaultLayout(defaultLayoutDirectory, _defaultLayoutPath);
        // Always start in Dark theme on app startup.
        IsDarkTheme = true;
        _config.DefaultTheme = "Dark";
        _config.Save(_configPath);

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
        LoadProjectCommand = new Amium.UiEditor.ViewModels.RelayCommand(LoadBook, CanRunBookAction);
        RebuildProjectCommand = new Amium.UiEditor.ViewModels.RelayCommand(RebuildBook, CanRunBookAction);
        RefreshLogCommand = new Amium.UiEditor.ViewModels.RelayCommand(RefreshLog);

        _bookProjectPath = ResolveInitialBookProjectPath(_startupPagePath);
        _loadedBookSummary = "No project loaded";
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
        public ProjectFolderLayout Layout { get; set; } = default!;
    }

    protected override string? CurrentProjectRootDirectory => ProjectPath;

    public string ProjectPath
    {
        get => _bookProjectPath;
        set
        {
            if (SetProperty(ref _bookProjectPath, value))
            {
                RefreshCurrentFolderWorkspacePath();
                LoadProjectCommand.RaiseCanExecuteChanged();
                RebuildProjectCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string LoadedProjectSummary
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
                    if (string.IsNullOrWhiteSpace(ProjectPath))
                    {
                        return false;
                    }

                    var currentDirectory = Path.GetFullPath(ProjectPath);
                    if (string.Equals(currentDirectory, start, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (!string.IsNullOrWhiteSpace(_currentBookEntryPath))
                    {
                        var currentEntry = Path.GetFullPath(_currentBookEntryPath);
                        return string.Equals(currentEntry, start, StringComparison.OrdinalIgnoreCase);
                    }

                    return false;
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
                LoadProjectCommand.RaiseCanExecuteChanged();
                RebuildProjectCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string LegendEditIconColor => IsEditMode ? "#DC2626" : TabSelectForeColor;

    public Amium.UiEditor.ViewModels.RelayCommand LoadProjectCommand { get; }

    public Amium.UiEditor.ViewModels.RelayCommand RebuildProjectCommand { get; }

    public Amium.UiEditor.ViewModels.RelayCommand RefreshLogCommand { get; }

    public Func<string, string, Task<string?>>? ResolveDuplicatePageNameAsync { get; set; }

    public bool IsDirectoryBook => _isStructuredBook || _watchedPages.Count > 0;

    public bool CreateNewBook(string bookEntryPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(bookEntryPath))
        {
            errorMessage = "No target .aaep file selected.";
            StatusText = errorMessage;
            return false;
        }

        var normalizedEntryPath = NormalizeBookEntryPath(bookEntryPath);
        var rootDirectory = Path.GetDirectoryName(Path.GetFullPath(normalizedEntryPath));
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            errorMessage = "The selected .aaep file must be inside a valid directory.";
            StatusText = errorMessage;
            return false;
        }

        try
        {
            Directory.CreateDirectory(rootDirectory);
            Directory.CreateDirectory(GetPagesDirectoryPath(rootDirectory));
            EnsureBookEntryFile(normalizedEntryPath, rootDirectory);

            var firstPageDirectory = Path.Combine(GetPagesDirectoryPath(rootDirectory), "Folder1");
            var firstPageLayoutPath = GetPageLayoutPath(firstPageDirectory);
            if (!File.Exists(firstPageLayoutPath))
            {
                CreatePageDirectoryStructure(firstPageDirectory);
                File.WriteAllText(firstPageLayoutPath, BuildNewPageYaml("Folder1", 1));
            }

            LoadStructuredBookFromEntryFile(normalizedEntryPath);
            StatusText = $"Project created: {normalizedEntryPath}";
            return true;
        }
        catch (Exception ex)
        {
            AddMessage("CreateBook", "Error", ex.Message, normalizedEntryPath);
            errorMessage = $"Could not create project: {ex.Message}";
            StatusText = errorMessage;
            return false;
        }
    }

    public bool TryCreateNewPage(string rawPageName, out string createdFilePath, out string errorMessage)
    {
        createdFilePath = string.Empty;
        errorMessage = string.Empty;

        var trimmedName = rawPageName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            errorMessage = "Folder name must not be empty.";
            StatusText = errorMessage;
            return false;
        }

        if (Folders.Any(page => string.Equals(page.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            errorMessage = $"A folder named '{trimmedName}' already exists.";
            StatusText = errorMessage;
            return false;
        }

        var targetDirectory = string.IsNullOrWhiteSpace(ProjectPath)
            ? string.Empty
            : Path.GetFullPath(ProjectPath);

        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
        {
            errorMessage = "No valid project directory loaded.";
            StatusText = errorMessage;
            return false;
        }

        if (!IsDirectoryBook)
        {
            errorMessage = "New folders are only supported for loaded project directories.";
            StatusText = errorMessage;
            return false;
        }

        var safeDirectoryName = BuildSafePageDirectoryName(trimmedName);
        if (string.IsNullOrWhiteSpace(safeDirectoryName))
        {
            errorMessage = "Folder name contains no valid directory name characters.";
            StatusText = errorMessage;
            return false;
        }

        var pagesDirectory = GetPagesDirectoryPath(targetDirectory);
        Directory.CreateDirectory(pagesDirectory);

        var pageDirectory = Path.Combine(pagesDirectory, safeDirectoryName);
        var fullPath = GetPageLayoutPath(pageDirectory);
        if (Directory.Exists(pageDirectory) || File.Exists(fullPath))
        {
            errorMessage = $"A folder directory named '{safeDirectoryName}' already exists.";
            StatusText = errorMessage;
            return false;
        }

        var pageIndex = Folders.Count + 1;
        var yamlContent = BuildNewPageYaml(trimmedName, pageIndex);

        try
        {
            CreatePageDirectoryStructure(pageDirectory);
            File.WriteAllText(fullPath, yamlContent);
            EnsureBookEntryFile(GetBookEntryPath(targetDirectory), targetDirectory);
            LoadStructuredBookFromDirectory(targetDirectory, GetBookEntryPath(targetDirectory));

            var createdPage = Folders.FirstOrDefault(page => string.Equals(page.UiFilePath, fullPath, StringComparison.OrdinalIgnoreCase));
            if (createdPage is not null)
            {
                SelectedFolder = createdPage;
            }

            createdFilePath = fullPath;
            StatusText = $"New folder created: {Path.GetFileName(fullPath)}";
            return true;
        }
        catch (Exception ex)
        {
            AddMessage("CreatePage", "Error", ex.Message, fullPath);
            errorMessage = $"Could not create folder: {ex.Message}";
            StatusText = errorMessage;
            return false;
        }
    }

    public void SetCurrentLayoutAsStartup()
    {
        if (IsDirectoryBook)
        {
            if (string.IsNullOrWhiteSpace(ProjectPath) || !Directory.Exists(ProjectPath))
            {
                AddMessage("StartLayout", "Warning", "No project directory is currently loaded.");
                StatusText = "No project directory is currently loaded.";
                return;
            }

            var startPath = ProjectPath;
            if (_isStructuredBook)
            {
                startPath = string.IsNullOrWhiteSpace(_currentBookEntryPath)
                    ? GetBookEntryPath(ProjectPath)
                    : _currentBookEntryPath;

                EnsureBookEntryFile(startPath, ProjectPath);
                _currentBookEntryPath = startPath;
            }

            _config.StartLayout = startPath;
            _config.Save(_configPath);
            _startupPagePath = startPath;
            StatusText = _isStructuredBook
                ? $"Start project set to: {startPath}"
                : $"Start directory set to: {ProjectPath}";
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
        if (!IsDirectoryBook)
        {
            SaveLayout();
            return;
        }

        StopBookWatcher();
        try
        {
            var pagesToSave = Folders
                .Where(page => !string.IsNullOrWhiteSpace(page.UiFilePath))
                .ToList();

            if (pagesToSave.Count == 0)
            {
                SaveLayout();
                return;
            }

            var originalSelectedPage = SelectedFolder;

            foreach (var page in pagesToSave)
            {
                SelectedFolder = page;
                SaveLayout();
            }

            if (originalSelectedPage is not null)
            {
                SelectedFolder = originalSelectedPage;
            }

            StatusText = $"Saved {pagesToSave.Count} folders in directory: {ProjectPath}";
        }
        finally
        {
            RestartBookWatcher();
        }
    }

    public void SaveCurrentLayoutAs(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        if (!IsDirectoryBook)
        {
            SaveLayout();

            var sourceLayoutPath = _currentLayoutFilePath;
            if (string.IsNullOrWhiteSpace(sourceLayoutPath))
            {
                sourceLayoutPath = SelectedFolder.UiFilePath ?? string.Empty;
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

            LoadYamlLayoutFromFile(targetPath);
            StatusText = $"Layout saved as: {targetPath}";
            return;
        }

        var sourceRoot = ProjectPath;
        if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
        {
            AddMessage("SaveAs", "Warning", "No project directory to copy.");
            StatusText = "No project directory to copy.";
            return;
        }

        try
        {
            StopBookWatcher();

            var targetRoot = IsBookEntryPath(targetPath)
                ? Path.GetDirectoryName(Path.GetFullPath(targetPath)) ?? targetPath
                : Path.GetFullPath(targetPath);
            var targetEntryPath = IsBookEntryPath(targetPath)
                ? Path.GetFullPath(targetPath)
                : GetBookEntryPath(targetRoot);

            CopyDirectory(sourceRoot, targetRoot);
            EnsureBookEntryFile(targetEntryPath, targetRoot);

            TasksManager.StopAll();
            ThreadsManager.StopAll();
            TimerManager.StopAll();

            ProjectPath = targetRoot;
            ResetMessages();
            if (_isStructuredBook)
            {
                LoadStructuredBookFromEntryFile(targetEntryPath);
            }
            else
            {
                LoadYamlBookFromDirectory(targetRoot);
            }

            StatusText = $"Project saved as: {targetEntryPath}";
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

    public void ApplyDestroyedUi(ProjectModel? project)
    {
        var pages = project is not null
            ? project.Folders.Select((page, index) => new FolderModel
            {
                Index = index + 1,
                Name = string.IsNullOrWhiteSpace(page.Name) ? $"Folder{index + 1}" : page.Name
            }).ToList()
            : Folders.Select(page => new FolderModel
            {
                Index = page.Index,
                Name = page.Name
            }).ToList();

        if (pages.Count > 0)
        {
            SetFolders(pages);
        }

        StatusText = "Runtime stopped. Canvas cleared.";
    }

    public void ApplyRunningUi(ProjectModel project)
    {
        ApplyBookManifestSettings(project.RootDirectory);
        SetFolders(CreateFoldersFromProject(project));
        ProjectPath = project.RootDirectory;
        LoadedProjectSummary = $"{project.ProjectName} | Folders: {project.Folders.Count} | C#: {project.SourceFiles.Count} | UI: {project.UiFiles.Count}";
        StatusText = $"Runtime started: {project.ProjectName}";
    }

    private bool CanRunBookAction()
        => !IsBookOperationRunning && !string.IsNullOrWhiteSpace(ProjectPath);

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

                        var content = "Caption: 'Default Layout'\n" +
                                      "Views:\n" +
                                      "  1: 'HomeScreen'\n" +
                                      "Controls: []\n";

                        File.WriteAllText(pagePath, content);
        }
        catch
        {
            // Best-effort only; falls die DefaultLayout-Erzeugung fehlschlägt,
	    // startet AutomationExplorer trotzdem, meldet aber später fehlende Layouts.
        }
    }

    private static string ResolveInitialBookProjectPath(string startupPath)
    {
        if (string.IsNullOrWhiteSpace(startupPath))
        {
            return AppContext.BaseDirectory;
        }

        try
        {
            if (Directory.Exists(startupPath))
            {
                return Path.GetFullPath(startupPath);
            }

            if (File.Exists(startupPath))
            {
                return Path.GetDirectoryName(Path.GetFullPath(startupPath)) ?? AppContext.BaseDirectory;
            }
        }
        catch
        {
        }

        return Path.GetDirectoryName(startupPath) ?? AppContext.BaseDirectory;
    }

    private static string BuildSafePageDirectoryName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name
            .Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim();

        return sanitized.Trim('.');
    }

    private static string BuildNewPageYaml(string pageName, int pageIndex)
    {
        var escapedName = EscapeYamlSingleQuoted(pageName);
        return $"Folder: '{escapedName}'{Environment.NewLine}"
            + $"Caption: '{escapedName}'{Environment.NewLine}"
            + $"PageIndex: {Math.Max(1, pageIndex)}{Environment.NewLine}"
            + "Views:" + Environment.NewLine
            + "  1: 'HomeScreen'" + Environment.NewLine
            + "Controls: []" + Environment.NewLine;
    }

    private static string EscapeYamlSingleQuoted(string value)
        => (value ?? string.Empty).Replace("'", "''", StringComparison.Ordinal);

    private static bool IsBookEntryPath(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".aaep", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".udlb", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBookEntryPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            return GetBookEntryPath(fullPath);
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return fullPath;
        }

        return GetBookEntryPath(directory);
    }

    private static string GetBookEntryPath(string bookRootPath)
    {
        var projectEntryPath = Path.Combine(bookRootPath, ProjectEntryFileName);
        var legacyEntryPath = Path.Combine(bookRootPath, LegacyProjectEntryFileName);
        var olderLegacyEntryPath = Path.Combine(bookRootPath, OlderLegacyProjectEntryFileName);
        return File.Exists(projectEntryPath)
            ? projectEntryPath
            : (File.Exists(legacyEntryPath)
                ? legacyEntryPath
                : (File.Exists(olderLegacyEntryPath) ? olderLegacyEntryPath : projectEntryPath));
    }

    private static string GetPagesDirectoryPath(string bookRootPath)
    {
        var foldersPath = Path.Combine(bookRootPath, FoldersDirectoryName);
        var legacyFoldersPath = Path.Combine(bookRootPath, LegacyFoldersDirectoryName);
        return Directory.Exists(foldersPath) ? foldersPath : (Directory.Exists(legacyFoldersPath) ? legacyFoldersPath : foldersPath);
    }

    private static string GetPageLayoutPath(string pageDirectoryPath)
    {
        var folderLayoutPath = Path.Combine(pageDirectoryPath, FolderLayoutFileName);
        var legacyLayoutPath = Path.Combine(pageDirectoryPath, LegacyFolderLayoutFileName);
        return File.Exists(folderLayoutPath) ? folderLayoutPath : (File.Exists(legacyLayoutPath) ? legacyLayoutPath : folderLayoutPath);
    }

    private static string GetPageMetadataPath(string pageDirectoryPath)
    {
        var folderMetadataPath = Path.Combine(pageDirectoryPath, FolderMetadataFileName);
        var legacyMetadataPath = Path.Combine(pageDirectoryPath, LegacyFolderMetadataFileName);
        return File.Exists(folderMetadataPath) ? folderMetadataPath : (File.Exists(legacyMetadataPath) ? legacyMetadataPath : folderMetadataPath);
    }

    private static string GetPageNameFallback(string layoutPath)
    {
        var fileName = Path.GetFileName(layoutPath);
        if (string.Equals(fileName, FolderLayoutFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, LegacyFolderLayoutFileName, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(Path.GetDirectoryName(layoutPath)) ?? "Folder";
        }

        return Path.GetFileNameWithoutExtension(layoutPath);
    }

    private static string GetTechnicalPageName(string layoutPath)
        => GetPageNameFallback(layoutPath);

    private static string BuildBookEntryContent(string bookRootPath)
    {
        var bookName = Path.GetFileName(bookRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return $"# AutomationExplorer project entry{Environment.NewLine}"
            + $"FormatVersion: 1{Environment.NewLine}"
            + $"Project: '{EscapeYamlSingleQuoted(bookName)}'{Environment.NewLine}";
    }

    private static void EnsureBookEntryFile(string entryPath, string bookRootPath)
    {
        var entryDirectory = Path.GetDirectoryName(entryPath);
        if (!string.IsNullOrWhiteSpace(entryDirectory))
        {
            Directory.CreateDirectory(entryDirectory);
        }

        if (!File.Exists(entryPath))
        {
            File.WriteAllText(entryPath, BuildBookEntryContent(bookRootPath));
        }
    }

    private static void CreatePageDirectoryStructure(string pageDirectory)
    {
        Directory.CreateDirectory(pageDirectory);
        Directory.CreateDirectory(Path.Combine(pageDirectory, ScriptsDirectoryName));
        Directory.CreateDirectory(Path.Combine(pageDirectory, AssetsDirectoryName));
    }

    private void ValidatePageMetadataScripts(string pageDirectory)
    {
        var metadataPath = GetPageMetadataPath(pageDirectory);
        if (!File.Exists(metadataPath))
        {
            return;
        }

        try
        {
            foreach (var relativeScriptPath in ReadMetadataScriptReferences(metadataPath))
            {
                var fullScriptPath = Path.GetFullPath(Path.Combine(pageDirectory, relativeScriptPath.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(fullScriptPath))
                {
                    AddMessage("Load", "Warning", $"Referenced script not found: {relativeScriptPath}", metadataPath);
                }
            }
        }
        catch (Exception ex)
        {
            AddMessage("Load", "Warning", $"Page metadata could not be parsed: {ex.Message}", metadataPath);
        }
    }

    private static IReadOnlyList<string> ReadMetadataScriptReferences(string metadataPath)
    {
        var scriptPaths = new List<string>();
        var inScriptsSection = false;

        foreach (var rawLine in File.ReadLines(metadataPath))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var trimmedLine = rawLine.Trim();
            if (trimmedLine.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (!char.IsWhiteSpace(rawLine[0]))
            {
                inScriptsSection = string.Equals(trimmedLine, "Scripts:", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inScriptsSection || !trimmedLine.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            var value = trimmedLine[1..].Trim().Trim('"', '\'');
            if (!string.IsNullOrWhiteSpace(value))
            {
                scriptPaths.Add(value);
            }
        }

        return scriptPaths;
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

        if (Directory.Exists(_startupPagePath))
        {
            var entryPath = GetBookEntryPath(_startupPagePath);
            if (File.Exists(entryPath))
            {
                LoadStructuredBookFromEntryFile(entryPath);
            }
            else
            {
                LoadYamlBookFromDirectory(_startupPagePath);
            }
            return;
        }

        if (!File.Exists(_startupPagePath))
        {
            LoadedProjectSummary = "Default layout missing";
            AddMessage("UI", "Warning", "Startup-Folder.yaml nicht gefunden.", _startupPagePath);
            StatusText = $"No startup folder found: {_startupPagePath}";
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = "AutomationExplorer";
            return;
        }

        if (IsBookEntryPath(_startupPagePath))
        {
            LoadStructuredBookFromEntryFile(_startupPagePath);
            return;
        }

        try
        {
            var layout = ProjectUiLayoutLoader.LoadYaml(_startupPagePath, "UdlClient");
            var pageName = GetTechnicalPageName(_startupPagePath);
            var model = CreateFolderModelFromLayout(_startupPagePath, layout, 1, pageName);

            ApplyBookManifestSettings(Path.GetDirectoryName(_startupPagePath) ?? AppContext.BaseDirectory);
            SetFolders([model]);
            ProjectPath = Path.GetDirectoryName(_startupPagePath) ?? AppContext.BaseDirectory;
            _currentLayoutFilePath = _startupPagePath;
            _currentBookEntryPath = string.Empty;
            _isStructuredBook = false;
            HasLayout = true;
            IsDefaultLayout = string.Equals(_startupPagePath, _defaultLayoutPath, StringComparison.OrdinalIgnoreCase);
            HeaderTitle = Path.GetFileNameWithoutExtension(_startupPagePath) ?? "AutomationExplorer";
            LoadedProjectSummary = $"Default layout | {Path.GetFileName(_startupPagePath)}";
            StatusText = $"Default layout loaded: {_startupPagePath}";
            OnPropertyChanged(nameof(IsCurrentLayoutStartLayout));
            OnPropertyChanged(nameof(StartLayoutIconColor));
        }
        catch (Exception ex)
        {
            LoadedProjectSummary = "Default layout invalid";
            AddMessage("UI", "Error", ex.Message, _startupPagePath);
            StatusText = $"Default layout could not be loaded: {ex.Message}";
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = "AutomationExplorer";
        }
    }

    public void LoadLayoutFromFile(string uiFilePath)
    {
        LoadYamlLayoutFromFile(NormalizeYamlPath(uiFilePath));
    }

    public void LoadYamlLayoutFromFile(string yamlFilePath)
    {
        ResetMessages();

        yamlFilePath = NormalizeYamlPath(yamlFilePath);

        if (!File.Exists(yamlFilePath))
        {
            LoadedProjectSummary = "YAML file missing";
            AddMessage("UI", "Warning", "YAML layout file not found.", yamlFilePath);
            StatusText = $"No YAML layout file found: {yamlFilePath}";
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = "AutomationExplorer";
            return;
        }

        try
        {
            var fallbackName = Path.GetFileNameWithoutExtension(yamlFilePath);
            var layout = ProjectUiLayoutLoader.LoadYaml(yamlFilePath, fallbackName);
            var pageName = GetTechnicalPageName(yamlFilePath);
            var model = CreateFolderModelFromLayout(yamlFilePath, layout, 1, pageName);

            var rootDirectory = Path.GetDirectoryName(yamlFilePath) ?? AppContext.BaseDirectory;
            ApplyBookManifestSettings(rootDirectory);
            SetFolders([model]);
            ProjectPath = rootDirectory;
            _currentLayoutFilePath = yamlFilePath;
            _currentBookEntryPath = string.Empty;
            _isStructuredBook = false;
            HasLayout = true;
            IsDefaultLayout = false;
            HeaderTitle = Path.GetFileNameWithoutExtension(yamlFilePath) ?? "AutomationExplorer";
            LoadedProjectSummary = $"YAML layout | {Path.GetFileName(yamlFilePath)}";
            StatusText = $"YAML layout loaded: {yamlFilePath}";
            OnPropertyChanged(nameof(IsCurrentLayoutStartLayout));
            OnPropertyChanged(nameof(StartLayoutIconColor));
        }
        catch (Exception ex)
        {
            LoadedProjectSummary = "YAML invalid";
            AddMessage("UI", "Error", ex.Message, yamlFilePath);
            StatusText = $"YAML layout could not be loaded: {ex.Message}";
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = "AutomationExplorer";
        }
    }

    public void LoadYamlBookFromDirectory(string directoryPath)
    {
        var fullDirectory = Path.GetFullPath(directoryPath);
        var entryPath = GetBookEntryPath(fullDirectory);
        if (File.Exists(entryPath) || Directory.Exists(GetPagesDirectoryPath(fullDirectory)))
        {
            LoadStructuredBookFromDirectory(fullDirectory, File.Exists(entryPath) ? entryPath : string.Empty);
            return;
        }

        LoadLegacyYamlBookFromDirectory(fullDirectory);
    }

    private void LoadLegacyYamlBookFromDirectory(string directoryPath)
    {
        try
        {
            var fullDirectory = Path.GetFullPath(directoryPath);
            if (!Directory.Exists(fullDirectory))
            {
                LoadedProjectSummary = "YAML directory not found";
                AddMessage("Load", "Warning", "YAML project directory not found.", fullDirectory);
                StatusText = $"No YAML project directory found: {fullDirectory}";
                HasLayout = false;
                IsDefaultLayout = false;
                HeaderTitle = "AutomationExplorer";
                return;
            }

            StopBookWatcher();
            _watchedPages.Clear();
            _currentBookEntryPath = string.Empty;
            _isStructuredBook = false;

            var yamlFiles = Directory.GetFiles(fullDirectory, "*.yaml", SearchOption.TopDirectoryOnly);
            foreach (var filePath in yamlFiles)
            {
                try
                {
                    var fallbackName = GetPageNameFallback(filePath);
                    var layout = ProjectUiLayoutLoader.LoadYaml(filePath, fallbackName);
                    var pageName = GetTechnicalPageName(filePath);
                    var pageIndex = GetFolderIndex(layout) ?? int.MaxValue;
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
            StartBookWatcher(fullDirectory, "*.yaml", false);
            ProjectPath = fullDirectory;
            LoadedProjectSummary = $"YAML directory project | {fullDirectory}";
            StatusText = $"YAML project directory loaded: {fullDirectory}";
        }
        catch (Exception ex)
        {
            LoadedProjectSummary = "YAML project could not be loaded";
            AddMessage("Load", "Error", ex.Message, directoryPath);
            StatusText = $"YAML load failed: {ex.Message}";
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = "AutomationExplorer";
        }
    }

    private void LoadStructuredBookFromEntryFile(string entryPath)
    {
        var fullEntryPath = Path.GetFullPath(entryPath);
        if (!File.Exists(fullEntryPath))
        {
            LoadedProjectSummary = "Project entry not found";
            AddMessage("Load", "Warning", "Project entry file not found.", fullEntryPath);
            StatusText = $"No .aaep file found: {fullEntryPath}";
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = "AutomationExplorer";
            return;
        }

        LoadStructuredBookFromDirectory(Path.GetDirectoryName(fullEntryPath) ?? AppContext.BaseDirectory, fullEntryPath);
    }

    private void LoadStructuredBookFromDirectory(string bookRootPath, string? entryPath)
    {
        try
        {
            var fullDirectory = Path.GetFullPath(bookRootPath);
            if (!Directory.Exists(fullDirectory))
            {
                LoadedProjectSummary = "Project directory not found";
                AddMessage("Load", "Warning", "AutomationExplorer project root directory not found.", fullDirectory);
                StatusText = $"No AutomationExplorer project directory found: {fullDirectory}";
                HasLayout = false;
                IsDefaultLayout = false;
                HeaderTitle = "AutomationExplorer";
                return;
            }

            var fullEntryPath = string.IsNullOrWhiteSpace(entryPath)
                ? GetBookEntryPath(fullDirectory)
                : Path.GetFullPath(entryPath);
            var pagesDirectory = GetPagesDirectoryPath(fullDirectory);

            StopBookWatcher();
            _watchedPages.Clear();
            _isStructuredBook = true;
            _currentBookEntryPath = File.Exists(fullEntryPath) ? fullEntryPath : string.Empty;
            ProjectPath = fullDirectory;

            if (!File.Exists(fullEntryPath))
            {
                AddMessage("Load", "Warning", "Project.aaep is missing.", fullDirectory);
            }

            if (!Directory.Exists(pagesDirectory))
            {
                HasLayout = false;
                IsDefaultLayout = false;
                HeaderTitle = Path.GetFileName(fullDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "AutomationExplorer";
                SetFolders(Array.Empty<FolderModel>());
                LoadedProjectSummary = "Folders directory missing";
                StatusText = $"Folders directory missing: {pagesDirectory}";
                AddMessage("Load", "Warning", "Folders directory not found.", pagesDirectory);
                OnPropertyChanged(nameof(IsCurrentLayoutStartLayout));
                OnPropertyChanged(nameof(StartLayoutIconColor));
                return;
            }

            var pageDirectories = Directory.GetDirectories(pagesDirectory, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var usedPageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pageDirectory in pageDirectories)
            {
                var filePath = GetPageLayoutPath(pageDirectory);
                if (!File.Exists(filePath))
                {
                    AddMessage("Load", "Warning", "Folder.yaml missing in folder directory.", pageDirectory);
                    continue;
                }

                try
                {
                    ValidatePageMetadataScripts(pageDirectory);
                    var fallbackName = GetPageNameFallback(filePath);
                    var layout = ProjectUiLayoutLoader.LoadYaml(filePath, fallbackName);
                    var pageName = GetTechnicalPageName(filePath);
                    if (!usedPageNames.Add(pageName))
                    {
                        AddMessage("Load", "Warning", $"Duplicate folder name detected: {pageName}", filePath);
                        continue;
                    }

                    var pageIndex = GetFolderIndex(layout) ?? int.MaxValue;
                    _watchedPages[filePath] = new WatchedPage
                    {
                        FilePath = filePath,
                        PageIndex = pageIndex,
                        PageName = pageName,
                        Layout = layout
                    };
                }
                catch (Exception ex)
                {
                    AddMessage("UI", "Error", ex.Message, filePath);
                }
            }

            UpdatePagesFromWatchedPages(fullDirectory);
            StartBookWatcher(pagesDirectory, StructuredLayoutWatcherFilter, true);
            LoadedProjectSummary = $"AutomationExplorer | Folders: {_watchedPages.Count}";
            StatusText = $"AutomationExplorer loaded: {fullDirectory}";
        }
        catch (Exception ex)
        {
            LoadedProjectSummary = "AutomationExplorer could not be loaded";
            AddMessage("Load", "Error", ex.Message, bookRootPath);
            StatusText = $"AutomationExplorer load failed: {ex.Message}";
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = "AutomationExplorer";
        }
    }

    public void LoadYamlVersionFromCurrentLayout()
    {
        var basePath = !string.IsNullOrWhiteSpace(_currentLayoutFilePath)
            ? _currentLayoutFilePath
            : _startupPagePath;

        if (string.IsNullOrWhiteSpace(basePath))
        {
            StatusText = "No current layout available for YAML start";
            return;
        }

        var yamlPath = string.Equals(Path.GetExtension(basePath), ".yaml", StringComparison.OrdinalIgnoreCase)
            ? basePath
            : Path.ChangeExtension(basePath, ".yaml");

        LoadYamlLayoutFromFile(yamlPath);
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
        LoadYamlBookFromDirectory(directoryPath);
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
            if (Directory.Exists(ProjectPath))
            {
                LoadYamlBookFromDirectory(ProjectPath);
                ProjectPath = Path.GetFullPath(ProjectPath);
                LoadedProjectSummary = _isStructuredBook
                    ? $"AutomationExplorer | {ProjectPath}"
                    : $"Directory project | {ProjectPath}";
                StatusText = _isStructuredBook
                    ? $"AutomationExplorer loaded: {ProjectPath}"
                    : $"Project directory loaded: {ProjectPath}";
                return;
            }

            if (!File.Exists(ProjectPath))
            {
                LoadedProjectSummary = "Project definition not found";
                AddMessage("Load", "Warning", "Project definition file not found.", ProjectPath);
                StatusText = $"No project definition found: {ProjectPath}";
                return;
            }

            if (IsBookEntryPath(ProjectPath))
            {
                var entryPath = Path.GetFullPath(ProjectPath);
                LoadStructuredBookFromEntryFile(entryPath);
                ProjectPath = Path.GetDirectoryName(entryPath) ?? AppContext.BaseDirectory;
                LoadedProjectSummary = $"AutomationExplorer | {Path.GetFileName(entryPath)}";
                StatusText = $"Project loaded: {entryPath}";
                return;
            }

            var yamlPath = NormalizeYamlPath(ProjectPath);
            LoadYamlLayoutFromFile(yamlPath);

            var definitionDirectory = Path.GetDirectoryName(yamlPath) ?? AppContext.BaseDirectory;
            ProjectPath = definitionDirectory;
            LoadedProjectSummary = $"Project | {Path.GetFileName(yamlPath)}";
            StatusText = $"Project loaded: {yamlPath}";
        }
        catch (Exception ex)
        {
            LoadedProjectSummary = "Project could not be loaded";
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
            if (IsDirectoryBook)
            {
                if (string.IsNullOrWhiteSpace(ProjectPath) || !Directory.Exists(ProjectPath))
                {
                    StatusText = "No project directory to reload.";
                    return;
                }

                if (_isStructuredBook)
                {
                    var entryPath = string.IsNullOrWhiteSpace(_currentBookEntryPath)
                        ? GetBookEntryPath(ProjectPath)
                        : _currentBookEntryPath;
                    LoadStructuredBookFromDirectory(ProjectPath, entryPath);
                    StatusText = $"AutomationExplorer reloaded: {ProjectPath}";
                }
                else
                {
                    LoadYamlBookFromDirectory(ProjectPath);
                    StatusText = $"Project reloaded: {ProjectPath}";
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(_currentLayoutFilePath))
            {
                StatusText = "No layout to reload.";
                return;
            }

            LoadYamlLayoutFromFile(_currentLayoutFilePath);
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

    private IReadOnlyList<FolderModel> CreateFoldersFromProject(ProjectModel project)
    {
        var pages = project.Folders
            .Select((page, index) => CreateFolderFromProject(page, index + 1))
            .ToList();

        if (pages.Count == 0)
        {
            pages.Add(new FolderModel
            {
                Index = 1,
                Name = project.ProjectName
            });
        }

        return pages;
    }

    private FolderModel CreateFolderFromProject(ProjectFolderDefinition page, int index)
    {
        var pageName = string.IsNullOrWhiteSpace(page.Name) ? $"Folder{index}" : page.Name;
        var pageDisplayText = GetFolderDisplayText(page) ?? pageName;
        var yamlPath = NormalizeYamlPath(page.UiFile);

        if (string.IsNullOrWhiteSpace(yamlPath) || !File.Exists(yamlPath))
        {
            var fallbackModel = new FolderModel
            {
                Index = index,
                Name = pageName,
                DisplayText = pageDisplayText,
                UiFilePath = yamlPath
            };

            fallbackModel.Items.Add(CreateFallbackItem(page.Name, "No Folder.yaml found"));
            return fallbackModel;
        }

        try
        {
            var layout = ProjectUiLayoutLoader.LoadYaml(yamlPath, page.Name);
            var model = new FolderModel
            {
                Index = index,
                Name = pageName,
                DisplayText = pageDisplayText,
                UiFilePath = yamlPath,
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
            AddMessage("UI", "Error", ex.Message, yamlPath);
            var fallbackModel = new FolderModel
            {
                Index = index,
                Name = pageName,
                DisplayText = pageDisplayText,
                UiFilePath = yamlPath
            };
            fallbackModel.Items.Add(CreateFallbackItem(page.Name, $"UI error: {ex.Message}"));
            return fallbackModel;
        }
    }

    private IEnumerable<FolderItemModel> CreateItemsFromNode(string pageName, ProjectUiNode node, double defaultX, double defaultY)
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

    private FolderItemModel CreateItemFromUiNode(string pageName, ProjectUiNode node, double defaultX, double defaultY)
    {
        var type = node.Type;
        var text = string.IsNullOrWhiteSpace(node.Text) ? type : node.Text;
        var kind = GetControlKindFromUiType(type);
        var isButton = kind == ControlKind.Button;
        var isListControl = kind == ControlKind.ListControl;
        var isChartControl = kind == ControlKind.ChartControl;
        var item = new FolderItemModel
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

    private static bool IsContainerNode(ProjectUiNode node)
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

        if (string.Equals(type, "TableControl", StringComparison.OrdinalIgnoreCase))
        {
            return ControlKind.TableControl;
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

        if (string.Equals(type, "CsvLoggerControl", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "CsvLogger", StringComparison.OrdinalIgnoreCase))
        {
            return ControlKind.CsvLoggerControl;
        }

        if (string.Equals(type, "SqlLoggerControl", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "SqlLogger", StringComparison.OrdinalIgnoreCase))
        {
            return ControlKind.SqlLoggerControl;
        }

        if (string.Equals(type, "CameraControl", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "Camera", StringComparison.OrdinalIgnoreCase))
        {
            return ControlKind.CameraControl;
        }

        if (string.Equals(type, "PythonEnvManager", StringComparison.OrdinalIgnoreCase))
        {
            return ControlKind.PythonEnvManager;
        }

        return ControlKind.Signal;
    }

    private FolderItemModel CreateFallbackItem(string pageName, string message)
    {
        var item = new FolderItemModel
        {
            Kind = ControlKind.Signal,
            Name = message,
            BodyCaption = message,
            ControlCaption = pageName,
            Footer = "Project",
            X = 48,
            Y = 48,
            Width = 320,
            Height = 84
        };

        item.SetHierarchy(pageName, null);
        item.ApplyTheme(IsDarkTheme);
        return item;
    }

    private static int? GetFolderIndex(ProjectFolderLayout layout)
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

    private FolderModel CreateFolderModelFromLayout(string uiFilePath, ProjectFolderLayout layout, int index, string pageName)
    {
        var model = new FolderModel
        {
            Index = index,
            Views = layout.Views.ToDictionary(static entry => entry.Key, static entry => entry.Value),
            Name = pageName,
            DisplayText = string.IsNullOrWhiteSpace(layout.Caption) ? (string.IsNullOrWhiteSpace(layout.Title) ? pageName : layout.Title) : layout.Caption,
            UiFilePath = uiFilePath,
            UiLayoutDefinition = layout
        };

        foreach (var item in CreateItemsFromNode(pageName, layout.Layout, 24, 24))
        {
            model.Items.Add(item);
        }

        return model;
    }

    private void StartBookWatcher(string directory, string filter, bool includeSubdirectories)
    {
        try
        {
            var watcher = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = includeSubdirectories,
                Filter = filter,
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

    private void RestartBookWatcher()
    {
        if (string.IsNullOrWhiteSpace(ProjectPath) || !Directory.Exists(ProjectPath))
        {
            return;
        }

        if (_isStructuredBook)
        {
            var pagesDirectory = GetPagesDirectoryPath(ProjectPath);
            if (Directory.Exists(pagesDirectory))
            {
                StartBookWatcher(pagesDirectory, StructuredLayoutWatcherFilter, true);
            }

            return;
        }

        StartBookWatcher(ProjectPath, "*.yaml", false);
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
                if (_watcherSuppressedPaths.Remove(fullPath))
                {
                    return;
                }

                _ = HandleBookFileUpsertAsync(fullPath);
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
                UpdatePagesFromWatchedPages(ProjectPath);
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
            _ = HandleBookFileUpsertAsync(newFullPath);
        });
    }

    private async Task HandleBookFileUpsertAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        if (!_watcherUpsertsInProgress.Add(filePath))
        {
            return;
        }

        try
        {
            var fallbackName = GetPageNameFallback(filePath);
            var layout = ProjectUiLayoutLoader.LoadYaml(filePath, fallbackName);
            var pageName = GetTechnicalPageName(filePath);
            var resolvedPageName = await ResolveWatchedPageNameConflictAsync(filePath, pageName);
            if (string.IsNullOrWhiteSpace(resolvedPageName))
            {
                StatusText = $"Folder import skipped for duplicate name: {pageName}";
                return;
            }

            var effectivePath = filePath;
            if (!string.Equals(resolvedPageName, pageName, StringComparison.Ordinal))
            {
                effectivePath = RenamePageStorage(filePath, resolvedPageName);
                fallbackName = GetPageNameFallback(effectivePath);
                layout = ProjectUiLayoutLoader.LoadYaml(effectivePath, fallbackName);
            }

            pageName = GetTechnicalPageName(effectivePath);
            var pageIndex = GetFolderIndex(layout) ?? int.MaxValue;
            var watchedPage = new WatchedPage
            {
                FilePath = effectivePath,
                PageIndex = pageIndex,
                PageName = pageName,
                Layout = layout
            };

            _watchedPages[effectivePath] = watchedPage;
            UpdatePagesFromWatchedPages(ProjectPath);
        }
        catch (Exception ex)
        {
            AddMessage("UI", "Error", ex.Message, filePath);
        }
        finally
        {
            _watcherUpsertsInProgress.Remove(filePath);
        }
    }

    private async Task<string?> ResolveWatchedPageNameConflictAsync(string filePath, string requestedPageName)
    {
        var candidate = string.IsNullOrWhiteSpace(requestedPageName)
            ? Path.GetFileNameWithoutExtension(filePath)
            : requestedPageName.Trim();

        while (HasDuplicatePageName(filePath, candidate))
        {
            if (ResolveDuplicatePageNameAsync is null)
            {
                AddMessage("Watch", "Warning", $"Duplicate folder name detected: {candidate}", filePath);
                return null;
            }

            var prompt = $"A folder named '{candidate}' already exists. Enter a new file-based folder name.";
            var replacement = await ResolveDuplicatePageNameAsync(candidate, prompt);
            if (string.IsNullOrWhiteSpace(replacement))
            {
                return null;
            }

            candidate = replacement.Trim();
        }

        return candidate;
    }

    private bool HasDuplicatePageName(string filePath, string pageName)
    {
        if (string.IsNullOrWhiteSpace(pageName))
        {
            return false;
        }

        return _watchedPages.Values.Any(page => !string.Equals(page.FilePath, filePath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(page.PageName, pageName, StringComparison.OrdinalIgnoreCase))
            || Folders.Any(page => !string.Equals(page.UiFilePath, filePath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(page.Name, pageName, StringComparison.OrdinalIgnoreCase));
    }

    private string RenamePageStorage(string filePath, string pageName)
    {
        var fullPath = Path.GetFullPath(filePath);
        var pageFileName = Path.GetFileName(fullPath);
        var parentDirectory = Path.GetDirectoryName(fullPath) ?? string.Empty;

        if (string.Equals(pageFileName, FolderLayoutFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(pageFileName, LegacyFolderLayoutFileName, StringComparison.OrdinalIgnoreCase))
        {
            var currentFolderDirectory = parentDirectory;
            var projectFoldersDirectory = Path.GetDirectoryName(currentFolderDirectory) ?? string.Empty;
            var safeDirectoryName = BuildSafePageDirectoryName(pageName);
            if (string.IsNullOrWhiteSpace(safeDirectoryName))
            {
                throw new InvalidOperationException("Folder name contains no valid directory name characters.");
            }

            var targetFolderDirectory = Path.Combine(projectFoldersDirectory, safeDirectoryName);
            if (string.Equals(currentFolderDirectory, targetFolderDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }

            if (Directory.Exists(targetFolderDirectory))
            {
                throw new IOException($"A folder directory named '{safeDirectoryName}' already exists.");
            }

            Directory.Move(currentFolderDirectory, targetFolderDirectory);
            return GetPageLayoutPath(targetFolderDirectory);
        }

        var safeFileName = BuildSafePageDirectoryName(pageName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            throw new InvalidOperationException("Folder name contains no valid file name characters.");
        }

        var targetFilePath = Path.Combine(parentDirectory, $"{safeFileName}.yaml");
        if (string.Equals(fullPath, targetFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        if (File.Exists(targetFilePath))
        {
            throw new IOException($"A folder file named '{Path.GetFileName(targetFilePath)}' already exists.");
        }

        File.Move(fullPath, targetFilePath);
        return targetFilePath;
    }

    private static string NormalizeYamlPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return string.Equals(Path.GetExtension(path), ".yaml", StringComparison.OrdinalIgnoreCase)
            ? path
            : Path.ChangeExtension(path, ".yaml");
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
            ProjectPath = directory;
            _currentLayoutFilePath = string.Empty;
            HasLayout = false;
            IsDefaultLayout = false;
            HeaderTitle = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "AutomationExplorer";
            SetFolders(Array.Empty<FolderModel>());
            LoadedProjectSummary = "No folders found";
            StatusText = $"No folders found in directory: {directory}";
            OnPropertyChanged(nameof(IsCurrentLayoutStartLayout));
            OnPropertyChanged(nameof(StartLayoutIconColor));
            return;
        }

        var pages = new List<FolderModel>(orderedPages.Count);
        for (var i = 0; i < orderedPages.Count; i++)
        {
            var watched = orderedPages[i];
            var model = CreateFolderModelFromLayout(watched.FilePath, watched.Layout, i + 1, watched.PageName);
            pages.Add(model);
        }

        ApplyBookManifestSettings(directory);
        SetFolders(pages);
        ProjectPath = directory;
        _currentLayoutFilePath = orderedPages[0].FilePath;
        HasLayout = true;
        IsDefaultLayout = false;
        HeaderTitle = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "AutomationExplorer";
        LoadedProjectSummary = _isStructuredBook
            ? $"AutomationExplorer | Folders: {pages.Count}"
            : $"Directory project | Folders: {pages.Count}";
        StatusText = _isStructuredBook
            ? $"AutomationExplorer loaded: {directory}"
            : $"Directory loaded: {directory}";
        OnPropertyChanged(nameof(IsCurrentLayoutStartLayout));
        OnPropertyChanged(nameof(StartLayoutIconColor));
    }

}