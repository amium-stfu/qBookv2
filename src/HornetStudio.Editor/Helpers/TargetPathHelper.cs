using System;
using System.Collections.Generic;

namespace HornetStudio.Editor.Helpers;

internal static class TargetPathHelper
{
    private static readonly string[] ProjectRootSegments = ["Project", "UdlProject", "UdlBook"];
    private static readonly string[] NonProjectRootSegments = ["Runtime", "Logs", "Commands"];
    private static readonly char[] HierarchySeparators = ['.', '/'];

    public static string NormalizeConfiguredTargetPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Trim().Replace('\\', '/').Trim('/', '.');
        if (string.Equals(normalized, "this", StringComparison.OrdinalIgnoreCase))
        {
            return "this";
        }

        var segments = normalized
            .Split(HierarchySeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments.Length == 0
            ? string.Empty
            : string.Join('.', segments);
    }

    public static string NormalizeComparablePath(string? path)
    {
        var segments = SplitPathSegments(path);
        return segments.Count == 0
            ? string.Empty
            : string.Join('.', segments);
    }

    public static IReadOnlyList<string> SplitPathSegments(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        var normalized = NormalizeConfiguredTargetPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        return normalized
            .Split(HierarchySeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static bool PathsEqual(string? left, string? right)
        => string.Equals(NormalizeComparablePath(left), NormalizeComparablePath(right), StringComparison.OrdinalIgnoreCase);

    public static bool IsDescendantPath(string? path, string? prefix)
        => TryGetRelativePath(path, prefix, out _);

    public static bool TryGetRelativePath(string? path, string? prefix, out string relativePath)
    {
        relativePath = string.Empty;

        var pathSegments = SplitPathSegments(path);
        var prefixSegments = SplitPathSegments(prefix);
        if (pathSegments.Count == 0 || prefixSegments.Count == 0 || pathSegments.Count <= prefixSegments.Count)
        {
            return false;
        }

        for (var index = 0; index < prefixSegments.Count; index++)
        {
            if (!string.Equals(pathSegments[index], prefixSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        relativePath = string.Join('.', pathSegments.Skip(prefixSegments.Count));
        return !string.IsNullOrWhiteSpace(relativePath);
    }

    public static string ToPersistedLayoutTargetPath(string? path, string? pageName = null)
    {
        var normalized = NormalizeConfiguredTargetPath(path);
        if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "this", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var folderRelativePath = RemoveFolderContextPrefix(normalized, pageName);
        if (!string.IsNullOrWhiteSpace(folderRelativePath))
        {
            return folderRelativePath;
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

        foreach (var candidate in ExpandCandidateForms(normalized))
        {
            if (yielded.Add(candidate))
            {
                yield return candidate;
            }
        }

        if (ShouldResolveAgainstFolderRoot(normalized))
        {
            foreach (var folderRootPrefix in GetFolderRootPrefixes(pageName))
            {
                foreach (var candidate in ExpandCandidateForms(JoinPath(folderRootPrefix, normalized)))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
        }

        if (ShouldPrependProjectRoot(normalized))
        {
            foreach (var projectRootPrefix in ProjectRootSegments)
            {
                foreach (var candidate in ExpandCandidateForms(JoinPath(projectRootPrefix, normalized)))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
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

    private static bool ShouldPrependProjectRoot(string path)
    {
        var segments = SplitPathSegments(path);
        if (segments.Count == 0
            || string.Equals(path, "this", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rootSegment = segments[0];
        if (ProjectRootSegments.Contains(rootSegment, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var prefix in NonProjectRootSegments)
        {
            if (string.Equals(rootSegment, prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldResolveAgainstFolderRoot(string path)
    {
        var segments = SplitPathSegments(path);
        if (segments.Count == 0
            || string.Equals(path, "this", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rootSegment = segments[0];
        if (ProjectRootSegments.Contains(rootSegment, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var prefix in NonProjectRootSegments)
        {
            if (string.Equals(rootSegment, prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> GetFolderRootPrefixes(string? pageName)
    {
        var normalizedPageName = NormalizeConfiguredTargetPath(pageName);

        if (string.IsNullOrWhiteSpace(normalizedPageName))
        {
            yield break;
        }

        yield return normalizedPageName;

        foreach (var projectRootPrefix in ProjectRootSegments)
        {
            yield return JoinPath(projectRootPrefix, normalizedPageName);
        }
    }

    private static string RemoveFolderContextPrefix(string path, string? pageName)
    {
        var normalizedPageName = NormalizeConfiguredTargetPath(pageName);
        if (string.IsNullOrWhiteSpace(normalizedPageName))
        {
            return string.Empty;
        }

        var segments = SplitPathSegments(path);
        for (var index = 0; index < segments.Count - 1; index++)
        {
            if (string.Equals(segments[index], normalizedPageName, StringComparison.OrdinalIgnoreCase))
            {
                return string.Join('.', segments.Skip(index + 1));
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> ExpandCandidateForms(string path)
    {
        var configured = NormalizeConfiguredTargetPath(path);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            yield return configured;
        }
    }

    private static string JoinPath(string prefix, string path)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return NormalizeConfiguredTargetPath(path);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return NormalizeConfiguredTargetPath(prefix);
        }

        return $"{NormalizeConfiguredTargetPath(prefix)}.{NormalizeConfiguredTargetPath(path)}";
    }
}