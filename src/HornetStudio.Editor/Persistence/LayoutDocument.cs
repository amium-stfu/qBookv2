using System.Collections.Generic;
using Amium.UiEditor.Models;

namespace Amium.UiEditor.Persistence;

public sealed class LayoutDocument
{
    public string TabStripPlacement { get; init; } = "Right";

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

    public string ControlCaption { get; init; } = string.Empty;

    public bool CaptionVisible { get; init; } = true;

    public bool ShowCaption { get; init; } = true;

    public string BodyCaption { get; init; } = string.Empty;

    public string BodyCaptionPosition { get; init; } = "Top";

    public bool BodyCaptionVisible { get; init; } = true;

    public bool ShowBodyCaption { get; init; } = true;

    public bool ShowFooter { get; init; } = true;

    public string Header { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Footer { get; init; } = string.Empty;

    public string? HeaderForeColor { get; init; }

    public string? HeaderBackColor { get; init; }

    public string? HeaderBorderColor { get; init; }

    public double HeaderBorderWidth { get; init; }

    public double HeaderCornerRadius { get; init; } = 6;

    public string? BodyForeColor { get; init; }

    public string? BodyBackColor { get; init; }

    public string? BodyBorderColor { get; init; }

    public double BodyBorderWidth { get; init; }

    public double BodyCornerRadius { get; init; }

    public string? FooterForeColor { get; init; }

    public string? FooterBackColor { get; init; }

    public string? FooterBorderColor { get; init; }

    public double FooterBorderWidth { get; init; }

    public double FooterCornerRadius { get; init; } = 6;

    public string ToolTipText { get; init; } = string.Empty;

    public string ButtonText { get; init; } = string.Empty;

    public string ButtonIcon { get; init; } = string.Empty;

    public bool ButtonOnlyIcon { get; init; }

    public string ButtonIconAlign { get; init; } = "Left";

    public string ButtonTextAlign { get; init; } = "Center";

    public string ButtonCommand { get; init; } = string.Empty;

    public string ButtonBodyBackground { get; init; } = "";

    public string ButtonBodyForegroundColor { get; init; } = string.Empty;

    public string ButtonIconColor { get; init; } = string.Empty;

    public bool UseThemeColor { get; init; } = true;

    public bool Enabled { get; init; } = true;

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

    public string Unit { get; init; } = string.Empty;

    public string TargetLog { get; init; } = "Logs/Host";

    public int RefreshRateMs { get; init; } = 1000;

    public int HistorySeconds { get; init; } = 120;

    public int ViewSeconds { get; init; } = 30;

    public string ChartSeriesDefinitions { get; init; } = string.Empty;

    public List<ItemInteractionRuleDocument> InteractionRules { get; init; } = [];

    public string UdlClientHost { get; init; } = "192.168.178.151";

    public int UdlClientPort { get; init; } = 9001;

    public bool UdlClientAutoConnect { get; init; }

    public bool UdlClientDebugLogging { get; init; }

    public string UdlAttachedItemPaths { get; init; } = string.Empty;

    public bool IsReadOnly { get; init; }

    public bool IsAutoHeight { get; init; } = true;

    public double ListItemHeight { get; init; } = 72;

    public double ControlHeight { get; init; } = 72;

    public double ControlBorderWidth { get; init; } = 0;

    public string? ControlBorderColor { get; init; }

    public double ControlCornerRadius { get; init; } = 0;

    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public List<PageItemDocument> Items { get; init; } = [];
}

public sealed class ItemInteractionRuleDocument
{
    public ItemInteractionEvent Event { get; init; } = ItemInteractionEvent.BodyLeftClick;

    public ItemInteractionAction Action { get; init; } = ItemInteractionAction.OpenValueEditor;

    public string TargetPath { get; init; } = "this";

    public string Argument { get; init; } = string.Empty;
}


