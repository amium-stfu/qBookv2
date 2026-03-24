using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using System.IO;
using System.Diagnostics;
using Amium.UiEditor.Helpers;

namespace Amium.UiEditor.Controls;

public partial class ThemeSvgIcon : UserControl
{
    public static readonly StyledProperty<string?> IconPathProperty =
        AvaloniaProperty.Register<ThemeSvgIcon, string?>(nameof(IconPath));

    public static readonly StyledProperty<string?> TintColorProperty =
        AvaloniaProperty.Register<ThemeSvgIcon, string?>(nameof(TintColor));

    private static readonly PropertyInfo? SvgPathProperty = Type
        .GetType("Avalonia.Svg.Skia.Svg, Avalonia.Svg.Skia", throwOnError: false)
        ?.GetProperty("Path");

    private Control? _iconElement;

    static ThemeSvgIcon()
    {
        IconPathProperty.Changed.AddClassHandler<ThemeSvgIcon>((icon, _) => icon.UpdateResolvedPath());
        TintColorProperty.Changed.AddClassHandler<ThemeSvgIcon>((icon, _) => icon.UpdateResolvedPath());
    }

    public ThemeSvgIcon()
    {
        InitializeComponent();
        _iconElement = this.FindControl<Control>("IconElement");
        UpdateResolvedPath();
    }

    public string? IconPath
    {
        get => GetValue(IconPathProperty);
        set => SetValue(IconPathProperty, value);
    }

    public string? TintColor
    {
        get => GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    private void UpdateResolvedPath()
    {
        if (_iconElement is null || SvgPathProperty is null)
        {
            return;
        }

        try
        {
            var resolvedPath = SvgIconCache.ResolvePath(IconPath, TintColor);
            var hasPath = !string.IsNullOrWhiteSpace(resolvedPath);
            _iconElement.IsVisible = hasPath;
            SvgPathProperty.SetValue(_iconElement, hasPath ? resolvedPath : null);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            _iconElement.IsVisible = false;
            SvgPathProperty.SetValue(_iconElement, null);
            Debug.WriteLine($"ThemeSvgIcon could not load icon '{IconPath}': {ex.Message}");
        }
    }
}