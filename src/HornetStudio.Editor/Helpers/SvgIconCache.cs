using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Platform;

namespace HornetStudio.Editor.Helpers;

internal static partial class SvgIconCache
{
    private static readonly ConcurrentDictionary<string, string> CachedPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string CacheDirectory = Path.Combine(Path.GetTempPath(), "HornetStudio.Editor", "SvgTintCache");
    private static readonly string CurrentAssemblyName = typeof(SvgIconCache).Assembly.GetName().Name ?? "HornetStudio.Editor";
    private static readonly string[] LegacyIconAssemblyNames = ["HornetStudio.Editor", "HornetStudio.Editor", "HornetStudio.EditorUi"];

    public static string? ResolvePath(string? iconPath, string? tintColor)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return null;
        }

        var normalizedIconPath = NormalizeIconPath(iconPath.Trim());
        if (!IconExists(normalizedIconPath))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(tintColor))
        {
            return normalizedIconPath;
        }

        var normalizedTint = tintColor.Trim();
        var cacheKey = $"{normalizedIconPath}|{normalizedTint}";
        return CachedPaths.GetOrAdd(cacheKey, _ => CreateTintedIcon(normalizedIconPath, normalizedTint));
    }

    private static string CreateTintedIcon(string iconPath, string tintColor)
    {
        Directory.CreateDirectory(CacheDirectory);

        var svgContent = ReadSvgContent(iconPath);
        var tintedSvg = ApplyTint(svgContent, tintColor);
        var fileName = $"{CreateSafeName(iconPath)}-{CreateHash(iconPath, tintColor)}.svg";
        var filePath = Path.Combine(CacheDirectory, fileName);
        File.WriteAllText(filePath, tintedSvg, new UTF8Encoding(false));
        return filePath;
    }

    private static string ReadSvgContent(string iconPath)
    {
        if (Uri.TryCreate(iconPath, UriKind.Absolute, out var uri) && uri.Scheme.Equals("avares", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = AssetLoader.Open(uri);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            return reader.ReadToEnd();
        }

        return File.ReadAllText(iconPath, Encoding.UTF8);
    }

    private static bool IconExists(string iconPath)
    {
        if (Uri.TryCreate(iconPath, UriKind.Absolute, out var uri) && uri.Scheme.Equals("avares", StringComparison.OrdinalIgnoreCase))
        {
            return AssetLoader.Exists(uri);
        }

        return File.Exists(iconPath);
    }

    private static string NormalizeIconPath(string iconPath)
    {
        if (!Uri.TryCreate(iconPath, UriKind.Absolute, out var uri)
            || !uri.Scheme.Equals("avares", StringComparison.OrdinalIgnoreCase))
        {
            return iconPath;
        }

        var original = uri.OriginalString;
        foreach (var legacyAssemblyName in LegacyIconAssemblyNames)
        {
            var legacyPrefix = $"avares://{legacyAssemblyName}/";
            if (original.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return $"avares://{CurrentAssemblyName}/{original[legacyPrefix.Length..]}";
            }
        }

        return original;
    }

    private static string ApplyTint(string svgContent, string tintColor)
    {
        var tintedSvg = FillAttributeRegex().Replace(svgContent, match => IsNone(match.Groups[1].Value) ? match.Value : $"fill=\"{tintColor}\"");
        tintedSvg = FillStyleRegex().Replace(tintedSvg, match => IsNone(match.Groups[1].Value) ? match.Value : $"fill:{tintColor}");
        tintedSvg = StrokeAttributeRegex().Replace(tintedSvg, match => IsNone(match.Groups[1].Value) ? match.Value : $"stroke=\"{tintColor}\"");
        tintedSvg = StrokeStyleRegex().Replace(tintedSvg, match => IsNone(match.Groups[1].Value) ? match.Value : $"stroke:{tintColor}");

        return EnsureRootFill(tintedSvg, tintColor);
    }

    private static string EnsureRootFill(string svgContent, string tintColor)
    {
        var match = SvgTagRegex().Match(svgContent);
        if (!match.Success)
        {
            return svgContent;
        }

        var svgTag = match.Value;
        if (FillAttributeRegex().IsMatch(svgTag) || FillStyleRegex().IsMatch(svgTag))
        {
            return svgContent;
        }

        var insertIndex = svgTag.LastIndexOf('>');
        if (insertIndex < 0)
        {
            return svgContent;
        }

        var updatedTag = svgTag.Insert(insertIndex, $" fill=\"{tintColor}\"");
        return string.Concat(svgContent.AsSpan(0, match.Index), updatedTag, svgContent.AsSpan(match.Index + match.Length));
    }

    private static bool IsNone(string value)
        => value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase);

    private static string CreateSafeName(string iconPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(iconPath);
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return string.IsNullOrWhiteSpace(fileName) ? "icon" : fileName;
    }

    private static string CreateHash(string iconPath, string tintColor)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{iconPath}|{tintColor}"));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    [GeneratedRegex("fill=\"(.*?)\"", RegexOptions.IgnoreCase)]
    private static partial Regex FillAttributeRegex();

    [GeneratedRegex("fill\\s*:\\s*([^;\"']+)", RegexOptions.IgnoreCase)]
    private static partial Regex FillStyleRegex();

    [GeneratedRegex("stroke=\"(.*?)\"", RegexOptions.IgnoreCase)]
    private static partial Regex StrokeAttributeRegex();

    [GeneratedRegex("stroke\\s*:\\s*([^;\"']+)", RegexOptions.IgnoreCase)]
    private static partial Regex StrokeStyleRegex();

    private static Regex SvgTagRegex()
        => new("<svg\\b[^>]*>", RegexOptions.IgnoreCase);
}
