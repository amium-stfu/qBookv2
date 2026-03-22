using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Amium.UiEditor.Controls;

public class EditorContainerControl : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EditorContainerControl, string?>(nameof(Title));

    public static readonly StyledProperty<object?> HeaderPanelContentProperty =
        AvaloniaProperty.Register<EditorContainerControl, object?>(nameof(HeaderPanelContent));

    public static readonly StyledProperty<IBrush?> OuterBackgroundProperty =
        AvaloniaProperty.Register<EditorContainerControl, IBrush?>(nameof(OuterBackground), Brushes.Transparent);

    public static readonly StyledProperty<IBrush?> OuterBorderBrushProperty =
        AvaloniaProperty.Register<EditorContainerControl, IBrush?>(nameof(OuterBorderBrush), Brushes.Black);

    public static readonly StyledProperty<Thickness> OuterBorderThicknessProperty =
        AvaloniaProperty.Register<EditorContainerControl, Thickness>(nameof(OuterBorderThickness), new Thickness(1));

    public static readonly StyledProperty<CornerRadius> OuterCornerRadiusProperty =
        AvaloniaProperty.Register<EditorContainerControl, CornerRadius>(nameof(OuterCornerRadius), new CornerRadius(16));

    public static readonly StyledProperty<Thickness> OuterPaddingProperty =
        AvaloniaProperty.Register<EditorContainerControl, Thickness>(nameof(OuterPadding), new Thickness(18));

    public static readonly StyledProperty<double> HeaderHeightProperty =
        AvaloniaProperty.Register<EditorContainerControl, double>(nameof(HeaderHeight), double.NaN);

    public static readonly StyledProperty<double> HeaderFontSizeProperty =
        AvaloniaProperty.Register<EditorContainerControl, double>(nameof(HeaderFontSize), 15);

    public static readonly StyledProperty<FontWeight> HeaderFontWeightProperty =
        AvaloniaProperty.Register<EditorContainerControl, FontWeight>(nameof(HeaderFontWeight), FontWeight.Medium);

    public static readonly StyledProperty<IBrush?> HeaderPanelBackgroundProperty =
        AvaloniaProperty.Register<EditorContainerControl, IBrush?>(nameof(HeaderPanelBackground), Brushes.Transparent);

    public static readonly StyledProperty<Thickness> HeaderPanelPaddingProperty =
        AvaloniaProperty.Register<EditorContainerControl, Thickness>(nameof(HeaderPanelPadding), new Thickness(0));

    public static readonly StyledProperty<IBrush?> InnerBackgroundProperty =
        AvaloniaProperty.Register<EditorContainerControl, IBrush?>(nameof(InnerBackground), Brushes.Transparent);

    public static readonly StyledProperty<IBrush?> InnerBorderBrushProperty =
        AvaloniaProperty.Register<EditorContainerControl, IBrush?>(nameof(InnerBorderBrush), Brushes.Transparent);

    public static readonly StyledProperty<Thickness> InnerBorderThicknessProperty =
        AvaloniaProperty.Register<EditorContainerControl, Thickness>(nameof(InnerBorderThickness), new Thickness(0));

    public static readonly StyledProperty<CornerRadius> InnerCornerRadiusProperty =
        AvaloniaProperty.Register<EditorContainerControl, CornerRadius>(nameof(InnerCornerRadius), new CornerRadius(12));

    public static readonly StyledProperty<Thickness> InnerPaddingProperty =
        AvaloniaProperty.Register<EditorContainerControl, Thickness>(nameof(InnerPadding), new Thickness(0));

    public static readonly StyledProperty<Thickness> InnerMarginProperty =
        AvaloniaProperty.Register<EditorContainerControl, Thickness>(nameof(InnerMargin), new Thickness(0, 10, 0, 0));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? HeaderPanelContent
    {
        get => GetValue(HeaderPanelContentProperty);
        set => SetValue(HeaderPanelContentProperty, value);
    }

    public IBrush? OuterBackground
    {
        get => GetValue(OuterBackgroundProperty);
        set => SetValue(OuterBackgroundProperty, value);
    }

    public IBrush? OuterBorderBrush
    {
        get => GetValue(OuterBorderBrushProperty);
        set => SetValue(OuterBorderBrushProperty, value);
    }

    public Thickness OuterBorderThickness
    {
        get => GetValue(OuterBorderThicknessProperty);
        set => SetValue(OuterBorderThicknessProperty, value);
    }

    public CornerRadius OuterCornerRadius
    {
        get => GetValue(OuterCornerRadiusProperty);
        set => SetValue(OuterCornerRadiusProperty, value);
    }

    public Thickness OuterPadding
    {
        get => GetValue(OuterPaddingProperty);
        set => SetValue(OuterPaddingProperty, value);
    }

    public double HeaderHeight
    {
        get => GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    public double HeaderFontSize
    {
        get => GetValue(HeaderFontSizeProperty);
        set => SetValue(HeaderFontSizeProperty, value);
    }

    public FontWeight HeaderFontWeight
    {
        get => GetValue(HeaderFontWeightProperty);
        set => SetValue(HeaderFontWeightProperty, value);
    }

    public IBrush? HeaderPanelBackground
    {
        get => GetValue(HeaderPanelBackgroundProperty);
        set => SetValue(HeaderPanelBackgroundProperty, value);
    }

    public Thickness HeaderPanelPadding
    {
        get => GetValue(HeaderPanelPaddingProperty);
        set => SetValue(HeaderPanelPaddingProperty, value);
    }

    public IBrush? InnerBackground
    {
        get => GetValue(InnerBackgroundProperty);
        set => SetValue(InnerBackgroundProperty, value);
    }

    public IBrush? InnerBorderBrush
    {
        get => GetValue(InnerBorderBrushProperty);
        set => SetValue(InnerBorderBrushProperty, value);
    }

    public Thickness InnerBorderThickness
    {
        get => GetValue(InnerBorderThicknessProperty);
        set => SetValue(InnerBorderThicknessProperty, value);
    }

    public CornerRadius InnerCornerRadius
    {
        get => GetValue(InnerCornerRadiusProperty);
        set => SetValue(InnerCornerRadiusProperty, value);
    }

    public Thickness InnerPadding
    {
        get => GetValue(InnerPaddingProperty);
        set => SetValue(InnerPaddingProperty, value);
    }

    public Thickness InnerMargin
    {
        get => GetValue(InnerMarginProperty);
        set => SetValue(InnerMarginProperty, value);
    }
}
