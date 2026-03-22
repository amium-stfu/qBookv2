using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Controls;

public sealed class BitChoiceClickedEventArgs : EventArgs
{
    public BitChoiceClickedEventArgs(int bitIndex)
    {
        BitIndex = bitIndex;
    }

    public int BitIndex { get; }
}

public sealed class BoolChoiceClickedEventArgs : EventArgs
{
    public BoolChoiceClickedEventArgs(bool value)
    {
        Value = value;
    }

    public bool Value { get; }
}

public partial class ParameterControl : UserControl
{
    private static readonly Typeface ValueTypeface = new(new FontFamily("Calibri"), FontStyle.Normal, FontWeight.Bold);
    private static readonly Typeface UnitTypeface = new(new FontFamily("Calibri"), FontStyle.Italic, FontWeight.Normal);

    public static readonly StyledProperty<ParameterDisplayModel?> PresentationProperty =
        AvaloniaProperty.Register<ParameterControl, ParameterDisplayModel?>(nameof(Presentation), ParameterDisplayModel.Empty);

    public static readonly StyledProperty<double> ValueFontSizeProperty =
        AvaloniaProperty.Register<ParameterControl, double>(nameof(ValueFontSize), 18);

    public static readonly StyledProperty<double> UnitFontSizeProperty =
        AvaloniaProperty.Register<ParameterControl, double>(nameof(UnitFontSize), 12);

    public static readonly StyledProperty<double> UnitWidthProperty =
        AvaloniaProperty.Register<ParameterControl, double>(nameof(UnitWidth), 0);

    public static readonly StyledProperty<double> UnitBaselineOffsetProperty =
        AvaloniaProperty.Register<ParameterControl, double>(nameof(UnitBaselineOffset), 0);

    public static readonly StyledProperty<int> BitColumnsProperty =
        AvaloniaProperty.Register<ParameterControl, int>(nameof(BitColumns), 4);

    public static readonly StyledProperty<double> ChoiceFontSizeProperty =
        AvaloniaProperty.Register<ParameterControl, double>(nameof(ChoiceFontSize), 12);

    public static readonly StyledProperty<double> ChoiceHeightProperty =
        AvaloniaProperty.Register<ParameterControl, double>(nameof(ChoiceHeight), 24);

    public static readonly StyledProperty<double> ResolvedValueFontSizeProperty =
        AvaloniaProperty.Register<ParameterControl, double>(nameof(ResolvedValueFontSize), 18);

    public static readonly StyledProperty<double> ResolvedUnitFontSizeProperty =
        AvaloniaProperty.Register<ParameterControl, double>(nameof(ResolvedUnitFontSize), 12);

    public static readonly StyledProperty<double> ResolvedUnitBaselineOffsetProperty =
        AvaloniaProperty.Register<ParameterControl, double>(nameof(ResolvedUnitBaselineOffset), 0);

    public static readonly StyledProperty<double> ValueVerticalOffsetProperty =
        AvaloniaProperty.Register<ParameterControl, double>(nameof(ValueVerticalOffset), 0);

    public ParameterControl()
    {
        InitializeComponent();
    }

    public event EventHandler<BitChoiceClickedEventArgs>? BitChoiceClicked;
    public event EventHandler<BoolChoiceClickedEventArgs>? BoolChoiceClicked;

    public ParameterDisplayModel? Presentation
    {
        get => GetValue(PresentationProperty) ?? ParameterDisplayModel.Empty;
        set => SetValue(PresentationProperty, value ?? ParameterDisplayModel.Empty);
    }

    public double ValueFontSize
    {
        get => GetValue(ValueFontSizeProperty);
        set => SetValue(ValueFontSizeProperty, value);
    }

    public double UnitFontSize
    {
        get => GetValue(UnitFontSizeProperty);
        set => SetValue(UnitFontSizeProperty, value);
    }

    public double UnitWidth
    {
        get => GetValue(UnitWidthProperty);
        set => SetValue(UnitWidthProperty, value);
    }

    public double UnitBaselineOffset
    {
        get => GetValue(UnitBaselineOffsetProperty);
        set => SetValue(UnitBaselineOffsetProperty, value);
    }

    public int BitColumns
    {
        get => GetValue(BitColumnsProperty);
        set => SetValue(BitColumnsProperty, value);
    }

    public double ChoiceFontSize
    {
        get => GetValue(ChoiceFontSizeProperty);
        set => SetValue(ChoiceFontSizeProperty, value);
    }

    public double ChoiceHeight
    {
        get => GetValue(ChoiceHeightProperty);
        set => SetValue(ChoiceHeightProperty, value);
    }

    public double ResolvedValueFontSize
    {
        get => GetValue(ResolvedValueFontSizeProperty);
        set => SetValue(ResolvedValueFontSizeProperty, value);
    }

    public double ResolvedUnitFontSize
    {
        get => GetValue(ResolvedUnitFontSizeProperty);
        set => SetValue(ResolvedUnitFontSizeProperty, value);
    }

    public double ResolvedUnitBaselineOffset
    {
        get => GetValue(ResolvedUnitBaselineOffsetProperty);
        set => SetValue(ResolvedUnitBaselineOffsetProperty, value);
    }

    public double ValueVerticalOffset
    {
        get => GetValue(ValueVerticalOffsetProperty);
        set => SetValue(ValueVerticalOffsetProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PresentationProperty)
        {
            BitColumns = ResolveBitColumns(Presentation?.Definition.BitCount ?? 0);
        }

        if (change.Property == PresentationProperty
            || change.Property == ValueFontSizeProperty
            || change.Property == UnitFontSizeProperty
            || change.Property == UnitBaselineOffsetProperty
            || change.Property == BoundsProperty)
        {
            RecalculateTextMetrics();
        }
    }

    private void RecalculateTextMetrics()
    {
        var presentation = Presentation;
        if (presentation is null || !presentation.IsText)
        {
            ResolvedValueFontSize = ValueFontSize;
            ResolvedUnitFontSize = UnitFontSize;
            ResolvedUnitBaselineOffset = UnitBaselineOffset;
            ValueVerticalOffset = 0;
            return;
        }

        var availableHeight = Bounds.Height;
        if (availableHeight <= 0)
        {
            ResolvedValueFontSize = ValueFontSize;
            ResolvedUnitFontSize = UnitFontSize;
            ResolvedUnitBaselineOffset = UnitBaselineOffset;
            ValueVerticalOffset = 0;
            return;
        }

        var valueMetricsHeight = BaselineHelper.GetTextHeightFromLayout("Aq", ValueTypeface, ValueFontSize);
        if (valueMetricsHeight <= 0)
        {
            ResolvedValueFontSize = ValueFontSize;
            ResolvedUnitFontSize = UnitFontSize;
            ResolvedUnitBaselineOffset = UnitBaselineOffset;
            ValueVerticalOffset = 0;
            return;
        }

        var fitScale = System.Math.Min(1.0, (availableHeight - 2) / valueMetricsHeight);
        var resolvedValueSize = System.Math.Max(8, ValueFontSize * fitScale);
        var unitScale = ValueFontSize <= 0 ? 1.0 : resolvedValueSize / ValueFontSize;
        var resolvedUnitSize = System.Math.Max(6, UnitFontSize * unitScale);

        ResolvedValueFontSize = resolvedValueSize;
        ResolvedUnitFontSize = resolvedUnitSize;
        ResolvedUnitBaselineOffset = UnitBaselineOffset * unitScale;

        var resolvedLineHeight = BaselineHelper.GetTextHeightFromLayout("Aq", ValueTypeface, resolvedValueSize);
        var freeSpace = System.Math.Max(0, availableHeight - resolvedLineHeight);
        var descent = BaselineHelper.GetDescentFromLayout("Aq", ValueTypeface, resolvedValueSize);
        ValueVerticalOffset = System.Math.Min(freeSpace, descent * 0.45);
    }

    private void OnBitChoiceClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int bitIndex })
        {
            BitChoiceClicked?.Invoke(this, new BitChoiceClickedEventArgs(bitIndex));
            e.Handled = true;
            return;
        }

        if (sender is Button { Tag: string bitText } && int.TryParse(bitText, out var parsedBitIndex))
        {
            BitChoiceClicked?.Invoke(this, new BitChoiceClickedEventArgs(parsedBitIndex));
            e.Handled = true;
        }
    }

    private void OnBoolChoiceClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int boolTag })
        {
            BoolChoiceClicked?.Invoke(this, new BoolChoiceClickedEventArgs(boolTag != 0));
            e.Handled = true;
            return;
        }

        if (sender is Button { Tag: string boolTextTag } && int.TryParse(boolTextTag, out var parsedBoolTag))
        {
            BoolChoiceClicked?.Invoke(this, new BoolChoiceClickedEventArgs(parsedBoolTag != 0));
            e.Handled = true;
        }
    }

    private static int ResolveBitColumns(int count)
    {
        return count <= 0 ? 1 : count;
    }
}
