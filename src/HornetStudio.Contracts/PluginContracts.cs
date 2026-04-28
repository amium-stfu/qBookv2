using System;
using System.Collections.Generic;

namespace HornetStudio.Contracts;

public sealed class PluginDescriptor
{
    public PluginDescriptor(string id, string name, Version version)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Plugin id must not be empty.", nameof(id)) : id;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Plugin name must not be empty.", nameof(name)) : name;
        Version = version ?? throw new ArgumentNullException(nameof(version));
    }

    public string Id { get; }
    public string Name { get; }
    public Version Version { get; }
}

public sealed class DriverDescriptor
{
    public DriverDescriptor(string id, string name, string category)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Driver id must not be empty.", nameof(id)) : id;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Driver name must not be empty.", nameof(name)) : name;
        Category = string.IsNullOrWhiteSpace(category) ? throw new ArgumentException("Driver category must not be empty.", nameof(category)) : category;
    }

    public string Id { get; }
    public string Name { get; }
    public string Category { get; }
}

public interface IPluginRegistration
{
    string HostDirectory { get; }
    string PluginDirectory { get; }
    void RegisterReferenceAssembly(string assemblyPath);
    void RegisterDriver(DriverDescriptor descriptor);
}

public interface IHostPlugin
{
    PluginDescriptor Descriptor { get; }
    void Register(IPluginRegistration registration);
}

public interface IBusAdapter
{
    string Name { get; }
}

public interface IDeviceDriver
{
    DriverDescriptor Descriptor { get; }
}

public interface IProtocolDriver : IDeviceDriver
{
    IReadOnlyCollection<string> SupportedProtocols { get; }
}
