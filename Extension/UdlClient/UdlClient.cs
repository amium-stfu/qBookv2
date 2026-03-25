using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Amium.Items;

namespace UdlClient;

public sealed class UdlClient : IDisposable
{
    private readonly string _itemsPath;
    private readonly object _sync = new();
    private CancellationTokenSource? _lifetime;
    private Task? _heartbeatTask;
    private Task? _writebackTask;
    private Can? _can;
    private DateTime _nextHeartbeatUtc = DateTime.UtcNow;
    private long _rxDispatchLogCount;
    private long _heartbeatLogCount;
    private long _ignoredFrameLogCount;
    private long _unknownTypeLogCount;

    public UdlClient(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A client name is required.", nameof(name));
        }

        Name = name.Trim();
        _itemsPath = $"Runtime/UdlClient/{Name}";
        Items = new ItemDictionary(_itemsPath);
    }

    public string Name { get; }
    public bool RemoteTime { get; set; }
    public ItemDictionary Items { get; }
    public event Action<uint, byte, byte[]>? FrameReceived;
    public event Action<string>? Diagnostic;
    public int LocalPort => _can?.LocalPort ?? 0;

    public void Open(string ip, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);

        Close();

        WriteDiagnostic($"open requested endpoint={ip}:{port}");

        var can = new Can(ip, port, OnCanDiagnostic);
        var lifetime = new CancellationTokenSource();

        can.MessageReceived += OnCanMessageReceived;

        lock (_sync)
        {
            _can = can;
            _lifetime = lifetime;
            _nextHeartbeatUtc = DateTime.UtcNow;
            _heartbeatTask = Task.Run(() => IdleLoopAsync(lifetime.Token), lifetime.Token);
            _writebackTask = Task.Run(() => WritebackLoopAsync(lifetime.Token), lifetime.Token);
        }

        WriteDiagnostic($"open completed localPort={can.LocalPort}");
    }

    public void Close()
    {
        Can? can;
        CancellationTokenSource? lifetime;
        Task? heartbeatTask;
        Task? writebackTask;

        lock (_sync)
        {
            can = _can;
            lifetime = _lifetime;
            heartbeatTask = _heartbeatTask;
            writebackTask = _writebackTask;

            _can = null;
            _lifetime = null;
            _heartbeatTask = null;
            _writebackTask = null;
        }

        if (can is not null)
        {
            can.MessageReceived -= OnCanMessageReceived;
            can.Diagnostic -= OnCanDiagnostic;
        }

        if (lifetime is not null)
        {
            lifetime.Cancel();
        }

        WaitForCompletion(heartbeatTask);
        WaitForCompletion(writebackTask);

        can?.Dispose();
        lifetime?.Dispose();
        WriteDiagnostic("close completed");
    }

    public void Dispose()
    {
        Close();
    }

    public void OnCanMessageReceived(uint id, byte dlc, byte[] data)
    {
        if (ShouldSample(ref _rxDispatchLogCount, 8, 100))
        {
            WriteDiagnostic($"OnCanMessageReceived id=0x{id:X3} dlc={dlc}");
        }

        if (data is null || dlc == 0)
        {
            WriteDiagnostic("OnCanMessageReceived ignored empty payload");
            return;
        }

        FrameReceived?.Invoke(id, dlc, data);

        if (id is >= 0x480 and <= 0x4FF)
        {
            HandleSubChannelPdo(id, dlc, data);
        }
        else if (id is >= 0x700 and <= 0x7FF)
        {
            HandleHeartbeat(id, dlc, data);
        }
        else
        {
            if (ShouldSample(ref _ignoredFrameLogCount, 4, 250))
            {
                WriteDiagnostic($"frame ignored id=0x{id:X3} dlc={dlc} data={FormatBytes(data, dlc)}");
            }
        }
    }

    private async Task IdleLoopAsync(CancellationToken token)
    {
        try
        {
            WriteDiagnostic("idle loop started");
            while (!token.IsCancellationRequested)
            {
                HbIdle();
                await Task.Delay(100, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            WriteDiagnostic($"idle loop error={exception.GetType().Name}: {exception.Message}");
        }
    }

    private async Task WritebackLoopAsync(CancellationToken token)
    {
        try
        {
            WriteDiagnostic("writeback loop started");
            while (!token.IsCancellationRequested)
            {
                foreach (var entry in Items.GetDictionary())
                {
                    if (entry.Value is not Module module)
                    {
                        continue;
                    }

                    if (!TryGetModuleId(module, out var moduleId))
                    {
                        continue;
                    }

                    ProcessWriteSet(moduleId, module);
                }

                await Task.Delay(20, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            WriteDiagnostic($"writeback loop error={exception.GetType().Name}: {exception.Message}");
        }
    }

    private void HandleSubChannelPdo(uint id, byte dlc, byte[] data)
    {
        if (dlc < 8 || data.Length < 8)
        {
            WriteDiagnostic($"subchannel ignored short frame id=0x{id:X3} dlc={dlc} len={data.Length}");
            return;
        }

        var moduleId = ((id & 0x7Fu) << 4) | ((uint)data[7] & 0x0Fu);
        var module = GetOrCreateModule(moduleId);
        module.WriteBack = true;

        var type = data[6];
        switch (type)
        {
            case 1:
                module.State.Value = Convert.ToInt32(Math.Round(BitConverter.ToSingle(data, 0), MidpointRounding.AwayFromZero));
                module.Command.Value = module.State.Value;
                break;

            case 2:
                module.Alert.Value = BitConverter.ToSingle(data, 0);
                break;

            case 3:
            {
                var value = BitConverter.ToSingle(data, 0);
                module.Value = value;
                module.Params["MetaData"].Value = (ushort)(data[4] | (data[5] << 8));
                break;
            }

            case 4:
                module.Set.Value = BitConverter.ToSingle(data, 0);
                break;

            case 5:
                module.Out.Value = BitConverter.ToSingle(data, 0);
                break;

            default:
                module.Params["LastType"].Value = type;
                module.Params["LastRaw"].Value = FormatBytes(data, dlc);
                if (ShouldSample(ref _unknownTypeLogCount, 8, 100))
                {
                    WriteDiagnostic($"subchannel unknown type={type} module={module.Name}");
                }
                break;
        }
    }

    private void HandleHeartbeat(uint id, byte dlc, byte[] data)
    {
    }

    private void HbIdle()
    {
        var can = _can;
        if (can is null)
        {
            return;
        }

        if (DateTime.UtcNow < _nextHeartbeatUtc)
        {
            return;
        }

        _nextHeartbeatUtc = DateTime.UtcNow.AddSeconds(1);
        if (ShouldSample(ref _heartbeatLogCount, 1, 60))
        {
            WriteDiagnostic("heartbeat tick active");
        }
        can.Transmit(new CanMessage(0x70E, new byte[] { 5, 4 }));

        if (RemoteTime)
        {
            return;
        }

        var milliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var bytes = BitConverter.GetBytes(milliseconds);

        can.Transmit(new CanMessage(0x100, new byte[]
        {
            bytes[0],
            bytes[1],
            bytes[2],
            bytes[3],
            bytes[4],
            bytes[5],
            0x00,
            0x08
        }));
    }

    private Module GetOrCreateModule(uint moduleId)
    {
        var key = FormatModuleName(moduleId);
        if (Items.Has(key) && Items[key] is Module existingModule)
        {
            return existingModule;
        }

        WriteDiagnostic($"create module {key} from moduleId=0x{moduleId:X3}");
        var module = new Module(key, _itemsPath);
        module.Params["ModuleId"].Value = moduleId;
        module.Params["Text"].Value = key;
        module.Params["Kind"].Value = "UdlModule";

        module.Set.Params["Text"].Value = $"{key} Set";
        module.Out.Params["Text"].Value = $"{key} Out";
        module.State.Params["Text"].Value = $"{key} State";
        module.Unit.Params["Text"].Value = $"{key} Unit";
        module.Alert.Params["Text"].Value = $"{key} Alert";
        module.Command.Params["Text"].Value = $"{key} Command";
        module.CommandSet.Params["Text"].Value = $"{key} Command Set";

        Items[key] = module;
        return module;
    }

    private void ProcessWriteSet(uint moduleId, Module module)
    {
        TryWrite(moduleId, module, 3);
        TryWrite(moduleId, module.Set, 4);
        TryWrite(moduleId, module.Out, 5);
    }

    private void TryWrite(uint moduleId, Item item, int function)
    {
        if (!TryGetWriteValue(item, out double desiredValue))
        {
            return;
        }

        if (!TryConvertToDouble(item.Value, out double currentValue))
        {
            return;
        }

        if (Math.Abs(desiredValue - currentValue) <= 0.0001)
        {
            return;
        }

        WriteDiagnostic($"write request moduleId=0x{moduleId:X3} function={function} current={currentValue:0.###} desired={desiredValue:0.###}");
        SendWritePdo(moduleId, desiredValue, function);
    }

    private void SendWritePdo(uint moduleId, double value, int function)
    {
        var can = _can;
        if (can is null)
        {
            return;
        }

        var writeId = GetWriteIdFromModule(moduleId);
        var data = new byte[8];

        Array.Copy(BitConverter.GetBytes((float)value), 0, data, 0, 4);
        data[4] = 0;
        data[5] = 0;
        data[6] = (byte)function;
        data[7] = (byte)(moduleId & 0x0F);

        WriteDiagnostic($"send write pdo id=0x{writeId:X3} function={function} moduleId=0x{moduleId:X3} data={FormatBytes(data, 8)}");
        can.Transmit(new CanMessage(writeId, data));
    }

    private static uint GetWriteIdFromModule(uint moduleId)
    {
        var baseId = (moduleId >> 4) & 0x7F;
        return 0x500 | baseId;
    }

    private static bool TryGetModuleId(Module module, out uint moduleId)
    {
        moduleId = 0;
        if (!module.Params.Has("ModuleId"))
        {
            return false;
        }

        var value = module.Params["ModuleId"].Value;
        return TryConvertToUInt32(value, out moduleId);
    }

    private static bool TryGetWriteValue(Item item, out double value)
    {
        value = 0;
        if (!item.Params.Has("Write"))
        {
            return false;
        }

        return TryConvertToDouble(item.Params["Write"].Value, out value) && !double.IsNaN(value);
    }

    private static bool TryConvertToUInt32(object? value, out uint converted)
    {
        converted = 0;
        if (value is null)
        {
            return false;
        }

        try
        {
            converted = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryConvertToDouble(object? value, out double converted)
    {
        converted = 0;
        if (value is null)
        {
            return false;
        }

        switch (value)
        {
            case double doubleValue:
                converted = doubleValue;
                return true;
            case float floatValue:
                converted = floatValue;
                return true;
            case string text when double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed):
                converted = parsed;
                return true;
            default:
                try
                {
                    converted = Convert.ToDouble(value, CultureInfo.InvariantCulture);
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
        if (dlc == 0 || data.Length == 0)
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

    private static string FormatModuleName(uint moduleId)
        => $"m{moduleId:X3}";

    private static bool ShouldSample(ref long counter, long initialBurst, long every)
    {
        var current = Interlocked.Increment(ref counter);
        return current <= initialBurst || current % every == 0;
    }

    private void OnCanDiagnostic(string message)
    {
        WriteDiagnostic(message);
    }

    private void WriteDiagnostic(string message)
    {
        var formatted = $"[UdlClient:{Name}] {message}";
        try
        {
            Diagnostic?.Invoke(formatted);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"UdlClient diagnostic callback failed: {exception}");
        }
    }

    private static void WaitForCompletion(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            task.Wait(250);
        }
        catch (AggregateException exception) when (exception.InnerExceptions.All(static ex => ex is OperationCanceledException))
        {
        }
        catch (OperationCanceledException)
        {
        }
    }
}