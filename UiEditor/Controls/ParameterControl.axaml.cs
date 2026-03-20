using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using UiEditor.ViewModels;

namespace UiEditor.Controls;

public sealed class BitChoiceClickedEventArgs : EventArgs
{
    public BitChoiceClickedEventArgs(int bitIndex)
    {
        BitIndex = bitIndex;
    }

    public int BitIndex { get; }
}

public partial class ParameterControl : UserControl
{
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

    public ParameterControl()
    {
        InitializeComponent();
    }

    public event EventHandler<BitChoiceClickedEventArgs>? BitChoiceClicked;

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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PresentationProperty)
        {
            BitColumns = ResolveBitColumns(Presentation?.Definition.BitCount ?? 0);
        }
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

    private static int ResolveBitColumns(int count)
    {
        return count <= 0 ? 1 : count;
    }
}
