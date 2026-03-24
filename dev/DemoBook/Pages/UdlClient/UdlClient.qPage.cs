using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amium.Host;
using Amium.Host.Logging;
using Amium.Items;
using UdlRuntimeClient = UdlClient.UdlClient;
using UdlRuntimeModule = UdlClient.Module;

namespace DefinitionUdlClient;

public class qPage : BookPage
{
    private const string ReceiveProcessLogName = "UdlClientRx";
    private const string DiagnosticProcessLogName = "UdlClientDiag";
    private const string EndpointAddress = "192.168.178.151";
    private const int EndpointPort = 9001;
    private const string EndpointDisplay = "192.168.178.151:9001";
    private const int ModuleSlotCount = 8;

    private readonly Item _endpointItem = CreateStatusItem("Endpoint", "Runtime/DemoBook/UdlClient", "socket", EndpointDisplay);
    private readonly Item _connectionItem = CreateStatusItem("Connection", "Runtime/DemoBook/UdlClient", "state", "Disconnected");
    private readonly Item _moduleCountItem = CreateStatusItem("ModuleCount", "Runtime/DemoBook/UdlClient", "count", 0);
    private readonly Item _messageCounterItem = CreateStatusItem("MessageCounter", "Runtime/DemoBook/UdlClient", "rx", 0L);
    private readonly Item _monitorCounterItem = CreateStatusItem("MonitorCounter", "Runtime/DemoBook/UdlClient", "loop", 0L);
    private readonly Item _lastFrameItem = CreateStatusItem("LastFrame", "Runtime/DemoBook/UdlClient", "raw", "<none>");
    private readonly Item _focusedModuleItem = CreateStatusItem("FocusedModule", "Runtime/DemoBook/UdlClient", "module", "-");
    private readonly Item _focusedValueItem = CreateStatusItem("FocusedValue", "Runtime/DemoBook/UdlClient", "value", 0f);
    private readonly Item _focusedStateItem = CreateStatusItem("FocusedState", "Runtime/DemoBook/UdlClient", "state", 0);
    private readonly Item _focusedUnitItem = CreateStatusItem("FocusedUnit", "Runtime/DemoBook/UdlClient", "unit", string.Empty);
    private readonly Item _focusedSetActualItem = CreateStatusItem("FocusedSetActual", "Runtime/DemoBook/UdlClient", "set", 0f);
    private readonly Item _focusedSetWriteItem = CreateStatusItem("FocusedSetWrite", "Runtime/DemoBook/UdlClient", "set", 0f);
    private readonly Item _focusedOutActualItem = CreateStatusItem("FocusedOutActual", "Runtime/DemoBook/UdlClient", "out", 0f);
    private readonly Item _focusedOutWriteItem = CreateStatusItem("FocusedOutWrite", "Runtime/DemoBook/UdlClient", "out", 0f);
    private readonly Item[] _moduleSlotItems = CreateModuleSlotItems();

    private readonly Dictionary<string, string> _lastLoggedValues = new(StringComparer.Ordinal);

    private Item? _endpointAttached;
    private Item? _connectionAttached;
    private Item? _moduleCountAttached;
    private Item? _messageCounterAttached;
    private Item? _monitorCounterAttached;
    private Item? _lastFrameAttached;
    private Item? _focusedModuleAttached;
    private Item? _focusedValueAttached;
    private Item? _focusedStateAttached;
    private Item? _focusedUnitAttached;
    private Item? _focusedSetActualAttached;
    private Item? _focusedSetWriteAttached;
    private Item? _focusedOutActualAttached;
    private Item? _focusedOutWriteAttached;
    private readonly Item?[] _moduleSlotAttachedItems = new Item?[ModuleSlotCount];
    private ProcessLog? _receiveProcessLog;
    private ProcessLog? _diagnosticProcessLog;
    private UdlRuntimeClient? _client;
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;
    private string[] _currentModuleNames = Array.Empty<string>();
    private string? _focusedModuleName;
    private bool _isSyncingWriteProxyItems;
    private long _messageCount;
    private long _monitorLoopCount;
    private long _rawLogCount;
    private string _lastFrameText = "<none>";
    private string _connectionState = "Disconnected";

    public qPage() : base("UdlClient")
    {
        _focusedSetWriteItem.Changed += OnFocusedSetWriteChanged;
        _focusedOutWriteItem.Changed += OnFocusedOutWriteChanged;
    }

    protected override void OnInitialize()
    {
        EnsureProcessLog();
        EnsureAttachedItems();
        PublishCommands();
        PublishBoundItems();
        LogDiagnostic("page initialized");
    }

    protected override void OnRun()
    {
        EnsureProcessLog();
        EnsureAttachedItems();
        PublishCommands();
        PublishBoundItems();
        StartClient();
        StartMonitor();
    }

    protected override void OnDestroy()
    {
        StopMonitor();

        if (_client is not null)
        {
            LogDiagnostic($"closing client endpoint={EndpointDisplay}");
            _client.FrameReceived -= OnFrameReceived;
            _client.Diagnostic -= OnClientDiagnostic;
            _client.Close();
            _client = null;
        }

        _connectionItem.Value = "Disconnected";
        _connectionState = "Disconnected";
        _moduleCountItem.Value = 0;
        _messageCounterItem.Value = 0L;
        _monitorCounterItem.Value = 0L;
        _lastFrameItem.Value = "<none>";
        _lastFrameText = "<none>";
        ResetFocusedItems();
        ClearModuleSlots();
        LogDiagnostic("page destroyed");
    }

    private void EnsureProcessLog()
    {
        if (_receiveProcessLog is not null && _diagnosticProcessLog is not null)
        {
            PublishProcessLog(ReceiveProcessLogName, _receiveProcessLog, "UDL Receive Log");
            PublishProcessLog(DiagnosticProcessLogName, _diagnosticProcessLog, "UDL Transport Log");
            return;
        }

        var receiveLogDirectory = Path.Combine(HostLogger.LogDirectory, "demobook-udlclient", "receive");
        var diagnosticLogDirectory = Path.Combine(HostLogger.LogDirectory, "demobook-udlclient", "diagnostic");
        Directory.CreateDirectory(receiveLogDirectory);
        Directory.CreateDirectory(diagnosticLogDirectory);

        _receiveProcessLog = new ProcessLog();
        _receiveProcessLog.InitializeLog(receiveLogDirectory);
        PublishProcessLog(ReceiveProcessLogName, _receiveProcessLog, "UDL Receive Log");
        _receiveProcessLog.Info($"[{ReceiveProcessLogName}] ProcessLog initialized at {receiveLogDirectory}");

        _diagnosticProcessLog = new ProcessLog();
        _diagnosticProcessLog.InitializeLog(diagnosticLogDirectory);
        PublishProcessLog(DiagnosticProcessLogName, _diagnosticProcessLog, "UDL Transport Log");
        _diagnosticProcessLog.Info($"[{DiagnosticProcessLogName}] ProcessLog initialized at {diagnosticLogDirectory}");
    }

    private void EnsureAttachedItems()
    {
        _endpointAttached ??= Attach(_endpointItem, "Status/Endpoint");
        _connectionAttached ??= Attach(_connectionItem, "Status/Connection");
        _moduleCountAttached ??= Attach(_moduleCountItem, "Status/ModuleCount");
        _messageCounterAttached ??= Attach(_messageCounterItem, "Status/MessageCounter");
        _monitorCounterAttached ??= Attach(_monitorCounterItem, "Status/MonitorCounter");
        _lastFrameAttached ??= Attach(_lastFrameItem, "Status/LastFrame");
        _focusedModuleAttached ??= Attach(_focusedModuleItem, "Focus/Module");
        _focusedValueAttached ??= Attach(_focusedValueItem, "Focus/Value");
        _focusedStateAttached ??= Attach(_focusedStateItem, "Focus/State");
        _focusedUnitAttached ??= Attach(_focusedUnitItem, "Focus/Unit");
        _focusedSetActualAttached ??= Attach(_focusedSetActualItem, "Focus/SetActual");
        _focusedSetWriteAttached ??= Attach(_focusedSetWriteItem, "Focus/SetWrite");
        _focusedOutActualAttached ??= Attach(_focusedOutActualItem, "Focus/OutActual");
        _focusedOutWriteAttached ??= Attach(_focusedOutWriteItem, "Focus/OutWrite");

        for (var index = 0; index < ModuleSlotCount; index++)
        {
            _moduleSlotAttachedItems[index] ??= Attach(_moduleSlotItems[index], $"Modules/Slot{index + 1}");
        }
    }

    private void PublishCommands()
    {
        PublishCommand("FocusPrevious", FocusPreviousModule, "Focus previous discovered UDL module");
        PublishCommand("FocusNext", FocusNextModule, "Focus next discovered UDL module");
    }

    private void PublishBoundItems()
    {
        if (_endpointAttached is not null)
        {
            PublishItem(_endpointAttached);
        }

        if (_connectionAttached is not null)
        {
            PublishItem(_connectionAttached);
        }

        if (_moduleCountAttached is not null)
        {
            PublishItem(_moduleCountAttached);
        }

        if (_messageCounterAttached is not null)
        {
            PublishItem(_messageCounterAttached);
        }

        if (_monitorCounterAttached is not null)
        {
            PublishItem(_monitorCounterAttached);
        }

        if (_lastFrameAttached is not null)
        {
            PublishItem(_lastFrameAttached);
        }

        if (_focusedModuleAttached is not null)
        {
            PublishItem(_focusedModuleAttached);
        }

        if (_focusedValueAttached is not null)
        {
            PublishItem(_focusedValueAttached);
        }

        if (_focusedStateAttached is not null)
        {
            PublishItem(_focusedStateAttached);
        }

        if (_focusedUnitAttached is not null)
        {
            PublishItem(_focusedUnitAttached);
        }

        if (_focusedSetActualAttached is not null)
        {
            PublishItem(_focusedSetActualAttached);
        }

        if (_focusedSetWriteAttached is not null)
        {
            PublishItem(_focusedSetWriteAttached);
        }

        if (_focusedOutActualAttached is not null)
        {
            PublishItem(_focusedOutActualAttached);
        }

        if (_focusedOutWriteAttached is not null)
        {
            PublishItem(_focusedOutWriteAttached);
        }

        foreach (var attachedSlot in _moduleSlotAttachedItems)
        {
            if (attachedSlot is not null)
            {
                PublishItem(attachedSlot);
            }
        }
    }

    private void StartClient()
    {
        if (_client is not null)
        {
            return;
        }

        _client = new UdlRuntimeClient("DemoBook");
        _client.FrameReceived += OnFrameReceived;
        _client.Diagnostic += OnClientDiagnostic;
        LogDiagnostic($"creating client endpoint={EndpointDisplay}");
        _client.Open(EndpointAddress, EndpointPort);
        Interlocked.Exchange(ref _messageCount, 0);
        Interlocked.Exchange(ref _monitorLoopCount, 0);
        _connectionState = _client.LocalPort > 0 ? "Open" : "OpenFailed";
        _connectionItem.Value = _connectionState;
        LogDiagnostic($"client open result connection={_connectionState} endpoint={EndpointDisplay} localPort={_client.LocalPort}");
        LogReceive("monitor started");
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
        WaitForCompletion(_monitorTask);
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
                var monitorCount = Interlocked.Increment(ref _monitorLoopCount);
                _connectionItem.Value = _connectionState;
                _messageCounterItem.Value = Interlocked.Read(ref _messageCount);
                _lastFrameItem.Value = _lastFrameText;
                _monitorCounterItem.Value = monitorCount;

                if (monitorCount == 1 || monitorCount % 20 == 0)
                {
                    LogDiagnostic($"alive loop={monitorCount} messages={Interlocked.Read(ref _messageCount)} connection={_connectionState} localPort={_client?.LocalPort ?? 0}");
                }

                MonitorClientItems();
                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void MonitorClientItems()
    {
        var client = _client;
        if (client is null)
        {
            return;
        }

        var modules = client.Items.GetDictionary()
            .OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static entry => entry.Value)
            .OfType<UdlRuntimeModule>()
            .ToArray();

        _moduleCountItem.Value = modules.Length;
        _currentModuleNames = modules.Select(static module => module.Name ?? string.Empty)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        if (modules.Length == 0 && Interlocked.Read(ref _monitorLoopCount) <= 8)
        {
            LogDiagnostic($"monitor snapshot modules=0 items={client.Items.GetDictionary().Count} messageCounter={Interlocked.Read(ref _messageCount)}");
        }

        EnsureFocusedModule(modules);
        UpdateModuleSlots(modules);
        var focusedModule = GetFocusedModule(modules);
        UpdateFocusedModuleItems(focusedModule);

        if (focusedModule is not null)
        {
            LogValue(focusedModule, "Value");
            LogValue(focusedModule.State, "State");
            LogValue(focusedModule.Set, "Set");
            LogValue(focusedModule.Out, "Out");
            LogValue(focusedModule.Unit, "Unit");
            LogValue(focusedModule.Alert, "Alert");
        }
    }

    private void OnFrameReceived(uint id, byte dlc, byte[] data)
    {
        Interlocked.Increment(ref _messageCount);
        _lastFrameText = $"0x{id:X3} [{dlc}] {FormatBytes(data, dlc)}";
        if (ShouldSample(ref _rawLogCount, 8, 50))
        {
            LogReceive($"RAW 0x{id:X3} DLC={dlc} DATA={FormatBytes(data, dlc)}");
        }
    }

    private void UpdateModuleSlots(UdlRuntimeModule[] modules)
    {
        for (var index = 0; index < ModuleSlotCount; index++)
        {
            var slot = _moduleSlotItems[index];
            if (index < modules.Length)
            {
                var module = modules[index];
                var moduleName = module.Name ?? $"Module {index + 1}";
                slot.Params["Text"].Value = moduleName;
                slot.Params["Unit"].Value = module.Unit.Value?.ToString() ?? string.Empty;
                slot.Value = BuildModuleSummary(module);
            }
            else
            {
                slot.Params["Text"].Value = $"Slot {index + 1}";
                slot.Params["Unit"].Value = string.Empty;
                slot.Value = "waiting";
            }
        }
    }

    private void EnsureFocusedModule(UdlRuntimeModule[] modules)
    {
        if (modules.Length == 0)
        {
            _focusedModuleName = null;
            return;
        }

        if (!string.IsNullOrWhiteSpace(_focusedModuleName)
            && modules.Any(module => string.Equals(module.Name, _focusedModuleName, StringComparison.Ordinal)))
        {
            return;
        }

        _focusedModuleName = modules[0].Name;
        if (!string.IsNullOrWhiteSpace(_focusedModuleName))
        {
            LogReceive($"focused module changed to {_focusedModuleName}");
        }
    }

    private UdlRuntimeModule? GetFocusedModule(UdlRuntimeModule[] modules)
    {
        if (modules.Length == 0 || string.IsNullOrWhiteSpace(_focusedModuleName))
        {
            return null;
        }

        return modules.FirstOrDefault(module => string.Equals(module.Name, _focusedModuleName, StringComparison.Ordinal));
    }

    private void UpdateFocusedModuleItems(UdlRuntimeModule? module)
    {
        if (module is null)
        {
            ResetFocusedItems();
            return;
        }

        _focusedModuleItem.Value = module.Name ?? "-";
        _focusedValueItem.Value = module.Value ?? 0f;
        _focusedStateItem.Value = module.State.Value ?? 0;
        _focusedUnitItem.Value = module.Unit.Value ?? string.Empty;
        _focusedSetActualItem.Value = module.Set.Value ?? 0f;
        _focusedOutActualItem.Value = module.Out.Value ?? 0f;

        _isSyncingWriteProxyItems = true;
        try
        {
            _focusedSetWriteItem.Value = ReadWriteRequest(module.Set, module.Set.Value);
            _focusedOutWriteItem.Value = ReadWriteRequest(module.Out, module.Out.Value);
        }
        finally
        {
            _isSyncingWriteProxyItems = false;
        }
    }

    private void FocusPreviousModule()
    {
        ShiftFocusedModule(-1);
    }

    private void FocusNextModule()
    {
        ShiftFocusedModule(1);
    }

    private void ShiftFocusedModule(int delta)
    {
        if (_currentModuleNames.Length == 0)
        {
            return;
        }

        var currentIndex = Array.FindIndex(_currentModuleNames, name => string.Equals(name, _focusedModuleName, StringComparison.Ordinal));
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = (currentIndex + delta + _currentModuleNames.Length) % _currentModuleNames.Length;
        _focusedModuleName = _currentModuleNames[nextIndex];
        LogReceive($"focused module changed to {_focusedModuleName}");
    }

    private void OnFocusedSetWriteChanged(object? sender, ItemChangedEventArgs e)
    {
        if (!string.Equals(e.ParameterName, "Value", StringComparison.Ordinal) || _isSyncingWriteProxyItems)
        {
            return;
        }

        ApplyWriteRequest(static module => module.Set, (object?)_focusedSetWriteItem.Value, "Set");
    }

    private void OnFocusedOutWriteChanged(object? sender, ItemChangedEventArgs e)
    {
        if (!string.Equals(e.ParameterName, "Value", StringComparison.Ordinal) || _isSyncingWriteProxyItems)
        {
            return;
        }

        ApplyWriteRequest(static module => module.Out, (object?)_focusedOutWriteItem.Value, "Out");
    }

    private void ApplyWriteRequest(Func<UdlRuntimeModule, Item> selector, object? rawValue, string label)
    {
        var client = _client;
        if (client is null || string.IsNullOrWhiteSpace(_focusedModuleName))
        {
            return;
        }

        if (!client.Items.Has(_focusedModuleName) || client.Items[_focusedModuleName] is not UdlRuntimeModule module)
        {
            return;
        }

        if (!TryConvertToFloat(rawValue, out var floatValue))
        {
            LogReceive($"ignored invalid {label} write value: {rawValue}");
            return;
        }

        var targetItem = selector(module);
        targetItem.Params["Write"].Value = floatValue;
        LogReceive($"TX request {_focusedModuleName}.{label} = {floatValue:0.###}");
    }

    private void ResetFocusedItems()
    {
        _focusedModuleItem.Value = "-";
        _focusedValueItem.Value = 0f;
        _focusedStateItem.Value = 0;
        _focusedUnitItem.Value = string.Empty;
        _focusedSetActualItem.Value = 0f;
        _focusedOutActualItem.Value = 0f;

        _isSyncingWriteProxyItems = true;
        try
        {
            _focusedSetWriteItem.Value = 0f;
            _focusedOutWriteItem.Value = 0f;
        }
        finally
        {
            _isSyncingWriteProxyItems = false;
        }
    }

    private void ClearModuleSlots()
    {
        for (var index = 0; index < ModuleSlotCount; index++)
        {
            _moduleSlotItems[index].Params["Text"].Value = $"Slot {index + 1}";
            _moduleSlotItems[index].Params["Unit"].Value = string.Empty;
            _moduleSlotItems[index].Value = "waiting";
        }
    }

    private static object ReadWriteRequest(Item item, object? fallbackValue)
    {
        if (item.Params.Has("Write") && item.Params["Write"].Value is not null)
        {
            return item.Params["Write"].Value;
        }

        return fallbackValue ?? 0f;
    }

    private static string BuildModuleSummary(UdlRuntimeModule module)
    {
        var unit = module.Unit.Value?.ToString();
        var suffix = string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}";
        return $"V={FormatValue(module.Value)}{suffix} | Set={FormatValue(module.Set.Value)} | Out={FormatValue(module.Out.Value)} | State={FormatValue(module.State.Value)}";
    }

    private void LogValue(Item item, string label)
    {
        var itemName = item.Name ?? label;
        var cacheKey = item.Path ?? itemName;
        var formattedValue = FormatValue(item.Value);

        if (_lastLoggedValues.TryGetValue(cacheKey, out var previousValue)
            && string.Equals(previousValue, formattedValue, StringComparison.Ordinal))
        {
            return;
        }

        _lastLoggedValues[cacheKey] = formattedValue;
        LogReceive($"RX {itemName} ({label}) = {formattedValue}");
    }

    private void OnClientDiagnostic(string message)
    {
        if (ShouldLogDiagnostic(message))
        {
            LogDiagnostic(message);
        }

        if (message.Contains("rx packet", StringComparison.OrdinalIgnoreCase)
            || message.Contains("OnCanMessageReceived", StringComparison.OrdinalIgnoreCase))
        {
            _connectionState = "Receiving";
        }
        else if (message.Contains("open completed", StringComparison.OrdinalIgnoreCase)
                 || message.Contains("localPort=", StringComparison.OrdinalIgnoreCase)
                 || message.Contains("udp socket created", StringComparison.OrdinalIgnoreCase)
                 || message.Contains("client open result", StringComparison.OrdinalIgnoreCase)
                 || message.Contains("rx thread started", StringComparison.OrdinalIgnoreCase))
        {
            _connectionState = "Open";
        }
        else if (message.Contains("error=", StringComparison.OrdinalIgnoreCase)
                 || message.Contains("failed", StringComparison.OrdinalIgnoreCase))
        {
            _connectionState = "Error";
        }
        else if (message.Contains("close", StringComparison.OrdinalIgnoreCase)
                 || message.Contains("dispose", StringComparison.OrdinalIgnoreCase))
        {
            _connectionState = "Closed";
        }
    }

    private void LogReceive(string message)
    {
        _receiveProcessLog?.Info($"[{ReceiveProcessLogName}] {message}");
    }

    private void LogDiagnostic(string message)
    {
        _diagnosticProcessLog?.Info($"[{DiagnosticProcessLogName}] {message}");
    }

    private static bool ShouldLogDiagnostic(string message)
    {
        if (message.Contains("error=", StringComparison.OrdinalIgnoreCase)
            || message.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("open ", StringComparison.OrdinalIgnoreCase)
            || message.Contains("close", StringComparison.OrdinalIgnoreCase)
            || message.Contains("dispose", StringComparison.OrdinalIgnoreCase)
            || message.Contains("bound", StringComparison.OrdinalIgnoreCase)
            || message.Contains("fallback", StringComparison.OrdinalIgnoreCase)
            || message.Contains("alive loop=", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return message.Contains("rx packet", StringComparison.OrdinalIgnoreCase)
               || message.Contains("OnCanMessageReceived", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldSample(ref long counter, long initialBurst, long every)
    {
        var current = Interlocked.Increment(ref counter);
        return current <= initialBurst || current % every == 0;
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "<null>",
            float floatValue => floatValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static bool TryConvertToFloat(object? value, out float converted)
    {
        converted = 0f;
        if (value is null)
        {
            return false;
        }

        switch (value)
        {
            case float floatValue:
                converted = floatValue;
                return true;
            case double doubleValue:
                converted = (float)doubleValue;
                return true;
            case int intValue:
                converted = intValue;
                return true;
            case long longValue:
                converted = longValue;
                return true;
            case string text when float.TryParse(text, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.InvariantCulture, out var parsed):
                converted = parsed;
                return true;
            default:
                try
                {
                    converted = Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    return false;
                }
        }
    }

    private static string FormatBytes(byte[] data, byte dlc)
    {
        if (data.Length == 0 || dlc == 0)
        {
            return "<empty>";
        }

        var length = Math.Min(data.Length, dlc);
        var parts = new string[length];
        for (var index = 0; index < length; index++)
        {
            parts[index] = data[index].ToString("X2");
        }

        return string.Join(" ", parts);
    }

    private static Item CreateStatusItem(string text, string path, string unit, object initialValue)
    {
        var item = new Item(name: text, path: path);
        item.Params["Text"].Value = text;
        item.Params["Unit"].Value = unit;
        item.Value = initialValue;
        return item;
    }

    private static Item[] CreateModuleSlotItems()
    {
        var items = new Item[ModuleSlotCount];
        for (var index = 0; index < ModuleSlotCount; index++)
        {
            var item = CreateStatusItem($"Slot{index + 1}", "Runtime/DemoBook/UdlClient/ModuleSlots", string.Empty, "waiting");
            item.Params["Text"].Value = $"Slot {index + 1}";
            items[index] = item;
        }

        return items;
    }

    private static void WaitForCompletion(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            task.Wait(500);
        }
        catch (AggregateException exception) when (exception.InnerExceptions.All(static ex => ex is OperationCanceledException))
        {
        }
        catch (OperationCanceledException)
        {
        }
    }
}