using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;
using TEdit5.Services;

namespace TEdit5.Controls;

/// <summary>
/// 物品图标缓存. 图标来自用户自备的纹理目录, 每个物品一张 16x16 的
/// Item_{id}.png; 缺失时返回 null, 界面回落到色块.
/// </summary>
public static class ItemIconCache
{
    private static readonly Dictionary<(string Prefix, int Id), Bitmap?> Cache = new();
    private static string? _textureRoot;

    /// <summary>纹理目录, 取自用户设置, 缺省时回落到应用数据目录下的 Textures.</summary>
    public static string TextureRoot => _textureRoot ??= AppSettings.Load().TexturePath;

    /// <summary>内置图标是否随包发布, 不依赖界面平台, 供自检使用.</summary>
    public static bool HasBuiltin(string prefix, int id)
    {
        var assembly = typeof(ItemIconCache).Assembly;
        string suffix = $".Icons.{prefix}_{id}.png";
        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static Stream? OpenBuiltin(string prefix, int id)
    {
        var assembly = typeof(ItemIconCache).Assembly;
        string suffix = $".Icons.{prefix}_{id}.png";
        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return assembly.GetManifestResourceStream(name);
            }
        }

        return null;
    }

    /// <summary>取图标, 前缀决定文件名的类别 (Item / Tile / Wall).</summary>
    public static Bitmap? Get(int id, string prefix = "Item")
    {
        // 用前缀与编号组成元组键: 早期用 HashCode 合并成 int, 理论上存在碰撞导致串图的风险.
        var key = (prefix, id);
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        Bitmap? bitmap = null;
        try
        {
            // 先取随包发布的内置图标 (由游戏 XNB 解出), 再取用户目录, 都没有才回落色块.
            using var builtin = OpenBuiltin(prefix, id);
            if (builtin != null)
            {
                bitmap = new Bitmap(builtin);
            }
        }
        catch (Exception)
        {
            bitmap = null;
        }

        try
        {
            if (bitmap == null)
            {
                string path = Path.Combine(TextureRoot, $"{prefix}_{id}.png");
                if (File.Exists(path))
                {
                    bitmap = new Bitmap(path);
                }
            }
        }
        catch (Exception)
        {
            bitmap = null;
        }

        Cache[key] = bitmap;
        return bitmap;
    }
}
