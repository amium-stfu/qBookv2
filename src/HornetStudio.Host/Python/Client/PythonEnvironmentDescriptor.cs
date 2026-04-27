using System;
using System.IO;
using HornetStudio.Host;

namespace HornetStudio.Host.Python.Client;

/// <summary>
/// Lightweight descriptor for a Python environment (Env).
///
/// Intended usage:
/// - Describe the Env name and root folder (e.g. <Project>/Python/ModbusClient).
/// - Optionally carry a default script path relative to the Env root.
/// - Provide a small helper to open the Env in an external editor.
///
/// This type does not own any runtime resources; it is a pure data/helper object.
/// Lifecycle and process management remain with higher-level managers.
/// </summary>
public sealed class PythonEnvironmentDescriptor
{
    public PythonEnvironmentDescriptor(string name, string rootPath, string? defaultScriptRelativePath = null)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Root path must not be empty.", nameof(rootPath));
        }

        Name = string.IsNullOrWhiteSpace(name) ? "PythonEnv" : name.Trim();
        RootPath = Path.GetFullPath(rootPath);
        DefaultScriptRelativePath = string.IsNullOrWhiteSpace(defaultScriptRelativePath)
            ? null
            : NormalizeRelativePath(defaultScriptRelativePath);
    }

    /// <summary>
    /// Display name of the environment (e.g. "ModbusClient", "pdfCreator").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Absolute root folder of the environment.
    /// Example: <Project>/Python/ModbusClient
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Optional default script path relative to <see cref="RootPath"/>.
    /// Example: "scripts/main.py". If null, a default of "main.py" is assumed.
    /// </summary>
    public string? DefaultScriptRelativePath { get; }

    /// <summary>
    /// Resolves the absolute path to the default script for this environment.
    /// If <paramref name="overrideRelativePath"/> is provided, it is used
    /// instead of <see cref="DefaultScriptRelativePath"/>.
    /// </summary>
    public string GetDefaultScriptPath(string? overrideRelativePath = null)
    {
        var relative = string.IsNullOrWhiteSpace(overrideRelativePath)
            ? (DefaultScriptRelativePath ?? "main.py")
            : NormalizeRelativePath(overrideRelativePath);

        return Path.GetFullPath(Path.Combine(RootPath, relative));
    }

    /// <summary>
    /// Opens the environment root folder in VS Code via the dedicated
    /// Python environment launcher. Returns false on failure and logs
    /// a warning via the host logger.
    /// </summary>
    public bool OpenInEditor(bool newWindow = true)
    {
        try
        {
            return VsCodeLauncher.OpenPythonEnvironmentFolder(RootPath, newWindow);
        }
        catch (Exception ex)
        {
            Core.LogWarn($"Failed to open Python environment in editor. Env={Name} Path={RootPath}", ex);
            return false;
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        var cleaned = path.Replace('\\', '/').Trim('/');
        return string.IsNullOrWhiteSpace(cleaned) ? "main.py" : cleaned;
    }
}
