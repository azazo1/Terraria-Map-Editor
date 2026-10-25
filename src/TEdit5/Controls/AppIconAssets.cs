using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TEdit5.Controls;

/// <summary>
/// 界面图标位图. 图标来自仓库 docs/Icons (原版 WPF 工具栏用的 famfamfam 图标),
/// 编译时随 Assets 一起打进包里, 不依赖用户自备文件.
/// </summary>
public static class AppIconAssets
{
    private const string Root = "avares://TEdit5/Assets/Icons/";

    private static readonly Dictionary<string, Bitmap?> Cache = new();

    /// <summary>按文件名取图标, 缺失时返回 null, 由调用方回落到矢量图标.</summary>
    public static Bitmap? Load(string fileName)
    {
        if (Cache.TryGetValue(fileName, out var cached))
        {
            return cached;
        }

        Bitmap? bitmap = null;
        try
        {
            var uri = new Uri(Root + fileName);
            if (AssetLoader.Exists(uri))
            {
                using var stream = AssetLoader.Open(uri);
                bitmap = new Bitmap(stream);
            }
        }
        catch (Exception)
        {
            bitmap = null;
        }

        Cache[fileName] = bitmap;
        return bitmap;
    }
}
