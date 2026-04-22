using System;
using System.Collections.Generic;
using System.Linq;

namespace Amium.Host;

internal static class EnhancedSignalPathHelper
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

        var segments = normalized.Split(HierarchySeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 0 ? string.Empty : string.Join('.', segments);
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

        return normalized.Split(HierarchySeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static string NormalizeComparablePath(string? path)
    {
        var segments = SplitPathSegments(path);
        return segments.Count == 0 ? string.Empty : string.Join('.', segments);
    }

    public static bool PathsEqual(string? left, string? right)
        => string.Equals(NormalizeComparablePath(left), NormalizeComparablePath(right), StringComparison.OrdinalIgnoreCase);

    public static bool IsDescendantPath(string? path, string? prefix)
    {
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

        return true;
    }

    public static IEnumerable<string> EnumerateResolutionCandidates(string? path, string? folderName = null)
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
            foreach (var prefix in GetFolderRootPrefixes(folderName))
            {
                foreach (var candidate in ExpandCandidateForms(JoinPath(prefix, normalized)))
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

    private static IEnumerable<string> GetFolderRootPrefixes(string? folderName)
    {
        var normalizedFolder = NormalizeConfiguredTargetPath(folderName);
        if (string.IsNullOrWhiteSpace(normalizedFolder))
        {
            yield break;
        }

        yield return normalizedFolder;
        yield return JoinPath("Project", normalizedFolder);
    }

    private static IEnumerable<string> ExpandCandidateForms(string path)
    {
        var normalized = NormalizeConfiguredTargetPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        yield return normalized;
        yield return normalized.Replace('/', '.');
        yield return normalized.Replace('.', '/');
    }

    private static bool ShouldPrependProjectRoot(string path)
    {
        var segments = SplitPathSegments(path);
        if (segments.Count == 0 || string.Equals(path, "this", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rootSegment = segments[0];
        if (ProjectRootSegments.Contains(rootSegment, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return !NonProjectRootSegments.Contains(rootSegment, StringComparer.OrdinalIgnoreCase);
    }

    private static bool ShouldResolveAgainstFolderRoot(string path)
        => ShouldPrependProjectRoot(path);

    private static string JoinPath(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return NormalizeConfiguredTargetPath(right);
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return NormalizeConfiguredTargetPath(left);
        }

        return $"{NormalizeConfiguredTargetPath(left)}.{NormalizeConfiguredTargetPath(right)}";
    }
}
