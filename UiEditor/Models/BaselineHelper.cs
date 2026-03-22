using System.Collections.Concurrent;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Amium.UiEditor.Models;

public static class BaselineHelper
{
    private readonly record struct TextMetrics(double Baseline, double Height)
    {
        public double Descent => Height - Baseline;
    }

    private static readonly ConcurrentDictionary<string, TextMetrics> MetricsCache = new();

    public static double GetBaselineFromLayout(string sampleText, Typeface typeface, double fontSize)
        => GetTextMetrics(sampleText, typeface, fontSize).Baseline;

    public static double GetDescentFromLayout(string sampleText, Typeface typeface, double fontSize)
        => GetTextMetrics(sampleText, typeface, fontSize).Descent;

    public static double GetTextHeightFromLayout(string sampleText, Typeface typeface, double fontSize)
        => GetTextMetrics(sampleText, typeface, fontSize).Height;

    private static TextMetrics GetTextMetrics(string sampleText, Typeface typeface, double fontSize)
    {
        var text = string.IsNullOrWhiteSpace(sampleText) ? "Mg" : sampleText;
        var cacheKey = $"{typeface.FontFamily}|{typeface.Style}|{typeface.Weight}|{typeface.Stretch}|{fontSize:0.####}|{text}";

        return MetricsCache.GetOrAdd(cacheKey, _ =>
        {
            var layout = new TextLayout(
                text,
                typeface,
                fontSize,
                foreground: Brushes.Transparent,
                maxWidth: double.PositiveInfinity);

            if (layout.TextLines.Count == 0)
            {
                return new TextMetrics(0, 0);
            }

            var line = layout.TextLines[0];
            return new TextMetrics(line.Baseline, line.Height);
        });
    }

    public static double GetBaselineOffsetBRelativeToA(
        Typeface typefaceA, double fontSizeA, string sampleA,
        Typeface typefaceB, double fontSizeB, string sampleB)
    {
        var descentA = GetDescentFromLayout(sampleA, typefaceA, fontSizeA);
        var descentB = GetDescentFromLayout(sampleB, typefaceB, fontSizeB);
        return descentB - descentA;
    }
}