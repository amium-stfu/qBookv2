using System;
using System.Collections.Generic;

namespace HornetStudio.Contracts;

public enum SignalDataType
{
    Unknown,
    Boolean,
    Integer,
    Float,
    String,
    Object
}

public sealed class SignalDescriptor
{
    public SignalDescriptor(
        string id,
        string name,
        SignalDataType dataType,
        string? unit = null,
        string? format = null,
        string? sourcePath = null,
        bool isWritable = false,
        string? category = null)
    {
        Id = string.IsNullOrWhiteSpace(id)
            ? throw new ArgumentException("Signal id must not be empty.", nameof(id))
            : id;

        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Signal name must not be empty.", nameof(name))
            : name;

        DataType = dataType;
        Unit = unit;
        Format = format;
        SourcePath = sourcePath;
        IsWritable = isWritable;
        Category = category;
    }

    public string Id { get; }
    public string Name { get; }
    public SignalDataType DataType { get; }
    public string? Unit { get; }
    public string? Format { get; }

    /// <summary>
    /// Kanonischer Pfad zur Datenquelle im Host (z.B. Registry- oder Item-Pfad).
    /// </summary>
    public string? SourcePath { get; }

    public bool IsWritable { get; }
    public string? Category { get; }
}

public sealed class SignalValueChangedEventArgs : EventArgs
{
    public SignalValueChangedEventArgs(SignalDescriptor descriptor, object? oldValue, object? newValue, DateTimeOffset timestamp)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        OldValue = oldValue;
        NewValue = newValue;
        Timestamp = timestamp;
    }

    public SignalDescriptor Descriptor { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }
    public DateTimeOffset Timestamp { get; }
}

public interface ISignal
{
    SignalDescriptor Descriptor { get; }

    object? Value { get; set; }

    event EventHandler<SignalValueChangedEventArgs>? ValueChanged;
}

public interface ISignalRegistry
{
    event EventHandler<SignalValueChangedEventArgs>? SignalChanged;

    IReadOnlyCollection<SignalDescriptor> GetAllDescriptors();

    bool TryGetById(string id, out ISignal? signal);

    bool TryGetBySourcePath(string sourcePath, out ISignal? signal);
}
