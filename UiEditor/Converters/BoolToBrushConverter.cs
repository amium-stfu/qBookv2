using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace UiEditor.Converters;

public sealed class BoolToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isTrue = value is bool state && state;
        var colorValue = isTrue ? parameter as string : null;

        if (string.IsNullOrWhiteSpace(colorValue))
        {
            return Brushes.Transparent;
        }

        return Brush.Parse(colorValue);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
