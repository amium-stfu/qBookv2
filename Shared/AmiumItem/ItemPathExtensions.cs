using System;

namespace UiEditor.Items;

public static class ItemPathExtensions
{
    public static Item Repath(this Item item, string absolutePath)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        ApplyPath(item, NormalizePath(absolutePath));
        return item;
    }

    private static void ApplyPath(Item item, string absolutePath)
    {
        item._path = absolutePath;
        item.Params["Name"].Value = GetLastSegment(absolutePath);
        item.Params["Path"].Value = absolutePath;

        foreach (var parameterEntry in item.Params.Dictionary)
        {
            parameterEntry.Value.Path = $"{absolutePath}/{parameterEntry.Key}";
        }

        foreach (var childEntry in item.Dictionary)
        {
            var child = childEntry.Value;
            var childName = child.Name ?? childEntry.Key;
            ApplyPath(child, $"{absolutePath}/{childName}");
        }
    }

    private static string NormalizePath(string value)
        => value.Replace('\\', '/').Trim('/');

    private static string GetLastSegment(string path)
    {
        var lastSeparatorIndex = path.LastIndexOf('/');
        return lastSeparatorIndex >= 0 ? path[(lastSeparatorIndex + 1)..] : path;
    }
}
