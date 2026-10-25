using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using TEdit.Common;

namespace TEdit5.Controls;

/// <summary>把 TEditColor 转成画刷, 用于样式色块.</summary>
public sealed class TEditColorBrushConverter : IValueConverter
{
    public static readonly TEditColorBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TEditColor color)
        {
            return Brushes.Transparent;
        }

        return new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
