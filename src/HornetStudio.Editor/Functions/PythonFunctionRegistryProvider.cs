using System;
using System.Collections.Generic;
using System.Linq;
using HornetStudio.Editor.Models;
using HornetStudio.Host.Python.Client;

namespace HornetStudio.Editor.Functions;

/// <summary>
/// Reads read-only Python function entries from the runtime registry.
/// </summary>
public sealed class PythonFunctionRegistryProvider : IFunctionRegistryEntryProvider
{
    /// <inheritdoc />
    public IReadOnlyList<FunctionCatalogEntry> GetEntries(string folderDirectory)
    {
        return PythonClientRuntimeRegistry.GetRegisteredTargetPaths()
            .SelectMany(GetEntriesForTargetPath)
            .OrderBy(entry => entry.SourceIdentifier, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<FunctionCatalogEntry> GetEntriesForTargetPath(string targetPath)
    {
        var normalizedTargetPath = NormalizeValue(targetPath);
        if (string.IsNullOrWhiteSpace(normalizedTargetPath))
        {
            return [];
        }

        return PythonClientRuntimeRegistry.GetFunctionNames(normalizedTargetPath)
            .Select(NormalizeValue)
            .Where(static functionName => !string.IsNullOrWhiteSpace(functionName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(functionName => new FunctionCatalogEntry
            {
                Reference = $"python:{normalizedTargetPath}:{functionName}",
                Name = functionName,
                Kind = FunctionCatalogKind.Python,
                Source = FunctionCatalogSource.PythonApplication,
                SourceIdentifier = normalizedTargetPath,
                DisplaySource = GetDisplaySource(),
                CanEdit = false,
                CanDelete = false,
                CanRun = true,
                IsValid = true,
                StatusText = string.Empty
            })
            .ToArray();
    }

    private static string NormalizeValue(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

    private static string GetDisplaySource()
        => "Applications/Python";
}