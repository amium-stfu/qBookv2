using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace Amium.Host.Python.Client;

public static class PythonClientRuntimeRegistry
{
    private sealed class Registration
    {
        public required string TargetPath { get; init; }
        public required string DisplayName { get; init; }
        public required PythonClient Client { get; init; }
    }

    private static readonly ConcurrentDictionary<string, Registration> Registrations = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(string targetPath, string displayName, PythonClient client)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new ArgumentException("Target path must not be empty.", nameof(targetPath));
        }

        Registrations[targetPath.Trim()] = new Registration
        {
            TargetPath = targetPath.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? targetPath.Trim() : displayName.Trim(),
            Client = client ?? throw new ArgumentNullException(nameof(client))
        };
    }

    public static void Unregister(string targetPath, PythonClient client)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || client is null)
        {
            return;
        }

        if (Registrations.TryGetValue(targetPath.Trim(), out var registration)
            && ReferenceEquals(registration.Client, client))
        {
            Registrations.TryRemove(targetPath.Trim(), out _);
        }
    }

    public static void Unregister(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        Registrations.TryRemove(targetPath.Trim(), out _);
    }

    public static bool TryGetClient(string targetPath, out PythonClient? client)
    {
        client = null;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return false;
        }

        if (!Registrations.TryGetValue(targetPath.Trim(), out var registration))
        {
            return false;
        }

        client = registration.Client;
        return true;
    }

    public static IReadOnlyList<string> GetFunctionNames(string targetPath)
    {
        if (!TryGetClient(targetPath, out var client) || client is null)
        {
            return Array.Empty<string>();
        }

        return client.Functions
            .Select(static function => function.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> GetRegisteredTargetPaths()
        => Registrations.Keys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}