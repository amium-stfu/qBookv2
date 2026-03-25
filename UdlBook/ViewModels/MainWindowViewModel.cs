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
    private readonly string _startupPagePath;
    private string _bookProjectPath;
    private string _loadedBookSummary;
    private string _messagesSummary;
    private string _currentLogText;
    private bool _isBookOperationRunning;

    public MainWindowViewModel()
        : base(true)
    {
        _startupPagePath = Path.Combine(AppContext.BaseDirectory, "Page.json");
        LoadBookCommand = new Amium.UiEditor.ViewModels.RelayCommand(LoadBook, CanRunBookAction);
        RebuildBookCommand = new Amium.UiEditor.ViewModels.RelayCommand(RebuildBook, CanRunBookAction);
        RefreshLogCommand = new Amium.UiEditor.ViewModels.RelayCommand(RefreshLog);
        _bookProjectPath = Path.GetDirectoryName(_startupPagePath) ?? AppContext.BaseDirectory;
        _loadedBookSummary = "Kein Book geladen";
        _messagesSummary = "Keine Meldungen";
        _currentLogText = string.Empty;
        HostLogger.ProcessLog.EntryAdded += OnHostLogEntryAdded;
        RefreshLog();

        Dispatcher.UIThread.Post(LoadStartupPage, DispatcherPriority.Background);
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

    public Amium.UiEditor.ViewModels.RelayCommand LoadBookCommand { get; }

    public Amium.UiEditor.ViewModels.RelayCommand RebuildBookCommand { get; }

    public Amium.UiEditor.ViewModels.RelayCommand RefreshLogCommand { get; }

    public void RefreshLog()
    {
        var entries = HostLogger.ProcessLog.GetEntries();
        if (entries.Count == 0)
        {
            CurrentLogText = "Noch kein Log vorhanden.";
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

        StatusText = "Runtime gestoppt. Canvas geleert.";
    }

    public void ApplyRunningUi(BookProject project)
    {
        ApplyBookTabStripPlacement(project.RootDirectory);
        SetPages(CreatePagesFromBook(project));
        BookProjectPath = project.RootDirectory;
        LoadedBookSummary = $"{project.ProjectName} | Pages: {project.Pages.Count} | C#: {project.SourceFiles.Count} | UI: {project.UiFiles.Count}";
        StatusText = $"Runtime gestartet: {project.ProjectName}";
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

    private void LoadStartupPage()
    {
        ResetMessages();

        if (!File.Exists(_startupPagePath))
        {
            LoadedBookSummary = "Default layout fehlt";
            AddMessage("UI", "Warning", "Startup-Page.json nicht gefunden.", _startupPagePath);
            StatusText = $"Keine Startup-Page gefunden: {_startupPagePath}";
            return;
        }

        try
        {
            var layout = BookUiLayoutLoader.Load(_startupPagePath, "UdlClient");
            var pageName = string.IsNullOrWhiteSpace(layout.PageName) ? "UdlClient" : layout.PageName;
            var model = new PageModel
            {
                Index = 1,
                Name = pageName,
                DisplayText = string.IsNullOrWhiteSpace(layout.Title) ? pageName : layout.Title,
                UiFilePath = _startupPagePath,
                UiLayoutDefinition = layout
            };

            foreach (var item in CreateItemsFromNode(pageName, layout.Layout, 24, 24))
            {
                model.Items.Add(item);
            }

            ApplyBookTabStripPlacement(Path.GetDirectoryName(_startupPagePath) ?? AppContext.BaseDirectory);
            SetPages([model]);
            BookProjectPath = Path.GetDirectoryName(_startupPagePath) ?? AppContext.BaseDirectory;
            LoadedBookSummary = $"Default layout | {Path.GetFileName(_startupPagePath)}";
            StatusText = $"Default layout geladen: {_startupPagePath}";
        }
        catch (Exception ex)
        {
            LoadedBookSummary = "Default layout fehlerhaft";
            AddMessage("UI", "Error", ex.Message, _startupPagePath);
            StatusText = $"Default layout konnte nicht geladen werden: {ex.Message}";
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
            var result = await Core.LoadAndRunAsync(BookProjectPath);
            BookProjectPath = result.Project.RootDirectory;
            ApplyBookTabStripPlacement(result.Project.RootDirectory);
            LoadedBookSummary = $"{result.Project.ProjectName} | Pages: {result.Project.Pages.Count} | C#: {result.Project.SourceFiles.Count} | UI: {result.Project.UiFiles.Count}";
            AddMessage("Load", result.Success ? "Info" : "Error", $"Load {(result.Success ? "erfolgreich" : "fehlgeschlagen")}: {result.Project.ProjectName}", result.Project.RootDirectory);

            foreach (var diagnostic in result.Diagnostics)
            {
                var span = diagnostic.Location.GetMappedLineSpan();
                var path = string.IsNullOrWhiteSpace(span.Path) ? diagnostic.Location.SourceTree?.FilePath ?? string.Empty : span.Path;
                var location = string.IsNullOrWhiteSpace(path)
                    ? string.Empty
                    : $"{Path.GetFileName(path)}:{span.StartLinePosition.Line + 1}:{span.StartLinePosition.Character + 1}";

                AddMessage("Roslyn", diagnostic.Severity.ToString(), diagnostic.GetMessage(), location);
            }

            StatusText = result.Success
                ? $"Load erfolgreich: {result.Project.ProjectName} ({result.ErrorCount} Fehler, {result.WarningCount} Warnungen)"
                : $"Load fehlgeschlagen: {result.ErrorCount} Fehler, {result.WarningCount} Warnungen";
        }
        catch (Exception ex)
        {
            LoadedBookSummary = "Book konnte nicht geladen werden";
            AddMessage("Load", "Error", ex.Message);
            StatusText = $"Load fehlgeschlagen: {ex.Message}";
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
            var result = await Core.RebuildAsync(BookProjectPath);
            ApplyBookTabStripPlacement(result.Project.RootDirectory);
            SetPages(CreatePagesFromBook(result.Project));
            BookProjectPath = result.Project.RootDirectory;
            LoadedBookSummary = $"{result.Project.ProjectName} | Pages: {result.Project.Pages.Count} | C#: {result.Project.SourceFiles.Count} | UI: {result.Project.UiFiles.Count}";
            AddMessage("Build", result.Success ? "Info" : "Error", $"Build {(result.Success ? "erfolgreich" : "fehlgeschlagen")}: {result.Project.ProjectName}", result.Project.RootDirectory);

            foreach (var diagnostic in result.Diagnostics)
            {
                var span = diagnostic.Location.GetMappedLineSpan();
                var path = string.IsNullOrWhiteSpace(span.Path) ? diagnostic.Location.SourceTree?.FilePath ?? string.Empty : span.Path;
                var location = string.IsNullOrWhiteSpace(path)
                    ? string.Empty
                    : $"{Path.GetFileName(path)}:{span.StartLinePosition.Line + 1}:{span.StartLinePosition.Character + 1}";

                AddMessage("Roslyn", diagnostic.Severity.ToString(), diagnostic.GetMessage(), location);
            }

            StatusText = result.Success
                ? $"Build erfolgreich: {result.Project.ProjectName} ({result.ErrorCount} Fehler, {result.WarningCount} Warnungen)"
                : $"Build fehlgeschlagen: {result.ErrorCount} Fehler, {result.WarningCount} Warnungen";
        }
        catch (Exception ex)
        {
            AddMessage("Build", "Error", ex.Message);
            StatusText = $"Rebuild fehlgeschlagen: {ex.Message}";
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

            fallbackModel.Items.Add(CreateFallbackItem(page.Name, "Keine Page.json gefunden"));
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
            fallbackModel.Items.Add(CreateFallbackItem(page.Name, $"UI-Fehler: {ex.Message}"));
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
            Footer = isButton ? "Aktion" : type,
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

}