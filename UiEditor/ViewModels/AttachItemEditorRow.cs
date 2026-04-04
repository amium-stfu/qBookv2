namespace Amium.UiEditor.ViewModels;

public sealed class AttachItemEditorRow : ObservableObject
{
    private bool _isAttached;
    private int _intervalMs;

    /// <summary>
    /// The raw option/relative path string as provided by the caller.
    /// For Csv/Sql signal selection this is typically "Name|Path" or "Name|Path|Unit".
    /// For other attach lists (e.g. UDL client) this is usually just the relative path.
    /// </summary>
    public string RelativePath { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable label shown in the attach dialog. For now this mirrors RelativePath
    /// so existing behavior remains unchanged.
    /// </summary>
    public string DisplayLabel => RelativePath;

    public bool IsAttached
    {
        get => _isAttached;
        set => SetProperty(ref _isAttached, value);
    }

    /// <summary>
    /// Optional per-item interval in milliseconds. Used by SqlLogger when configured
    /// via the CsvSignalPaths attach-list field. Ignored for other attach-list usages.
    /// </summary>
    public int IntervalMs
    {
        get => _intervalMs;
        set => SetProperty(ref _intervalMs, value);
    }
}