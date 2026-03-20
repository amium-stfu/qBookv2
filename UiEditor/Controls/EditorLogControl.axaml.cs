using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using UiEditor.Host;
using UiEditor.Host.Logging;
using UiEditor.Models;
using UiEditor.ViewModels;

namespace UiEditor.Controls;

public partial class EditorLogControl : UserControl
{
    private ListBox? _logListBox;
    private ScrollViewer? _scrollViewer;
    private ProcessLog? _processLog;
    private PageItemModel? _observedLogItem;

    public EditorLogControl()
    {
        LogEntries = [];
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    public ObservableCollection<string> LogEntries { get; }

    private PageItemModel? LogItem => DataContext as PageItemModel;

    private MainWindowViewModel? ViewModel
        => this.GetVisualRoot() is Window { DataContext: MainWindowViewModel viewModel } ? viewModel : null;

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (LogItem is null || ViewModel is null || this.GetVisualAncestors().OfType<PageEditorControl>().FirstOrDefault() is not { } editor)
        {
            return;
        }

        var anchor = this.TranslatePoint(new Point(Bounds.Width + 8, 0), editor) ?? new Point(24, 24);
        ViewModel.OpenItemEditor(LogItem, anchor.X, anchor.Y);
        e.Handled = true;
    }

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _logListBox = this.FindControl<ListBox>("LogListBox");
        HookObservedLogItem();
        ResolveProcessLog();
        ReloadEntries();
        ResolveScrollViewer();
        Dispatcher.UIThread.Post(ResolveScrollViewer, DispatcherPriority.Loaded);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        UnhookObservedLogItem();
        UnhookProcessLog();
        _scrollViewer = null;
        _logListBox = null;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        HookObservedLogItem();
        ResolveProcessLog();
        ReloadEntries();
    }

    private void ResolveProcessLog()
    {
        var resolved = ResolveProcessLogFromTarget(LogItem?.TargetLog);
        if (ReferenceEquals(_processLog, resolved))
        {
            return;
        }

        UnhookProcessLog();
        _processLog = resolved;
        if (_processLog is not null)
        {
            _processLog.EntryAdded += OnLogEntryAdded;
            _processLog.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }
    }

    private void HookObservedLogItem()
    {
        if (ReferenceEquals(_observedLogItem, LogItem))
        {
            return;
        }

        UnhookObservedLogItem();
        _observedLogItem = LogItem;
        if (_observedLogItem is not null)
        {
            _observedLogItem.PropertyChanged += OnLogItemPropertyChanged;
        }
    }

    private void UnhookObservedLogItem()
    {
        if (_observedLogItem is null)
        {
            return;
        }

        _observedLogItem.PropertyChanged -= OnLogItemPropertyChanged;
        _observedLogItem = null;
    }

    private void UnhookProcessLog()
    {
        if (_processLog is null)
        {
            return;
        }

        _processLog.EntryAdded -= OnLogEntryAdded;
        _processLog.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _processLog = null;
    }

    private void OnLogItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PageItemModel.TargetLog))
        {
            return;
        }

        ResolveProcessLog();
        ReloadEntries();
    }

    private void OnDisplaySettingsChanged()
    {
        Dispatcher.UIThread.Post(ReloadEntries);
    }

    private void OnLogEntryAdded(ProcessLogEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Add(FormatEntry(entry));
            ScrollToEnd();
        });
    }

    private void ReloadEntries()
    {
        LogEntries.Clear();
        foreach (var entry in _processLog?.GetEntries() ?? [])
        {
            LogEntries.Add(FormatEntry(entry));
        }

        ScrollToEnd();
    }

    private void ResolveScrollViewer()
    {
        if (_logListBox is null)
        {
            return;
        }

        _scrollViewer = _logListBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        _scrollViewer?.ScrollToEnd();
    }

    private static ProcessLog? ResolveProcessLogFromTarget(string? targetLog)
    {
        if (TryResolveProcessLog(targetLog, out var resolved))
        {
            return resolved;
        }

        return TryResolveProcessLog("Logs/Host", out resolved) ? resolved : null;
    }

    private static bool TryResolveProcessLog(string? targetLog, out ProcessLog? resolved)
    {
        resolved = null;
        if (string.IsNullOrWhiteSpace(targetLog))
        {
            return false;
        }

        var normalized = targetLog.Trim().Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (HostRegistries.Data.TryGet(normalized, out var item) && item?.Value is ProcessLog processLog)
        {
            resolved = processLog;
            return true;
        }

        if (!normalized.Contains('/', StringComparison.Ordinal)
            && HostRegistries.Data.TryGet($"Logs/{normalized}", out item)
            && item?.Value is ProcessLog legacyProcessLog)
        {
            resolved = legacyProcessLog;
            return true;
        }

        return false;
    }

    private static string FormatEntry(ProcessLogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Exception))
        {
            return $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] {entry.Message}";
        }

        return $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] {entry.Message}{Environment.NewLine}{entry.Exception}";
    }
}
