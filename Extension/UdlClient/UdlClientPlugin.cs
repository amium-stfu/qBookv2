using System;
using System.IO;
using UiEditor.Contracts;

namespace UdlClient;

public sealed class UdlClientPlugin : IHostPlugin
{
    public PluginDescriptor Descriptor { get; } = new(
        id: "udl-client",
        name: "UdlClient",
        version: new Version(1, 0, 0, 0));

    public void Register(IPluginRegistration registration)
    {
        var assemblyPath = typeof(UdlClientPlugin).Assembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath))
        {
            registration.RegisterReferenceAssembly(assemblyPath);
        }

        registration.RegisterDriver(new DriverDescriptor(
            id: "udl-client.default",
            name: "UdlClient Driver",
            category: "UDL"));
    }
}
