using System;
using Amium.Logging;
using Amium.Items;

namespace Amium.Host;

public static class UiPublisher
{
    public static Item Publish(Item item, bool pruneMissingMembers = false)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Path);
        return HostRegistries.Data.UpsertSnapshot(item.Path!, item, pruneMissingMembers);
    }

    public static Item Publish(string path, ProcessLog log, string? title = null, bool pruneMissingMembers = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(log);

        var normalizedPath = path.Replace('\\', '/').Trim('/');
        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');
        var itemName = lastSeparatorIndex >= 0 ? normalizedPath[(lastSeparatorIndex + 1)..] : normalizedPath;
        var parentPath = lastSeparatorIndex >= 0 ? normalizedPath[..lastSeparatorIndex] : null;
        var displayTitle = string.IsNullOrWhiteSpace(title) ? itemName : title.Trim();

        var item = parentPath is null
            ? new Item(itemName, log)
            : new Item(itemName, log, parentPath);

        item.Params["Kind"].Value = "ProcessLog";
        item.Params["Title"].Value = displayTitle;
        item.Params["Text"].Value = displayTitle;
        return Publish(item, pruneMissingMembers);
    }

    public static void Publish(HostCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        HostRegistries.Commands.Register(command);
    }

    public static void Publish(ICameraFrameSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        HostRegistries.Cameras.Register(source);
    }
}

