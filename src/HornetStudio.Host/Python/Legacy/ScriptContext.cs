using System;
using HornetStudio.Contracts;

namespace HornetStudio.Host.Python.Legacy;

public interface IScriptContext
{
    IScriptSignal GetSignal(string id);
    void SetValue(string id, object? value);
    void Log(string message);
}

public interface IScriptSignal
{
    string Id { get; }
    object? Value { get; set; }
}

internal sealed class SignalProxy : IScriptSignal
{
    private readonly ISignal _inner;

    public SignalProxy(ISignal inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public string Id => _inner.Descriptor.Id;

    public object? Value
    {
        get => _inner.Value;
        set => _inner.Value = value;
    }
}

internal sealed class ScriptContext : IScriptContext
{
    private readonly ISignalRegistry _signals;
    private readonly Action<string, object?>? _valueWriter;

    public ScriptContext(ISignalRegistry signals, Action<string, object?>? valueWriter = null)
    {
        _signals = signals ?? throw new ArgumentNullException(nameof(signals));
        _valueWriter = valueWriter;
    }

    public IScriptSignal GetSignal(string id)
    {
        if (!_signals.TryGetById(id, out var signal) || signal is null)
        {
            throw new InvalidOperationException($"Signal '{id}' not found.");
        }

        return new SignalProxy(signal);
    }

    public void SetValue(string id, object? value)
    {
        if (_valueWriter is not null)
        {
            _valueWriter(id, value);
            return;
        }

        if (_signals.TryGetById(id, out var signal) && signal is not null)
        {
            signal.Value = value;
            return;
        }

        throw new InvalidOperationException($"Signal '{id}' not found.");
    }

    public void Log(string message)
    {
        HornetStudio.Host.Core.LogInfo($"[Python] {message}");
    }
}