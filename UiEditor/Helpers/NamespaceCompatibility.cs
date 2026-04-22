using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Amium.EditorUi.Converters
{
    public sealed class BoolToBrushConverter : IValueConverter
    {
        private readonly Amium.UiEditor.Converters.BoolToBrushConverter _impl = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => _impl.Convert(value, targetType, parameter, culture);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => _impl.ConvertBack(value, targetType, parameter, culture);
    }

    public sealed class BoolNegationConverter : IValueConverter
    {
        private readonly Amium.UiEditor.Converters.BoolNegationConverter _impl = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => _impl.Convert(value, targetType, parameter, culture);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => _impl.ConvertBack(value, targetType, parameter, culture);
    }
}

namespace Amium.EditorUi
{
    public interface IEditorUiHost
    {
        bool IsEditMode { get; }

        string? PrimaryTextBrush { get; }

        void OpenItemEditor(object item, double x, double y);

        bool DeleteItem(object item);

        void RefreshFolderBindings(string pageName);
    }
}

namespace Amium.EditorUi.Helpers
{
    internal static class SvgIconCache
    {
        public static string? ResolvePath(string? iconPath, string? tintColor)
            => Amium.UiEditor.Helpers.SvgIconCache.ResolvePath(iconPath, tintColor);
    }
}
