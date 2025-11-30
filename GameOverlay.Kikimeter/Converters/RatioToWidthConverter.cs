using System;
using System.Globalization;
using System.Windows.Data;

namespace GameOverlay.Kikimeter.Converters;

public sealed class RatioToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not { Length: >= 2 })
            return 0d;

        if (values[0] is not double ratio || double.IsNaN(ratio) || double.IsInfinity(ratio))
            ratio = 0d;

        ratio = Math.Clamp(ratio, 0d, 1d);

        if (values[1] is not double width || double.IsNaN(width) || double.IsInfinity(width))
            width = 0d;

        return width * ratio;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}



