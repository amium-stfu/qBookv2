namespace UiEditor.ViewModels;

public sealed class HostMessageEntry
{
    public required string Source { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public string Location { get; init; } = string.Empty;

    public string Summary => string.IsNullOrWhiteSpace(Location)
        ? $"{Source} | {Severity}"
        : $"{Source} | {Severity} | {Location}";
}
