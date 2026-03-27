using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Amium.EditorUi.Controls;
using Amium.Host;
using Amium.Items;
using Amium.Logging;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;
using UdlClientRuntime = Amium.Host.HostUdlClient;

namespace Amium.UiEditor.Controls;

public partial class UdlClientControl : EditorTemplateControl
{
    public static readonly DirectProperty<UdlClientControl, string> SocketTextProperty =
        AvaloniaProperty.RegisterDirect<UdlClientControl, string>(nameof(SocketText), control => control.SocketText);

    public static readonly DirectProperty<UdlClientControl, string> ConnectionStateTextProperty =
        AvaloniaProperty.RegisterDirect<UdlClientControl, string>(nameof(ConnectionStateText), control => control.ConnectionStateText);

    public static readonly DirectProperty<UdlClientControl, string> AutoConnectTextProperty =
        AvaloniaProperty.RegisterDirect<UdlClientControl, string>(nameof(AutoConnectText), control => control.AutoConnectText);

    public static readonly DirectProperty<UdlClientControl, string> ItemCountTextProperty =
        AvaloniaProperty.RegisterDirect<UdlClientControl, string>(nameof(ItemCountText), control => control.ItemCountText);

    public static readonly DirectProperty<UdlClientControl, bool> CanConnectProperty =
        AvaloniaProperty.RegisterDirect<UdlClientControl, bool>(nameof(CanConnect), control => control.CanConnect);

    public static readonly DirectProperty<UdlClientControl, bool> CanDisconnectProperty =
        AvaloniaProperty.RegisterDirect<UdlClientControl, bool>(nameof(CanDisconnect), control => control.CanDisconnect);

    public static readonly DirectProperty<UdlClientControl, IBrush> ConnectionStatusBackgroundProperty =
        AvaloniaProperty.RegisterDirect<UdlClientControl, IBrush>(nameof(ConnectionStatusBackground), control => control.ConnectionStatusBackground);

    public static readonly DirectProperty<UdlClientControl, IBrush> ConnectionStatusForegroundProperty =
        AvaloniaProperty.RegisterDirect<UdlClientControl, IBrush>(nameof(ConnectionStatusForeground), control => control.ConnectionStatusForeground);

    public static readonly DirectProperty<UdlClientControl, IBrush> ConnectionStatusHoverBackgroundProperty =
        AvaloniaProperty.RegisterDirect<UdlClientControl, IBrush>(nameof(ConnectionStatusHoverBackground), control => control.ConnectionStatusHoverBackground);

    public static readonly DirectProperty<UdlClientControl, bool> CanToggleConnectionProperty =
        AvaloniaProperty.RegisterDirect<UdlClientControl, bool>(nameof(CanToggleConnection), control => control.CanToggleConnection);

    public static readonly DirectProperty<UdlClientControl, string> ConnectionToggleTextProperty =
        AvaloniaProperty.RegisterDirect<UdlClientControl, string>(nameof(ConnectionToggleText), control => control.ConnectionToggleText);

    private Popup? _attachPopup;
    private PageItemModel? _observedItem;
    private UiPageContext? _uiPageContext;
    private UdlClientRuntime? _client;
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;
    private DispatcherTimer? _attachedItemsRefreshTimer;
    private readonly Dictionary<string, string> _publishedStatusValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _publishedRuntimeSignatures = new(StringComparer.OrdinalIgnoreCase);
    private int _clientItemsDirty = 1;
    private int _lastPublishedClientItemCount = -1;
    private int _hasAttachedPaths;
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private bool _canConnect = true;
    private bool _canDisconnect;
    private long _messageCounter;
    private long _rxCounter;
    private long _txCounter;
    private long _lastLoggedRxCounter;
    private long _lastLoggedTxCounter;
    private long _monitorLoopCounter;
    private volatile bool _verboseDiagnosticsEnabled;
    private string _socketText = "192.168.178.151:9001";
    private string _connectionStateText = "Disconnected";
    private string _autoConnectText = "False";
    private string _itemCountText = "0";
    private IBrush _connectionStatusBackground = Brushes.Black;
    private IBrush _connectionStatusForeground = Brushes.White;
    private IBrush _connectionStatusHoverBackground = Brushes.DimGray;
    private bool _canToggleConnection = true;
    private string _connectionToggleText = "Connect";

    public UdlClientControl()
    {
        AttachRows = [];
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    private enum ConnectionState
    {
        Connected,
        Disconnected,
        Failed
    }

    public ObservableCollection<AttachItemEditorRow> AttachRows { get; }

    public string SocketText
    {
        get => _socketText;
        private set => SetAndRaise(SocketTextProperty, ref _socketText, value);
    }

    public string ConnectionStateText
    {
        get => _connectionStateText;
        private set => SetAndRaise(ConnectionStateTextProperty, ref _connectionStateText, value);
    }

    public string AutoConnectText
    {
        get => _autoConnectText;
        private set => SetAndRaise(AutoConnectTextProperty, ref _autoConnectText, value);
    }

    public string ItemCountText
    {
        get => _itemCountText;
        private set => SetAndRaise(ItemCountTextProperty, ref _itemCountText, value);
    }

    public bool CanConnect
    {
        get => _canConnect;
        private set => SetAndRaise(CanConnectProperty, ref _canConnect, value);
    }

    public bool CanDisconnect
    {
        get => _canDisconnect;
        private set => SetAndRaise(CanDisconnectProperty, ref _canDisconnect, value);
    }

    public IBrush ConnectionStatusBackground
    {
        get => _connectionStatusBackground;
        private set => SetAndRaise(ConnectionStatusBackgroundProperty, ref _connectionStatusBackground, value);
    }

    public IBrush ConnectionStatusForeground
    {
        get => _connectionStatusForeground;
        private set => SetAndRaise(ConnectionStatusForegroundProperty, ref _connectionStatusForeground, value);
    }

    public IBrush ConnectionStatusHoverBackground
    {
        get => _connectionStatusHoverBackground;
        private set => SetAndRaise(ConnectionStatusHoverBackgroundProperty, ref _connectionStatusHoverBackground, value);
    }

    public bool CanToggleConnection
    {
        get => _canToggleConnection;
        private set => SetAndRaise(CanToggleConnectionProperty, ref _canToggleConnection, value);
    }

    public string ConnectionToggleText
    {
        get => _connectionToggleText;
        private set => SetAndRaise(ConnectionToggleTextProperty, ref _connectionToggleText, value);
    }

    private PageItemModel? Item => DataContext as PageItemModel;

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _attachPopup = this.FindControl<Popup>("AttachPopup");
        HookObservedItem();
        RefreshPresentation();
        _ = EnsureAutoConnectAsync();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        TearDownClient();
        CancelAttachedItemsRefresh();
        ReleaseUiPageContext();
        UnhookObservedItem();
        foreach (var row in AttachRows)
        {
            row.PropertyChanged -= OnAttachRowPropertyChanged;
        }

        AttachRows.Clear();
        _attachPopup = null;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        HookObservedItem();
        RefreshPresentation();
        _ = EnsureAutoConnectAsync();
    }

    private void HookObservedItem()
    {
        if (ReferenceEquals(_observedItem, Item))
        {
            return;
        }

        UnhookObservedItem();
        _observedItem = Item;
        if (_observedItem is not null)
        {
            _observedItem.PropertyChanged += OnObservedItemPropertyChanged;
            UpdateAttachedPathsFlag(_observedItem);
        }
        else
        {
            Volatile.Write(ref _hasAttachedPaths, 0);
        }

        RebuildAttachRows();
    }

    private void UnhookObservedItem()
    {
        if (_observedItem is null)
        {
            return;
        }

        _observedItem.PropertyChanged -= OnObservedItemPropertyChanged;
        _observedItem = null;
        Volatile.Write(ref _hasAttachedPaths, 0);
    }

    private void OnObservedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            var propertyName = e.PropertyName;
            Dispatcher.UIThread.Post(() => OnObservedItemPropertyChanged(sender, new PropertyChangedEventArgs(propertyName)));
            return;
        }

        if (e.PropertyName is nameof(PageItemModel.UdlClientHost)
            or nameof(PageItemModel.UdlClientPort)
            or nameof(PageItemModel.UdlClientAutoConnect)
            or nameof(PageItemModel.UdlClientDebugLogging)
            or nameof(PageItemModel.UdlAttachedItemPaths)
            or nameof(PageItemModel.Name)
            or nameof(PageItemModel.PageName))
        {
            RefreshPresentation();
        }

        if (e.PropertyName == nameof(PageItemModel.UdlAttachedItemPaths))
        {
            if (sender is PageItemModel changedItem)
            {
                UpdateAttachedPathsFlag(changedItem);
            }

            RebuildAttachRows();
            SynchronizeAttachedItems();
        }

        if (e.PropertyName == nameof(PageItemModel.UdlClientAutoConnect))
        {
            _ = EnsureAutoConnectAsync();
        }
    }

    private async System.Threading.Tasks.Task EnsureAutoConnectAsync()
    {
        if (Item?.UdlClientAutoConnect != true || _client is not null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(ConnectInternal);
    }

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandleInteractivePointerPressed(e);
    }

    private void OnMenuClicked(object? sender, RoutedEventArgs e)
    {
        RebuildAttachRows();
        LogAttachListSnapshot();
        if (_attachPopup is not null)
        {
            _attachPopup.IsOpen = !_attachPopup.IsOpen;
        }

        e.Handled = true;
    }

    private void OnConnectClicked(object? sender, RoutedEventArgs e)
    {
        ConnectInternal();
        e.Handled = true;
    }

    private void OnDisconnectClicked(object? sender, RoutedEventArgs e)
    {
        DisconnectInternal();
        e.Handled = true;
    }

    private void OnToggleConnectionClicked(object? sender, RoutedEventArgs e)
    {
        if (_client is null)
        {
            ConnectInternal();
        }
        else
        {
            DisconnectInternal();
        }

        e.Handled = true;
    }

    private void ConnectInternal()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ConnectInternal);
            return;
        }

        var item = Item;
        if (item is null)
        {
            return;
        }

        try
        {
            WriteDiagnosticLog($"Connect requested endpoint={item.UdlClientHost}:{item.UdlClientPort}");
            TearDownClient();
            Interlocked.Exchange(ref _messageCounter, 0);
            Interlocked.Exchange(ref _rxCounter, 0);
            Interlocked.Exchange(ref _txCounter, 0);
            Interlocked.Exchange(ref _lastLoggedRxCounter, 0);
            Interlocked.Exchange(ref _lastLoggedTxCounter, 0);
            Interlocked.Exchange(ref _monitorLoopCounter, 0);
            Interlocked.Exchange(ref _clientItemsDirty, 1);
            _lastPublishedClientItemCount = -1;
            _publishedStatusValues.Clear();
            _publishedRuntimeSignatures.Clear();
            var client = new UdlClientRuntime(NormalizeClientName(item), item.UdlClientHost, item.UdlClientPort);
            client.FrameReceived += OnClientFrameReceived;
            client.Diagnostic += OnClientDiagnostic;
            client.ConnectAsync().GetAwaiter().GetResult();
            _client = client;
            _connectionState = ConnectionState.Connected;
            StartMonitor();
            PublishClientItems();
            SynchronizeAttachedItems();
            WriteDiagnosticLog($"Connect completed localPort={client.LocalPort}");
            WriteDiagnosticLog($"Initial runtime roots={GetRootItemCount()} items={EnumerateClientItems().Count}");
        }
        catch (Exception ex)
        {
            _connectionState = ConnectionState.Failed;
            HostLogger.Log.Error(ex, "UdlClient connect failed for {ClientName}", item.Name);
            WriteDiagnosticError("Connect failed", ex);
        }

        RefreshPresentation();
    }

    private void DisconnectInternal()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(DisconnectInternal);
            return;
        }

        WriteDiagnosticLog("Disconnect requested");
        BeginDisconnectClient();
        _connectionState = ConnectionState.Disconnected;
        Interlocked.Exchange(ref _clientItemsDirty, 0);
        _lastPublishedClientItemCount = -1;
        CancelAttachedItemsRefresh();
        _publishedStatusValues.Clear();
        _publishedRuntimeSignatures.Clear();
        RefreshPresentation();
        WriteDiagnosticLog("Disconnect completed");
    }

    private void BeginDisconnectClient()
    {
        StopMonitor();

        if (_client is null)
        {
            ReleaseUiPageContext();
            return;
        }

        var client = _client;
        _client = null;
        client.FrameReceived -= OnClientFrameReceived;
        client.Diagnostic -= OnClientDiagnostic;
        Interlocked.Exchange(ref _clientItemsDirty, 0);
        _lastPublishedClientItemCount = -1;
        CancelAttachedItemsRefresh();
        _publishedRuntimeSignatures.Clear();
        ReleaseUiPageContext();

        _ = Task.Run(() =>
        {
            try
            {
                client.Dispose();
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _connectionState = ConnectionState.Failed;
                    RefreshPresentation();
                    WriteDiagnosticError("Disconnect failed", ex);
                });
            }
        });
    }

    private void TearDownClient()
    {
        StopMonitor();

        if (_client is null)
        {
            return;
        }

        _client.FrameReceived -= OnClientFrameReceived;
        _client.Diagnostic -= OnClientDiagnostic;
        Interlocked.Exchange(ref _clientItemsDirty, 0);
        _lastPublishedClientItemCount = -1;
        CancelAttachedItemsRefresh();
        _publishedRuntimeSignatures.Clear();
        ReleaseUiPageContext();
        _client.Dispose();
        _client = null;
    }

    private void OnClientFrameReceived(uint id, byte dlc, byte[] data)
    {
        Interlocked.Increment(ref _messageCounter);
        Interlocked.Increment(ref _rxCounter);
        _connectionState = ConnectionState.Connected;
        Interlocked.Exchange(ref _clientItemsDirty, 1);
    }

    private void OnClientDiagnostic(string message)
    {
        UpdateCountersFromDiagnostic(message);

        if (ShouldLogDiagnosticMessage(message) && ShouldWriteVerboseDiagnostics())
        {
            WriteVerboseDiagnosticLog(message);
        }
    }

    private void StartMonitor()
    {
        if (_monitorTask is not null)
        {
            return;
        }

        _monitorCts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoopAsync(_monitorCts.Token), _monitorCts.Token);
    }

    private void StopMonitor()
    {
        _monitorCts?.Cancel();
        WaitForMonitor(_monitorTask);
        _monitorCts?.Dispose();
        _monitorCts = null;
        _monitorTask = null;
    }

    private async Task MonitorLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var loop = Interlocked.Increment(ref _monitorLoopCounter);
                var publishItems = Interlocked.Exchange(ref _clientItemsDirty, 0) == 1;

                if (publishItems)
                {
                    PublishClientItems();
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RefreshPresentation();
                });

                if (loop == 1 || loop % 4 == 0)
                {
                    if (GetRootItemCount() == 0 && Interlocked.Read(ref _messageCounter) > 0)
                    {
                        WriteVerboseDiagnosticLog($"Monitor snapshot roots=0 items={EnumerateClientItems().Count} messages={Interlocked.Read(ref _messageCounter)} localPort={_client?.LocalPort ?? 0}");
                    }
                }

                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void UpdateCountersFromDiagnostic(string message)
    {
        if (message.Contains("tx queued", StringComparison.OrdinalIgnoreCase)
            || message.Contains("tx send package", StringComparison.OrdinalIgnoreCase)
            || message.Contains("send write pdo", StringComparison.OrdinalIgnoreCase)
            || message.Contains("write request", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _txCounter);
        }

        if (message.Contains("OnCanMessageReceived", StringComparison.OrdinalIgnoreCase)
            || message.Contains("rx packet", StringComparison.OrdinalIgnoreCase))
        {
            _connectionState = ConnectionState.Connected;
        }

        if (message.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("error=", StringComparison.OrdinalIgnoreCase))
        {
            _connectionState = ConnectionState.Failed;
        }

        if (message.Contains("close", StringComparison.OrdinalIgnoreCase)
            || message.Contains("dispose", StringComparison.OrdinalIgnoreCase))
        {
            _connectionState = ConnectionState.Disconnected;
        }
    }

    private static void WaitForMonitor(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            task.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static inner => inner is OperationCanceledException))
        {
        }
    }

    private void PublishClientItems()
    {
        if (_client is null)
        {
            return;
        }

        var runtimeItems = EnumerateClientItems();
        if (runtimeItems.Count != _lastPublishedClientItemCount)
        {
            _lastPublishedClientItemCount = runtimeItems.Count;
            var samplePaths = string.Join(", ", runtimeItems
                .Take(3)
                .Select(static item => item.Path ?? string.Empty));
            WriteDiagnosticLog($"Runtime items updated client={_client.Name} count={runtimeItems.Count} samplePaths=[{samplePaths}]");
            ScheduleAttachedItemsRefresh();
        }

        foreach (var runtimeItem in runtimeItems)
        {
            if (!string.IsNullOrWhiteSpace(runtimeItem.Path) && ShouldPublishRuntimeItem(runtimeItem))
            {
                WriteVerboseDiagnosticLog($"Publish snapshot client={_client.Name} path={runtimeItem.Path} signature={BuildItemSignature(runtimeItem)}");
                HostRegistries.Data.UpsertSnapshot(runtimeItem.Path!, runtimeItem.Clone(), pruneMissingMembers: true);
            }
        }
    }

    private void ScheduleAttachedItemsRefresh()
    {
        if (Volatile.Read(ref _hasAttachedPaths) != 1)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            _attachedItemsRefreshTimer ??= new DispatcherTimer();
            _attachedItemsRefreshTimer.Stop();
            _attachedItemsRefreshTimer.Interval = TimeSpan.FromSeconds(2);
            _attachedItemsRefreshTimer.Tick -= OnAttachedItemsRefreshTimerTick;
            _attachedItemsRefreshTimer.Tick += OnAttachedItemsRefreshTimerTick;
            _attachedItemsRefreshTimer.Start();
        });
    }

    private void CancelAttachedItemsRefresh()
    {
        Dispatcher.UIThread.Post(() => _attachedItemsRefreshTimer?.Stop());
    }

    private void OnAttachedItemsRefreshTimerTick(object? sender, EventArgs e)
    {
        if (sender is DispatcherTimer timer)
        {
            timer.Stop();
            timer.Tick -= OnAttachedItemsRefreshTimerTick;
        }

        SynchronizeAttachedItems();
        RebuildAttachRows();
        Host?.RefreshPageBindings(Item?.PageName ?? string.Empty);
        RefreshPresentation();
        WriteVerboseDiagnosticLog($"AttachToUi refreshed after item-settle delay itemCount={_lastPublishedClientItemCount}");
    }

    private bool ShouldPublishRuntimeItem(Item runtimeItem)
    {
        var path = runtimeItem.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var signature = BuildItemSignature(runtimeItem);
        if (_publishedRuntimeSignatures.TryGetValue(path, out var previousSignature)
            && string.Equals(previousSignature, signature, StringComparison.Ordinal))
        {
            return false;
        }

        _publishedRuntimeSignatures[path] = signature;
        return true;
    }

    private static string BuildItemSignature(Item item)
    {
        var parameters = item.Params.GetDictionary()
            .OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static entry => $"{entry.Key}={entry.Value.LastUpdate}:{entry.Value.Value}");

        return string.Join("|", parameters);
    }

    private void RebuildAttachRows()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RebuildAttachRows);
            return;
        }

        var item = Item;
        foreach (var row in AttachRows)
        {
            row.PropertyChanged -= OnAttachRowPropertyChanged;
        }

        AttachRows.Clear();
        if (item is null)
        {
            return;
        }

        var selected = ParseAttachedPaths(item.UdlAttachedItemPaths);
        foreach (var option in GetAttachOptions(item))
        {
            var row = new AttachItemEditorRow
            {
                RelativePath = option,
                IsAttached = selected.Contains(option)
            };

            row.PropertyChanged += OnAttachRowPropertyChanged;
            AttachRows.Add(row);
        }
    }

    private void OnAttachRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            var propertyName = e.PropertyName;
            Dispatcher.UIThread.Post(() => OnAttachRowPropertyChanged(sender, new PropertyChangedEventArgs(propertyName)));
            return;
        }

        if (e.PropertyName != nameof(AttachItemEditorRow.IsAttached) || Item is null)
        {
            return;
        }

        Item.UdlAttachedItemPaths = string.Join(Environment.NewLine, AttachRows
            .Where(static row => row.IsAttached)
            .Select(static row => row.RelativePath));

        SynchronizeAttachedItems();
        RefreshPresentation();
    }

    private void SynchronizeAttachedItems()
    {
        ReleaseUiPageContext();

        var item = Item;
        if (item is null || _client is null)
        {
            return;
        }

        var attachedPaths = ParseAttachedPaths(item.UdlAttachedItemPaths);
        if (attachedPaths.Count == 0)
        {
            return;
        }

        var pageContext = new UiPageContext($"{item.PageName}/{NormalizeClientName(item)}", "UdlBook");
        _uiPageContext = pageContext;

        foreach (var relativePath in attachedPaths)
        {
            if (!TryResolveRuntimeItem(relativePath, out var runtimeItem) || runtimeItem?.Path is null)
            {
                continue;
            }

            var alias = relativePath.Replace('\\', '/').Trim('/');
            var attached = pageContext.Attach(runtimeItem, alias);
            WriteVerboseDiagnosticLog($"Attach snapshot page={pageContext.PagePath} client={NormalizeClientName(item)} runtimePath={runtimeItem.Path} alias={alias} attachedPath={attached.Path}");
            HostRegistries.Data.UpsertSnapshot(attached.Path!, attached.Clone(), pruneMissingMembers: true);
        }
    }

    private void ReleaseUiPageContext()
    {
        _uiPageContext?.Dispose();
        _uiPageContext = null;
    }

    private bool TryResolveRuntimeItem(string relativePath, out Item? resolved)
    {
        resolved = EnumerateClientItems()
            .FirstOrDefault(candidate => string.Equals(GetRelativeRuntimePath(candidate), relativePath, StringComparison.OrdinalIgnoreCase));

        return resolved is not null;
    }

    private IReadOnlyList<Item> EnumerateClientItems()
    {
        if (_client is null)
        {
            return [];
        }

        var items = new List<Item>();
        foreach (var root in _client.Items.GetDictionary().Values.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase))
        {
            AppendItem(root, items);
        }

        return items;
    }

    private int GetRootItemCount()
    {
        return _client?.Items.GetDictionary().Count ?? 0;
    }

    private static bool HasAttachedPaths(PageItemModel item)
    {
        return ParseAttachedPaths(item.UdlAttachedItemPaths).Count > 0;
    }

    private void UpdateAttachedPathsFlag(PageItemModel item)
    {
        Volatile.Write(ref _hasAttachedPaths, HasAttachedPaths(item) ? 1 : 0);
    }

    private static void AppendItem(Item item, ICollection<Item> items)
    {
        items.Add(item);
        foreach (var child in item.GetDictionary().Values.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase))
        {
            AppendItem(child, items);
        }
    }

    private IEnumerable<string> GetAttachOptions(PageItemModel item)
    {
        var prefix = $"Runtime/UdlClient/{NormalizeClientName(item)}/";
        var runtimeOptions = HostRegistries.Data.GetAllKeys()
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(key => key[prefix.Length..])
            .Where(static path => IsRootAttachPath(path));

        return runtimeOptions
            .Concat(EnumerateClientItems().Select(GetRelativeRuntimePath))
            .Where(static path => IsRootAttachPath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
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

    private void LogAttachListSnapshot()
    {
        if (!ShouldWriteVerboseDiagnostics())
        {
            return;
        }

        var item = Item;
        if (item is null)
        {
            WriteVerboseDiagnosticLog("Attach list open skipped because DataContext item is null");
            return;
        }

        var prefix = $"Runtime/UdlClient/{NormalizeClientName(item)}/";
        var registryOptions = HostRegistries.Data.GetAllKeys()
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(key => key[prefix.Length..])
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var clientItems = EnumerateClientItems()
            .Select(static candidate => new
            {
                FullPath = candidate.Path ?? string.Empty,
                RelativePath = GetRelativeRuntimePath(candidate)
            })
            .OrderBy(static candidate => candidate.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var attachOptions = GetAttachOptions(item).ToArray();
        var attachRows = AttachRows
            .Select(static (row, index) => new
            {
                Index = index,
                row.RelativePath,
                row.IsAttached
            })
            .ToArray();

        WriteVerboseDiagnosticLog($"Attach list open page={item.PageName} client={NormalizeClientName(item)} registryCount={registryOptions.Length} clientItemCount={clientItems.Length} optionCount={attachOptions.Length} rowCount={attachRows.Length}");

        foreach (var option in registryOptions.Select(static (path, index) => new { Index = index, Path = path }))
        {
            WriteVerboseDiagnosticLog($"Attach registry[{option.Index}]={option.Path}");
        }

        foreach (var clientItem in clientItems.Select(static (candidate, index) => new { Index = index, candidate.FullPath, candidate.RelativePath }))
        {
            WriteVerboseDiagnosticLog($"Attach runtime[{clientItem.Index}] full={clientItem.FullPath} relative={clientItem.RelativePath}");
        }

        foreach (var option in attachOptions.Select(static (path, index) => new { Index = index, Path = path }))
        {
            WriteVerboseDiagnosticLog($"Attach option[{option.Index}]={option.Path}");
        }

        foreach (var row in attachRows)
        {
            WriteVerboseDiagnosticLog($"Attach row[{row.Index}] path={row.RelativePath} attached={row.IsAttached}");
        }
    }

    private void RefreshPresentation()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshPresentation);
            return;
        }

        var item = Item;
        SocketText = item is null ? string.Empty : $"{item.UdlClientHost}:{item.UdlClientPort}";
        AutoConnectText = item?.UdlClientAutoConnect == true ? "True" : "False";
        ConnectionStateText = _connectionState.ToString();
        ItemCountText = GetRootItemCount().ToString();
        CanConnect = _client is null;
        CanDisconnect = _client is not null;
        CanToggleConnection = CanConnect || CanDisconnect;
        ConnectionToggleText = _client is null ? "Connect" : "Disconnect";

        switch (_connectionState)
        {
            case ConnectionState.Connected:
                ConnectionStatusBackground = Brushes.ForestGreen;
                ConnectionStatusForeground = Brushes.White;
                break;
            case ConnectionState.Failed:
                ConnectionStatusBackground = Brushes.Tomato;
                ConnectionStatusForeground = Brushes.White;
                break;
            default:
                ConnectionStatusBackground = Brushes.Black;
                ConnectionStatusForeground = Brushes.White;
                break;
        }

        ConnectionStatusHoverBackground = CreateHoverBrush(ConnectionStatusBackground);

        _verboseDiagnosticsEnabled = item?.UdlClientDebugLogging == true;

        if (item is not null)
        {
            item.Footer = $"{ConnectionStateText} | {ItemCountText} Items | Msg {Interlocked.Read(ref _messageCounter)}";
            PublishStatusItems(item);
        }
    }

    private void EnsureDiagnosticLog(PageItemModel item)
    {
        _ = item;
    }

    private static IBrush CreateHoverBrush(IBrush baseBrush)
    {
        if (baseBrush is SolidColorBrush solid)
        {
            var c = solid.Color;
            static byte L(byte v) => (byte)System.Math.Min(255, v + (255 - v) * 0.25);
            var lighter = Color.FromArgb(c.A, L(c.R), L(c.G), L(c.B));
            return new SolidColorBrush(lighter);
        }

        return baseBrush;
    }

    private bool ShouldWriteVerboseDiagnostics()
    {
        return _verboseDiagnosticsEnabled;
    }

    private void WriteVerboseDiagnosticLog(string message)
    {
        if (!ShouldWriteVerboseDiagnostics())
        {
            return;
        }
        // Verbose Diagnostik: als Debug loggen und nicht über den UI-Thread marshallen,
        // damit hohes Logaufkommen die UI nicht blockiert.
        HostLogger.Log.Debug("[UdlClientControl] {Message}", message);
    }

    private void WriteDiagnosticLog(string message)
    {
        // Wichtige Statusmeldungen (Connect/Disconnect/High-Level) als Information loggen.
        HostLogger.Log.Information("[UdlClientControl] {Message}", message);
    }

    private void WriteDiagnosticError(string message, Exception exception)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => WriteDiagnosticError(message, exception));
            return;
        }

        HostLogger.Log.Error(exception, "[UdlClientControl] {Message}", message);
    }

    private static bool ShouldLogDiagnosticMessage(string message)
    {
        // Nur Verbindungs-/Initialisierungs-Lifecycle loggen, alles andere ignorieren,
        // damit das Log übersichtlich bleibt.
        if (message.Contains("ctor start", StringComparison.OrdinalIgnoreCase)
            || message.Contains("remote resolved endpoint", StringComparison.OrdinalIgnoreCase)
            || message.Contains("udp socket", StringComparison.OrdinalIgnoreCase)
            || message.Contains("open requested", StringComparison.OrdinalIgnoreCase)
            || message.Contains("open completed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("rx thread started", StringComparison.OrdinalIgnoreCase)
            || message.Contains("tx thread started", StringComparison.OrdinalIgnoreCase)
            || message.Contains("close requested", StringComparison.OrdinalIgnoreCase)
            || message.Contains("close completed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("dispose start", StringComparison.OrdinalIgnoreCase)
            || message.Contains("dispose completed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private void PublishStatusItems(PageItemModel item)
    {
        var statusBasePath = GetStatusBasePath(item);
        PublishStatusValue(statusBasePath, "Endpoint", SocketText, "UdlClient endpoint");
        PublishStatusValue(statusBasePath, "Connection", ConnectionStateText, "Connection state");
        PublishStatusValue(statusBasePath, "ItemCount", GetRootItemCount(), "Discovered items");
        PublishStatusValue(statusBasePath, "MessageCounter", Interlocked.Read(ref _messageCounter), "Received messages");
        PublishStatusValue(statusBasePath, "AutoConnect", item.UdlClientAutoConnect, "AutoConnect");
    }

    private void PublishStatusValue(string statusBasePath, string name, object? value, string title)
    {
        var cacheKey = $"{statusBasePath}/{name}";
        var serializedValue = value?.ToString() ?? "<null>";
        if (_publishedStatusValues.TryGetValue(cacheKey, out var previousValue)
            && string.Equals(previousValue, serializedValue, StringComparison.Ordinal))
        {
            return;
        }

        _publishedStatusValues[cacheKey] = serializedValue;

        var snapshot = new Item(name, value, statusBasePath);
        snapshot.Params["Kind"].Value = "Status";
        snapshot.Params["Text"].Value = title;
        snapshot.Params["Title"].Value = title;
        WriteVerboseDiagnosticLog($"Status snapshot base={statusBasePath} name={name} value={serializedValue}");
        HostRegistries.Data.UpsertSnapshot(snapshot.Path!, snapshot, pruneMissingMembers: true);
    }

    private static HashSet<string> ParseAttachedPaths(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return [];
        }

        var parsed = serialized
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static path => path.Replace('\\', '/').Trim('/'))
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path.Count(static ch => ch == '/'))
            .ThenBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in parsed)
        {
            var hasAncestor = normalized.Any(existing => path.StartsWith(existing + "/", StringComparison.OrdinalIgnoreCase));
            if (!hasAncestor)
            {
                normalized.Add(path);
            }
        }

        return normalized;
    }

    private static string NormalizeClientName(PageItemModel item)
        => string.IsNullOrWhiteSpace(item.Name) ? "UdlClientControl" : item.Name.Trim();

    private static string GetStatusBasePath(PageItemModel item)
        => $"UdlBook/{item.PageName}/{NormalizeClientName(item)}/Status";

    private static string GetRelativeRuntimePath(Item item)
    {
        var fullPath = item.Path ?? string.Empty;
        var runtimeMarkerIndex = fullPath.IndexOf("Runtime/UdlClient/", StringComparison.OrdinalIgnoreCase);
        if (runtimeMarkerIndex < 0)
        {
            return fullPath;
        }

        var trimmed = fullPath[runtimeMarkerIndex..];
        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 3)
        {
            return string.Empty;
        }

        return string.Join('/', segments.Skip(3));
    }
}