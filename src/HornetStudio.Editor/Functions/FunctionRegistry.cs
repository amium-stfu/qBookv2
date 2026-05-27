using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HornetStudio.Editor.Models;
using HornetStudio.Editor.Widgets.Workflow;

namespace HornetStudio.Editor.Functions;

/// <summary>
/// Provides widget-independent function lookup for one folder.
/// </summary>
public static class FunctionRegistry
{
    private const string DeclarativePrefix = "declarative:";
    private const string YamlPrefix = "yaml:";

    private static readonly IFunctionRegistryEntryProvider[] Providers =
    [
        new DeclarativeFunctionRegistryProvider(),
        new PythonFunctionRegistryProvider()
    ];

    /// <summary>
    /// Enumerates all registered function entries for the specified folder.
    /// </summary>
    /// <param name="folderDirectory">The directory that contains Folder.yaml.</param>
    /// <returns>The combined registry entries from all registered providers.</returns>
    public static IReadOnlyList<FunctionCatalogEntry> EnumerateEntries(string folderDirectory)
    {
        if (string.IsNullOrWhiteSpace(folderDirectory))
        {
            return [];
        }

        var combinedEntries = new Dictionary<string, FunctionCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in Providers)
        {
            foreach (var entry in provider.GetEntries(folderDirectory))
            {
                if (string.IsNullOrWhiteSpace(entry.Reference) || combinedEntries.ContainsKey(entry.Reference))
                {
                    continue;
                }

                combinedEntries[entry.Reference] = entry;
            }
        }

        return combinedEntries.Values
            .OrderBy(entry => entry.Kind)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Tries to resolve one registry entry by its stable reference.
    /// </summary>
    /// <param name="folderDirectory">The directory that contains Folder.yaml.</param>
    /// <param name="reference">The stable function reference.</param>
    /// <param name="entry">The resolved catalog entry when found.</param>
    /// <returns><see langword="true"/> when the entry exists; otherwise <see langword="false"/>.</returns>
    public static bool TryGetEntry(string folderDirectory, string? reference, out FunctionCatalogEntry? entry)
    {
        var normalizedReference = NormalizeReference(reference);
        entry = EnumerateEntries(folderDirectory)
            .FirstOrDefault(candidate => string.Equals(candidate.Reference, normalizedReference, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeReference(candidate.Reference), normalizedReference, StringComparison.OrdinalIgnoreCase));
        return entry is not null;
    }

    public static string NormalizeReference(string? reference)
    {
        var trimmed = reference?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (trimmed.StartsWith(DeclarativePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return $"{YamlPrefix}{trimmed[DeclarativePrefix.Length..].Trim()}";
        }

        return trimmed;
    }

    public static bool ReferencesEqual(string? left, string? right)
        => string.Equals(NormalizeReference(left), NormalizeReference(right), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Defines one provider boundary that can add entries to the central function registry.
/// </summary>
public interface IFunctionRegistryEntryProvider
{
    /// <summary>
    /// Gets all registry entries contributed by the provider for one folder.
    /// </summary>
    /// <param name="folderDirectory">The directory that contains Folder.yaml.</param>
    /// <returns>The contributed registry entries.</returns>
    IReadOnlyList<FunctionCatalogEntry> GetEntries(string folderDirectory);
}

/// <summary>
/// Reads declarative YAML functions from the folder-local Scripts directories.
/// </summary>
public sealed class DeclarativeFunctionRegistryProvider : IFunctionRegistryEntryProvider
{
    /// <inheritdoc />
    public IReadOnlyList<FunctionCatalogEntry> GetEntries(string folderDirectory)
    {
        var discoveredFiles = new Dictionary<string, (string FilePath, FunctionCatalogSource Source)>(StringComparer.OrdinalIgnoreCase);

        AddDirectory(directoryPath: FunctionDefinitionCodec.GetFunctionDirectory(folderDirectory), source: FunctionCatalogSource.FunctionsDirectory);
        AddDirectory(directoryPath: Path.Combine(folderDirectory, "Scripts", "Workflows"), source: FunctionCatalogSource.LegacyWorkflowDirectory);

        return discoveredFiles
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => CreateDeclarativeEntry(filePath: entry.Value.FilePath, source: entry.Value.Source))
            .ToArray();

        void AddDirectory(string directoryPath, FunctionCatalogSource source)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.yml").Concat(Directory.EnumerateFiles(directoryPath, "*.yaml")))
            {
                var referenceName = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrWhiteSpace(referenceName) || discoveredFiles.ContainsKey(referenceName))
                {
                    continue;
                }

                discoveredFiles[referenceName] = (filePath, source);
            }
        }
    }

    private static FunctionCatalogEntry CreateDeclarativeEntry(string filePath, FunctionCatalogSource source)
    {
        var referenceName = Path.GetFileNameWithoutExtension(filePath);
        var fallbackName = string.IsNullOrWhiteSpace(referenceName)
            ? Path.GetFileName(filePath)
            : referenceName;

        if (FunctionDefinitionCodec.TryLoadFromFile(filePath, out var definition, out var validation))
        {
            return new FunctionCatalogEntry
            {
                Reference = CreateDeclarativeReference(referenceName),
                Name = string.IsNullOrWhiteSpace(definition?.Name) ? fallbackName : definition.Name.Trim(),
                Kind = FunctionCatalogKind.Declarative,
                Source = source,
                SourceIdentifier = filePath,
                DisplaySource = GetDisplaySource(source),
                CanEdit = true,
                CanDelete = true,
                CanRun = true,
                IsValid = true,
                StatusText = string.Empty
            };
        }

        return new FunctionCatalogEntry
        {
            Reference = CreateDeclarativeReference(referenceName),
            Name = fallbackName,
            Kind = FunctionCatalogKind.Declarative,
            Source = source,
            SourceIdentifier = filePath,
            DisplaySource = GetDisplaySource(source),
            CanEdit = true,
            CanDelete = true,
            CanRun = false,
            IsValid = false,
            StatusText = validation.Errors.FirstOrDefault()?.Message ?? "Invalid function YAML."
        };
    }

    private static string CreateDeclarativeReference(string? referenceName)
    {
        var normalizedReferenceName = string.IsNullOrWhiteSpace(referenceName)
            ? "unnamed"
            : referenceName.Trim();
        return $"yaml:{normalizedReferenceName}";
    }

    internal static string GetDisplaySource(FunctionCatalogSource source)
    {
        return source switch
        {
            FunctionCatalogSource.FunctionsDirectory => "Scripts/Functions",
            FunctionCatalogSource.LegacyWorkflowDirectory => "Scripts/Workflows (legacy)",
            FunctionCatalogSource.PythonApplication => "Applications/Python",
            _ => source.ToString()
        };
    }
}