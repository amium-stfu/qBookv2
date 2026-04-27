namespace Amium.UiEditor.ViewModels;

public sealed class AttachItemEditorRow : ObservableObject
{
    private bool _isAttached;
    private int _intervalMs;

    private string[] ParsedParts => RelativePath.Split('|', System.StringSplitOptions.TrimEntries);

    /// <summary>
    /// The raw option/relative path string as provided by the caller.
    /// For Csv/Sql signal selection this is typically "Name|Path" or "Name|Path|Unit".
    /// For other attach lists (e.g. UDL client) this is usually just the relative path.
    /// </summary>
    public string RelativePath { get; init; } = string.Empty;

    public string DisplayLabel => string.IsNullOrWhiteSpace(Source)
        ? Name
        : $"{Name}|{Source}";

    public string Name
    {
        get
        {
            var parts = ParsedParts;
            if (parts.Length == 0)
            {
                return string.Empty;
            }

            if (parts.Length == 1)
            {
                return parts[0];
            }

            return parts[0];
        }
    }

    public string Source
    {
        get
        {
            var parts = ParsedParts;
            return parts.Length > 1 ? parts[1] : RelativePath;
        }
    }

    public string Unit
    {
        get
        {
            var parts = ParsedParts;
            return parts.Length > 2 ? parts[2] : string.Empty;
        }
    }

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