using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Amium.Logging;
using Amium.Items;

namespace Amium.Host;

public enum DataChangeKind
{
    SnapshotUpserted,
    ValueUpdated,
    ParameterUpdated
}

public sealed class DataChangedEventArgs : EventArgs
{
    public DataChangedEventArgs(string key, Item item, DataChangeKind changeKind, string? parameterName = null, ulong? timestamp = null)
    {
        Key = key;
        Item = item;
        ChangeKind = changeKind;
        ParameterName = parameterName;
        Timestamp = timestamp ?? (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public string Key { get; }
    public Item Item { get; }
    public DataChangeKind ChangeKind { get; }
    public string? ParameterName { get; }
    public ulong Timestamp { get; }
}

public interface IDataRegistry
{
    event EventHandler<DataChangedEventArgs>? ItemChanged;
    event EventHandler<DataChangedEventArgs>? RegistryChanged;

    IReadOnlyCollection<string> GetAllKeys();
    bool TryGet(string key, out Item? value);
    Item UpsertSnapshot(string key, Item snapshot, bool pruneMissingMembers = false);
    bool UpdateValue(string key, object? value, ulong? timestamp = null);
    bool UpdateParameter(string key, string parameterName, object? value, ulong? timestamp = null);
    bool Remove(string key);
}

public sealed class DataRegistry : IDataRegistry
{
    private readonly ConcurrentDictionary<string, Item> _items = new();

    public event EventHandler<DataChangedEventArgs>? ItemChanged;
    public event EventHandler<DataChangedEventArgs>? RegistryChanged;

    public IReadOnlyCollection<string> GetAllKeys() => _items.Keys.ToArray();

    public bool TryGet(string key, out Item? value) => _items.TryGetValue(key, out value);

    public Item UpsertSnapshot(string key, Item snapshot, bool pruneMissingMembers = false)
    {
        var added = false;
        var item = _items.AddOrUpdate(
            key,
            _ =>
            {
                added = true;
                return snapshot;
            },
            (_, existing) =>
            {
                MergeItem(existing, snapshot, pruneMissingMembers);
                return existing;
            });

        RaiseItemChanged(key, item, DataChangeKind.SnapshotUpserted);

        if (added)
        {
            RaiseRegistryChanged(key, item, DataChangeKind.SnapshotUpserted);
            var keys = string.Join(", ", _items.Keys.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase));
            var message = $"DataRegistry.UpsertSnapshot key={key} count={_items.Count} keys=[{keys}]";
            Debug.WriteLine(message);
            HostLogger.Log.Information(message);
        }

        return item;
    }

    public bool UpdateValue(string key, object? value, ulong? timestamp = null)
    {
        if (!_items.TryGetValue(key, out var item))
        {
            return false;
        }

        item.Value = value!;
        if (timestamp.HasValue && item.Params.Has("Value"))
        {
            item.Params["Value"].LastUpdate = timestamp.Value;
        }

        RaiseItemChanged(key, item, DataChangeKind.ValueUpdated, timestamp: timestamp);
        return true;
    }

    public bool UpdateParameter(string key, string parameterName, object? value, ulong? timestamp = null)
    {
        if (!_items.TryGetValue(key, out var item) || !item.Params.Has(parameterName))
        {
            return false;
        }

        item.Params[parameterName].Value = value!;
        if (timestamp.HasValue)
        {
            item.Params[parameterName].LastUpdate = timestamp.Value;
        }

        RaiseItemChanged(key, item, DataChangeKind.ParameterUpdated, parameterName, timestamp);
        return true;
    }

    public bool Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return _items.TryRemove(key, out _);
    }

    private void RaiseItemChanged(string key, Item item, DataChangeKind changeKind, string? parameterName = null, ulong? timestamp = null)
    {
        ItemChanged?.Invoke(this, new DataChangedEventArgs(key, item, changeKind, parameterName, timestamp));
    }

    private void RaiseRegistryChanged(string key, Item item, DataChangeKind changeKind, string? parameterName = null, ulong? timestamp = null)
    {
        RegistryChanged?.Invoke(this, new DataChangedEventArgs(key, item, changeKind, parameterName, timestamp));
    }


    private static void MergeItem(Item target, Item source, bool pruneMissingMembers)
    {
        MergeParameters(target, source, pruneMissingMembers);
        MergeChildren(target, source, pruneMissingMembers);
    }

    private static void MergeParameters(Item target, Item source, bool pruneMissingMembers)
    {
        foreach (var parameterEntry in source.Params.GetDictionary())
        {
            var targetParameter = target.Params[parameterEntry.Key];
            targetParameter.Value = parameterEntry.Value.Value;
            targetParameter.LastUpdate = parameterEntry.Value.LastUpdate;
            targetParameter.Path = parameterEntry.Value.Path;
        }

        if (!pruneMissingMembers)
        {
            return;
        }

        foreach (var parameterName in target.Params.GetDictionary().Keys)
        {
            if (!source.Params.Has(parameterName))
            {
                target.Params.Remove(parameterName);
            }
        }
    }

    private static void MergeChildren(Item target, Item source, bool pruneMissingMembers)
    {
        foreach (var childEntry in source.GetDictionary())
        {
            if (target.Has(childEntry.Key))
            {
                MergeItem(target[childEntry.Key], childEntry.Value, pruneMissingMembers);
                continue;
            }

            target[childEntry.Key] = childEntry.Value.Clone();
        }

        if (!pruneMissingMembers)
        {
            return;
        }

        foreach (var childName in target.GetDictionary().Keys)
        {
            if (!source.Has(childName))
            {
                target.Remove(childName);
            }
        }
    }
}


public interface IProcessLogRegistry
{
    IReadOnlyCollection<string> GetAllNames();
    bool TryGet(string name, out ProcessLog? value);
    void Register(string name, ProcessLog log);
}

public sealed class ProcessLogRegistry : IProcessLogRegistry
{
    private readonly ConcurrentDictionary<string, ProcessLog> _logs = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> GetAllNames() => _logs.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();

    public bool TryGet(string name, out ProcessLog? value) => _logs.TryGetValue(name, out value);

    public void Register(string name, ProcessLog log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _logs[name] = log ?? throw new ArgumentNullException(nameof(log));
    }
}
public sealed class HostCommand
{
    public HostCommand(string name, Action<object?> execute, Func<object?, bool>? canExecute = null, string? description = null, Type? parameterType = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Command name must not be empty.", nameof(name));
        }

        Name = name;
        Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        CanExecute = canExecute;
        Description = description;
        ParameterType = parameterType;
    }

    public string Name { get; }
    public Action<object?> Execute { get; }
    public Func<object?, bool>? CanExecute { get; }
    public string? Description { get; }
    public Type? ParameterType { get; }
}

public interface ICommandRegistry
{
    IReadOnlyCollection<HostCommand> GetAll();
    void Register(HostCommand command);
    bool TryGet(string name, out HostCommand? command);
    bool CanExecute(string name, object? parameter = null);
    bool Execute(string name, object? parameter = null);
}

public sealed class CommandRegistry : ICommandRegistry
{
    private readonly ConcurrentDictionary<string, HostCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<HostCommand> GetAll() => _commands.Values.OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public void Register(HostCommand command)
    {
        _commands[command.Name] = command;
    }

    public bool TryGet(string name, out HostCommand? command) => _commands.TryGetValue(name, out command);

    public bool CanExecute(string name, object? parameter = null)
    {
        if (!TryGet(name, out var command) || command is null)
        {
            return false;
        }

        return command.CanExecute?.Invoke(parameter) ?? true;
    }

    public bool Execute(string name, object? parameter = null)
    {
        if (!TryGet(name, out var command) || command is null || !(command.CanExecute?.Invoke(parameter) ?? true))
        {
            return false;
        }

        command.Execute(parameter);
        return true;
    }
}

public static class HostRegistries
{
    static HostRegistries()
    {
        Data = new DataRegistry();
        Commands = new CommandRegistry();
        Cameras = new CameraRegistry();
        ProcessLogs = new ProcessLogRegistry();
        UiPublisher.Publish("Logs/Host", HostLogger.ProcessLog, "Host");

        var assembly = typeof(HostRegistries).Assembly;
        var loadContext = AssemblyLoadContext.GetLoadContext(assembly);
        var message = $"HostRegistries initialized. pid={ProcessId} session={SessionId} sessionUtc={SessionStartedUtc:O} dataRegistryId={DataRegistryId} Assembly={assembly.Location} LoadContext={loadContext?.Name ?? "<default>"}";
        Debug.WriteLine(message);
        HostLogger.Log.Information(message);
    }

    public static DateTimeOffset SessionStartedUtc { get; } = DateTimeOffset.UtcNow;
    public static int ProcessId { get; } = Environment.ProcessId;
    public static string SessionId { get; } = Guid.NewGuid().ToString("N");
    public static IDataRegistry Data { get; }
    public static ICommandRegistry Commands { get; }
    public static ICameraRegistry Cameras { get; }
    public static IProcessLogRegistry ProcessLogs { get; }
    public static int DataRegistryId => RuntimeHelpers.GetHashCode(Data);
}




