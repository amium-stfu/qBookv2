using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HornetStudio.Contracts;
using Amium.Item;

namespace HornetStudio.Host;

internal sealed class DataRegistrySignal : ISignal
{
    private readonly IDataRegistry _dataRegistry;
    private readonly string _sourcePath;
    private object? _cachedValue;

    public DataRegistrySignal(IDataRegistry dataRegistry, string sourcePath, SignalDescriptor descriptor)
    {
        _dataRegistry = dataRegistry ?? throw new ArgumentNullException(nameof(dataRegistry));
        _sourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Source path must not be empty.", nameof(sourcePath))
            : sourcePath;
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

        if (_dataRegistry.TryGet(_sourcePath, out var item) && item is not null)
        {
            _cachedValue = item.Value;
        }
    }

    public SignalDescriptor Descriptor { get; }

    public object? Value
    {
        get => _cachedValue;
        set
        {
            _dataRegistry.UpdateValue(_sourcePath, value, null);
        }
    }

    public event EventHandler<SignalValueChangedEventArgs>? ValueChanged;

    internal void OnSourceValueUpdated(object? newValue)
    {
        var oldValue = _cachedValue;
        _cachedValue = newValue;
        var args = new SignalValueChangedEventArgs(Descriptor, oldValue, newValue, DateTimeOffset.UtcNow);
        ValueChanged?.Invoke(this, args);
    }
}

public sealed class SignalRegistry : ISignalRegistry
{
    private readonly IDataRegistry _dataRegistry;
    private readonly ConcurrentDictionary<string, DataRegistrySignal> _signalsBySourcePath = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DataRegistrySignal> _signalsById = new(StringComparer.OrdinalIgnoreCase);

    public SignalRegistry(IDataRegistry dataRegistry)
    {
        _dataRegistry = dataRegistry ?? throw new ArgumentNullException(nameof(dataRegistry));
        _dataRegistry.ItemChanged += OnDataRegistryItemChanged;
    }

    public event EventHandler<SignalValueChangedEventArgs>? SignalChanged;

    public IReadOnlyCollection<SignalDescriptor> GetAllDescriptors()
        => _signalsById.Values
            .Select(signal => signal.Descriptor)
            .Distinct()
            .ToArray();

    public bool TryGetById(string id, out ISignal? signal)
    {
        signal = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (_signalsById.TryGetValue(id, out var existing))
        {
            signal = existing;
            return true;
        }

        // Fallback: interpret id as source path if no explicit descriptor exists yet.
        if (!TryGetBySourcePath(id, out signal))
        {
            return false;
        }

        if (signal is DataRegistrySignal concrete)
        {
            _signalsById.TryAdd(concrete.Descriptor.Id, concrete);
        }

        return true;
    }

    public bool TryGetBySourcePath(string sourcePath, out ISignal? signal)
    {
        signal = null;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return false;
        }

        if (_signalsBySourcePath.TryGetValue(sourcePath, out var existing))
        {
            signal = existing;
            return true;
        }

        if (!_dataRegistry.TryGet(sourcePath, out var item) || item is null)
        {
            return false;
        }

        var descriptor = CreateDescriptorFromItem(sourcePath, item);
        var created = new DataRegistrySignal(_dataRegistry, sourcePath, descriptor);

        existing = _signalsBySourcePath.GetOrAdd(sourcePath, created);
        _signalsById.TryAdd(existing.Descriptor.Id, existing);

        signal = existing;
        return true;
    }

    private static SignalDescriptor CreateDescriptorFromItem(string sourcePath, Item item)
    {
        var name = item.Name ?? sourcePath;
        var unit = item.Params.Has("Unit") ? item.Params["Unit"].Value?.ToString() : null;
        var format = item.Params.Has("Format") ? item.Params["Format"].Value?.ToString() : null;

        var value = item.Params.Has("Value") ? item.Params["Value"].Value : null;
        var dataType = InferDataType(value);

        var isWritable = true; // optional: später z.B. über spezielles Flag steuern
        var category = item.Params.Has("Kind") ? item.Params["Kind"].Value?.ToString() : null;

        return new SignalDescriptor(
            id: sourcePath,
            name: name,
            dataType: dataType,
            unit: unit,
            format: format,
            sourcePath: sourcePath,
            isWritable: isWritable,
            category: category);
    }

    private static SignalDataType InferDataType(object? value)
    {
        if (value is null)
        {
            return SignalDataType.Unknown;
        }

        var type = Nullable.GetUnderlyingType(value.GetType()) ?? value.GetType();

        if (type == typeof(bool))
        {
            return SignalDataType.Boolean;
        }

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
                return SignalDataType.Integer;

            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return SignalDataType.Float;

            case TypeCode.String:
                return SignalDataType.String;

            default:
                return SignalDataType.Object;
        }
    }

    private void OnDataRegistryItemChanged(object? sender, DataChangedEventArgs e)
    {
        if (e.ChangeKind != DataChangeKind.ValueUpdated)
        {
            return;
        }

        if (!_signalsBySourcePath.TryGetValue(e.Key, out var signal))
        {
            return;
        }

        var currentValue = e.Item.Params.Has("Value") ? e.Item.Params["Value"].Value : e.Item.Value;
        signal.OnSourceValueUpdated(currentValue);

        var args = new SignalValueChangedEventArgs(signal.Descriptor, null, currentValue, DateTimeOffset.FromUnixTimeMilliseconds((long)e.Timestamp));
        SignalChanged?.Invoke(this, args);
    }
}
