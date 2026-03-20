using System.Collections.Generic;
using UiEditor.Models;

namespace UiEditor.Persistence;

public sealed class LayoutDocument
{
    public List<PageDocument> Pages { get; init; } = [];
}

public sealed class PageDocument
{
    public int Index { get; init; }

    public string Name { get; init; } = string.Empty;

    public List<PageItemDocument> Items { get; init; } = [];
}

public sealed class PageItemDocument
{
    public ControlKind Kind { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Id { get; init; } = string.Empty;

    public string Header { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Footer { get; init; } = string.Empty;

    public string? BackgroundColor { get; init; }

    public string? BorderColor { get; init; }

    public string? ContainerBorder { get; init; }

    public string? ContainerBackgroundColor { get; init; }

    public double ContainerBorderWidth { get; init; } = 0;

    public double BorderWidth { get; init; } = 1;

    public double CornerRadius { get; init; } = 12;

    public string? PrimaryForegroundColor { get; init; }

    public string? SecondaryForegroundColor { get; init; }

    public string? AccentBackgroundColor { get; init; }

    public string? AccentForegroundColor { get; init; }

    public string TargetPath { get; init; } = string.Empty;

    public string TargetParameterPath { get; init; } = string.Empty;

    public string TargetParameterFormat { get; init; } = string.Empty;

    public string TargetLog { get; init; } = "Logs/Host";

    public int RefreshRateMs { get; init; } = 1000;

    public int HistorySeconds { get; init; } = 120;

    public int ViewSeconds { get; init; } = 30;

    public string ChartSeriesDefinitions { get; init; } = string.Empty;

    public bool IsReadOnly { get; init; }

    public bool IsAutoHeight { get; init; } = true;

    public double ListItemHeight { get; init; } = 72;

    public double ControlHeight { get; init; } = 72;

    public double ControlBorderWidth { get; init; } = 0;

    public double ControlCornerRadius { get; init; } = 0;

    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public List<PageItemDocument> Items { get; init; } = [];
}


