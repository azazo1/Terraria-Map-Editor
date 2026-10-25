using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TEdit5.Controls;

/// <summary>
/// 从应用资源里取矢量图标. 图标资源由 Views/Icons.axaml 提供.
/// </summary>
public static class AppIcons
{
    public static Geometry Get(string key)
    {
        if (Application.Current?.TryFindResource(key, out var value) == true && value is Geometry geometry)
        {
            return geometry;
        }

        return StreamGeometry.Parse("M0 0 H1 V1 H0 Z");
    }
}
