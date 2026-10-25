using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace TEdit5.Controls;

/// <summary>
/// 把物品编号转成图标. 参数可指定文件名前缀, 省略时按物品处理.
/// </summary>
public class ItemIconConverter : IValueConverter
{
    public static readonly ItemIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int id
            ? ItemIconCache.Get(id, parameter as string ?? "Item")
            : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
