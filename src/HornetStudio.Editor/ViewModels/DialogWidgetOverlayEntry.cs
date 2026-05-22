using HornetStudio.Editor.Models;

namespace HornetStudio.Editor.ViewModels;

/// <summary>
/// Describes an opened dialog widget overlay.
/// </summary>
public sealed class DialogWidgetOverlayEntry
{
    /// <summary>
    /// Gets the stable id of the dialog widget that owns the overlay.
    /// </summary>
    public required string DialogWidgetId { get; init; }

    /// <summary>
    /// Gets the display name shown in the overlay header.
    /// </summary>
    public required string DialogName { get; init; }

    /// <summary>
    /// Gets the configured placement origin.
    /// </summary>
    public required string Origin { get; init; }

    /// <summary>
    /// Gets the configured placement position.
    /// </summary>
    public required string Position { get; init; }

    /// <summary>
    /// Gets the cloned folder used to render the overlay content.
    /// </summary>
    public required FolderModel Folder { get; init; }

    /// <summary>
    /// Gets the cloned dialog widget item used to render the overlay content.
    /// </summary>
    public required FolderItemModel DialogItem { get; init; }

    /// <summary>
    /// Gets the dialog content width from the dialog widget definition.
    /// </summary>
    public required double ContentWidth { get; init; }

    /// <summary>
    /// Gets the dialog content height from the dialog widget definition.
    /// </summary>
    public required double ContentHeight { get; init; }

    /// <summary>
    /// Gets the overlay title.
    /// </summary>
    public string Title => string.IsNullOrWhiteSpace(DialogName)
        ? "Dialog"
        : DialogName;
}
