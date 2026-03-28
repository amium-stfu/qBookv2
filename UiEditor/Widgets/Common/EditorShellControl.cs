using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Amium.EditorUi.Controls;

public partial class EditorShellControl : UserControl
{
    public static readonly StyledProperty<object?> HeaderContentProperty =
        AvaloniaProperty.Register<EditorShellControl, object?>(nameof(HeaderContent));

    public static readonly StyledProperty<object?> HeaderRightContentProperty =
        AvaloniaProperty.Register<EditorShellControl, object?>(nameof(HeaderRightContent));

    public static readonly StyledProperty<object?> SubHeaderContentProperty =
        AvaloniaProperty.Register<EditorShellControl, object?>(nameof(SubHeaderContent));

    public static readonly StyledProperty<object?> BodyContentProperty =
        AvaloniaProperty.Register<EditorShellControl, object?>(nameof(BodyContent));

    public static readonly StyledProperty<object?> FooterContentProperty =
        AvaloniaProperty.Register<EditorShellControl, object?>(nameof(FooterContent));

    public static readonly StyledProperty<object?> OverlayContentProperty =
        AvaloniaProperty.Register<EditorShellControl, object?>(nameof(OverlayContent));

    public static readonly StyledProperty<bool> ShowHeaderProperty =
        AvaloniaProperty.Register<EditorShellControl, bool>(nameof(ShowHeader), true);

    public static readonly StyledProperty<bool> ShowSubHeaderProperty =
        AvaloniaProperty.Register<EditorShellControl, bool>(nameof(ShowSubHeader), false);

    public static readonly StyledProperty<bool> ShowFooterProperty =
        AvaloniaProperty.Register<EditorShellControl, bool>(nameof(ShowFooter), false);

    public static readonly StyledProperty<bool> ShowOverlayContentProperty =
        AvaloniaProperty.Register<EditorShellControl, bool>(nameof(ShowOverlayContent), false);

    public static readonly StyledProperty<IBrush?> ShellBackgroundProperty =
        AvaloniaProperty.Register<EditorShellControl, IBrush?>(nameof(ShellBackground), Brushes.Transparent);

    public static readonly StyledProperty<IBrush?> ShellBorderBrushProperty =
        AvaloniaProperty.Register<EditorShellControl, IBrush?>(nameof(ShellBorderBrush), Brushes.Transparent);

    public static readonly StyledProperty<Thickness> ShellBorderThicknessProperty =
        AvaloniaProperty.Register<EditorShellControl, Thickness>(nameof(ShellBorderThickness), new Thickness(0));

    public static readonly StyledProperty<CornerRadius> ShellCornerRadiusProperty =
        AvaloniaProperty.Register<EditorShellControl, CornerRadius>(nameof(ShellCornerRadius), new CornerRadius(0));

    public static readonly StyledProperty<Thickness> ShellPaddingProperty =
        AvaloniaProperty.Register<EditorShellControl, Thickness>(nameof(ShellPadding), new Thickness(0));

    public static readonly StyledProperty<IBrush?> HeaderBackgroundProperty =
        AvaloniaProperty.Register<EditorShellControl, IBrush?>(nameof(HeaderBackground), Brushes.Transparent);

    public static readonly StyledProperty<IBrush?> HeaderBorderBrushProperty =
        AvaloniaProperty.Register<EditorShellControl, IBrush?>(nameof(HeaderBorderBrush), Brushes.Transparent);

    public static readonly StyledProperty<Thickness> HeaderBorderThicknessProperty =
        AvaloniaProperty.Register<EditorShellControl, Thickness>(nameof(HeaderBorderThickness), new Thickness(0));

    public static readonly StyledProperty<CornerRadius> HeaderCornerRadiusProperty =
        AvaloniaProperty.Register<EditorShellControl, CornerRadius>(nameof(HeaderCornerRadius), new CornerRadius(0));

    public static readonly StyledProperty<Thickness> HeaderPaddingProperty =
        AvaloniaProperty.Register<EditorShellControl, Thickness>(nameof(HeaderPadding), new Thickness(0));

    public static readonly StyledProperty<double> HeaderMinHeightProperty =
        AvaloniaProperty.Register<EditorShellControl, double>(nameof(HeaderMinHeight), 0d);

    public static readonly StyledProperty<IBrush?> BodyBackgroundProperty =
        AvaloniaProperty.Register<EditorShellControl, IBrush?>(nameof(BodyBackground), Brushes.Transparent);

    public static readonly StyledProperty<IBrush?> BodyBorderBrushProperty =
        AvaloniaProperty.Register<EditorShellControl, IBrush?>(nameof(BodyBorderBrush), Brushes.Transparent);

    public static readonly StyledProperty<Thickness> BodyBorderThicknessProperty =
        AvaloniaProperty.Register<EditorShellControl, Thickness>(nameof(BodyBorderThickness), new Thickness(0));

    public static readonly StyledProperty<CornerRadius> BodyCornerRadiusProperty =
        AvaloniaProperty.Register<EditorShellControl, CornerRadius>(nameof(BodyCornerRadius), new CornerRadius(0));

    public static readonly StyledProperty<Thickness> BodyPaddingProperty =
        AvaloniaProperty.Register<EditorShellControl, Thickness>(nameof(BodyPadding), new Thickness(0));

    public static readonly StyledProperty<IBrush?> FooterBackgroundProperty =
        AvaloniaProperty.Register<EditorShellControl, IBrush?>(nameof(FooterBackground), Brushes.Transparent);

    public static readonly StyledProperty<IBrush?> FooterBorderBrushProperty =
        AvaloniaProperty.Register<EditorShellControl, IBrush?>(nameof(FooterBorderBrush), Brushes.Transparent);

    public static readonly StyledProperty<Thickness> FooterBorderThicknessProperty =
        AvaloniaProperty.Register<EditorShellControl, Thickness>(nameof(FooterBorderThickness), new Thickness(0));

    public static readonly StyledProperty<CornerRadius> FooterCornerRadiusProperty =
        AvaloniaProperty.Register<EditorShellControl, CornerRadius>(nameof(FooterCornerRadius), new CornerRadius(0));

    public EditorShellControl()
    {
        InitializeComponent();
    }

    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public object? HeaderRightContent
    {
        get => GetValue(HeaderRightContentProperty);
        set => SetValue(HeaderRightContentProperty, value);
    }

    public object? SubHeaderContent
    {
        get => GetValue(SubHeaderContentProperty);
        set => SetValue(SubHeaderContentProperty, value);
    }

    public object? BodyContent
    {
        get => GetValue(BodyContentProperty);
        set => SetValue(BodyContentProperty, value);
    }

    public object? FooterContent
    {
        get => GetValue(FooterContentProperty);
        set => SetValue(FooterContentProperty, value);
    }

    public object? OverlayContent
    {
        get => GetValue(OverlayContentProperty);
        set => SetValue(OverlayContentProperty, value);
    }

    public bool ShowHeader
    {
        get => GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    public bool ShowSubHeader
    {
        get => GetValue(ShowSubHeaderProperty);
        set => SetValue(ShowSubHeaderProperty, value);
    }

    public bool ShowFooter
    {
        get => GetValue(ShowFooterProperty);
        set => SetValue(ShowFooterProperty, value);
    }

    public bool ShowOverlayContent
    {
        get => GetValue(ShowOverlayContentProperty);
        set => SetValue(ShowOverlayContentProperty, value);
    }

    public IBrush? ShellBackground
    {
        get => GetValue(ShellBackgroundProperty);
        set => SetValue(ShellBackgroundProperty, value);
    }

    public IBrush? ShellBorderBrush
    {
        get => GetValue(ShellBorderBrushProperty);
        set => SetValue(ShellBorderBrushProperty, value);
    }

    public Thickness ShellBorderThickness
    {
        get => GetValue(ShellBorderThicknessProperty);
        set => SetValue(ShellBorderThicknessProperty, value);
    }

    public CornerRadius ShellCornerRadius
    {
        get => GetValue(ShellCornerRadiusProperty);
        set => SetValue(ShellCornerRadiusProperty, value);
    }

    public Thickness ShellPadding
    {
        get => GetValue(ShellPaddingProperty);
        set => SetValue(ShellPaddingProperty, value);
    }

    public IBrush? HeaderBackground
    {
        get => GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    public IBrush? HeaderBorderBrush
    {
        get => GetValue(HeaderBorderBrushProperty);
        set => SetValue(HeaderBorderBrushProperty, value);
    }

    public Thickness HeaderBorderThickness
    {
        get => GetValue(HeaderBorderThicknessProperty);
        set => SetValue(HeaderBorderThicknessProperty, value);
    }

    public CornerRadius HeaderCornerRadius
    {
        get => GetValue(HeaderCornerRadiusProperty);
        set => SetValue(HeaderCornerRadiusProperty, value);
    }

    public Thickness HeaderPadding
    {
        get => GetValue(HeaderPaddingProperty);
        set => SetValue(HeaderPaddingProperty, value);
    }

    public double HeaderMinHeight
    {
        get => GetValue(HeaderMinHeightProperty);
        set => SetValue(HeaderMinHeightProperty, value);
    }

    public IBrush? BodyBackground
    {
        get => GetValue(BodyBackgroundProperty);
        set => SetValue(BodyBackgroundProperty, value);
    }

    public IBrush? BodyBorderBrush
    {
        get => GetValue(BodyBorderBrushProperty);
        set => SetValue(BodyBorderBrushProperty, value);
    }

    public Thickness BodyBorderThickness
    {
        get => GetValue(BodyBorderThicknessProperty);
        set => SetValue(BodyBorderThicknessProperty, value);
    }

    public CornerRadius BodyCornerRadius
    {
        get => GetValue(BodyCornerRadiusProperty);
        set => SetValue(BodyCornerRadiusProperty, value);
    }

    public Thickness BodyPadding
    {
        get => GetValue(BodyPaddingProperty);
        set => SetValue(BodyPaddingProperty, value);
    }

    public IBrush? FooterBackground
    {
        get => GetValue(FooterBackgroundProperty);
        set => SetValue(FooterBackgroundProperty, value);
    }

    public IBrush? FooterBorderBrush
    {
        get => GetValue(FooterBorderBrushProperty);
        set => SetValue(FooterBorderBrushProperty, value);
    }

    public Thickness FooterBorderThickness
    {
        get => GetValue(FooterBorderThicknessProperty);
        set => SetValue(FooterBorderThicknessProperty, value);
    }

    public CornerRadius FooterCornerRadius
    {
        get => GetValue(FooterCornerRadiusProperty);
        set => SetValue(FooterCornerRadiusProperty, value);
    }

    // Alias class to support the new "Widget" naming without changing XAML x:Class.
}

public partial class EditorShellWidget : EditorShellControl
{
}