using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ClassIsland.Converters;

/// <summary>
/// 把数值乘以 ConverterParameter 指定的系数，用于按缩放比例计算字号等尺寸。
/// </summary>
public sealed class MultiplyDoubleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var scale = value is double d ? d : value is float f ? f : 1.0;
        if (parameter is string text &&
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor))
        {
            return scale * factor;
        }

        if (parameter is double factorValue)
        {
            return scale * factorValue;
        }

        return scale;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
