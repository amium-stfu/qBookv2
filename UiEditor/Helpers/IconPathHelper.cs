using System;
using System.IO;

namespace Amium.UiEditor.Helpers;

public static class IconPathHelper
{
    private const string AssetsDirectoryName = "Assets";
    private const string IconsDirectoryName = "Icons";

    public static string NormalizeStoredPath(string? iconPath, string? layoutFilePath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return string.Empty;
        }

        var trimmed = iconPath.Trim();
        if (IsAssetUri(trimmed))
        {
            return trimmed;
        }

        if (!Path.IsPathRooted(trimmed))
        {
            return NormalizeRelativePath(trimmed);
        }

        var layoutDirectory = GetLayoutDirectory(layoutFilePath);
        if (string.IsNullOrWhiteSpace(layoutDirectory))
        {
            return Path.GetFullPath(trimmed);
        }

        var absolutePath = Path.GetFullPath(trimmed);
        var folderIconDirectory = GetFolderIconDirectory(layoutFilePath);
        if (!string.IsNullOrWhiteSpace(folderIconDirectory) && IsPathWithinDirectory(absolutePath, folderIconDirectory))
        {
            return NormalizeRelativePath(Path.GetRelativePath(layoutDirectory, absolutePath));
        }

        return absolutePath;
    }

    public static string? ResolvePath(string? iconPath, string? layoutFilePath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return null;
        }

        var trimmed = iconPath.Trim();
        if (IsAssetUri(trimmed))
        {
            return trimmed;
        }

        if (Path.IsPathRooted(trimmed))
        {
            return Path.GetFullPath(trimmed);
        }

        var layoutDirectory = GetLayoutDirectory(layoutFilePath);
        if (string.IsNullOrWhiteSpace(layoutDirectory))
        {
            return NormalizeRelativePath(trimmed);
        }

        return Path.GetFullPath(Path.Combine(layoutDirectory, NormalizeRelativePath(trimmed)));
    }

    public static string? GetFolderIconDirectory(string? layoutFilePath)
    {
        var layoutDirectory = GetLayoutDirectory(layoutFilePath);
        return string.IsNullOrWhiteSpace(layoutDirectory)
            ? null
            : Path.Combine(layoutDirectory, AssetsDirectoryName, IconsDirectoryName);
    }

    private static string? GetLayoutDirectory(string? layoutFilePath)
    {
        if (string.IsNullOrWhiteSpace(layoutFilePath))
        {
            return null;
        }

        return Path.GetDirectoryName(Path.GetFullPath(layoutFilePath));
    }

    private static bool IsPathWithinDirectory(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, fullDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAssetUri(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && !uri.IsFile;

    private static string NormalizeRelativePath(string value)
        => value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
}