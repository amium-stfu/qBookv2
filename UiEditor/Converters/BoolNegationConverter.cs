using System.Globalization;
using Avalonia.Data.Converters;

namespace Amium.EditorUi.Converters;

public sealed class BoolNegationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool state ? !state : false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool state ? !state : false;
}
