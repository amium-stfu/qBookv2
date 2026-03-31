using System;
using System.Collections.Generic;

namespace Amium.UiEditor.Helpers;

internal static class TargetPathHelper
{
    private const string BookRootPrefix = "UdlBook/";
    private static readonly string[] NonBookRootPrefixes = ["Runtime/", "Logs/", "Commands/"];

    public static string NormalizeConfiguredTargetPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Trim().Replace('\\', '/').Trim('/');
        return string.Equals(normalized, "this", StringComparison.OrdinalIgnoreCase)
            ? "this"
            : normalized;
    }

    public static string ToPersistedLayoutTargetPath(string? path, string? pageName = null)
    {
        var normalized = NormalizeConfiguredTargetPath(path);
        if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "this", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var pageRootPrefix = GetPageRootPrefix(pageName);
        if (!string.IsNullOrWhiteSpace(pageRootPrefix)
            && normalized.StartsWith(pageRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return normalized[pageRootPrefix.Length..];
        }

        return normalized;
    }

    public static IEnumerable<string> EnumerateResolutionCandidates(string? path, string? pageName = null)
    {
        var normalized = NormalizeConfiguredTargetPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (yielded.Add(normalized))
        {
            yield return normalized;
        }

        var pageRootPrefix = GetPageRootPrefix(pageName);
        if (!string.IsNullOrWhiteSpace(pageRootPrefix)
            && ShouldResolveAgainstPageRoot(normalized))
        {
            var pageScopedPath = pageRootPrefix + normalized;
            if (yielded.Add(pageScopedPath))
            {
                yield return pageScopedPath;
            }
        }

        if (ShouldPrependBookRoot(normalized))
        {
            var bookScopedPath = BookRootPrefix + normalized;
            if (yielded.Add(bookScopedPath))
            {
                yield return bookScopedPath;
            }
        }
    }

    public static string NormalizeChartSeriesDefinitions(string? definitions)
        => TransformChartSeriesDefinitions(definitions, NormalizeConfiguredTargetPath);

    public static string ToPersistedChartSeriesDefinitions(string? definitions, string? pageName = null)
        => TransformChartSeriesDefinitions(definitions, path => ToPersistedLayoutTargetPath(path, pageName));

    private static string TransformChartSeriesDefinitions(string? definitions, Func<string?, string> transformPath)
    {
        if (string.IsNullOrWhiteSpace(definitions))
        {
            return string.Empty;
        }

        var lines = definitions
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var normalizedLines = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            var targetPath = transformPath(parts[0]);
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                continue;
            }

            parts[0] = targetPath;
            normalizedLines.Add(string.Join('|', parts));
        }

        return string.Join(Environment.NewLine, normalizedLines);
    }

    private static bool ShouldPrependBookRoot(string path)
    {
        if (string.Equals(path, "this", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(BookRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var prefix in NonBookRootPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldResolveAgainstPageRoot(string path)
    {
        if (string.Equals(path, "this", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(BookRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var prefix in NonBookRootPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetPageRootPrefix(string? pageName)
    {
        var normalizedPageName = string.IsNullOrWhiteSpace(pageName)
            ? string.Empty
            : pageName.Trim().Replace('\\', '/').Trim('/');

        return string.IsNullOrWhiteSpace(normalizedPageName)
            ? string.Empty
            : $"{BookRootPrefix}{normalizedPageName}/";
    }
}