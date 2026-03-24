namespace UdlBook.ViewModels;

public sealed record ThemePalette(
    string WindowBackground,
    string CardBackground,
    string CardBorderBrush,
    string PrimaryTextBrush,
    string SecondaryTextBrush,
    string CanvasBackground,
    string CanvasBorderBrush,
    string TabSelectNumerBackColor,
    string TabSelectBackColor,
    string TabSelectForeColor,
    string TabNumerBackColor,
    string TabBackColor,
    string TabForeColor,
    string HeaderBadgeBackground,
    string HeaderBadgeForeground)
{
    public static ThemePalette Light { get; } = new(
        WindowBackground: "#F4F5F7",
        CardBackground: "#E7E7E7",
        CardBorderBrush: "#D5D9E0",
        PrimaryTextBrush: "#111827",
        SecondaryTextBrush: "#5E6777",
        CanvasBackground: "#FCFCFD",
        CanvasBorderBrush: "#C9CED8",
        TabSelectNumerBackColor: "#F59E0B",
        TabSelectBackColor: "#FFF1C4",
        TabSelectForeColor: "#000000",
        TabNumerBackColor: "#AAAAAA",
        TabBackColor: "#E7E7E7",
        TabForeColor: "#111827",
        HeaderBadgeBackground: "#111827",
        HeaderBadgeForeground: "#FFFFFF");

    public static ThemePalette Dark { get; } = new(
        WindowBackground: "#0B0B0C",
        CardBackground: "#111827",
        CardBorderBrush: "#374151",
        PrimaryTextBrush: "#F9FAFB",
        SecondaryTextBrush: "#9CA3AF",
        CanvasBackground: "#000000",
        CanvasBorderBrush: "#4B5563",
        TabSelectNumerBackColor: "#F59E0B",
        TabSelectBackColor: "#F3E6B3",
        TabSelectForeColor: "#000000",
        TabNumerBackColor: "#4B5563",
        TabBackColor: "#111827",
        TabForeColor: "#F3F4F6",
        HeaderBadgeBackground: "#F9FAFB",
        HeaderBadgeForeground: "#111827");
}