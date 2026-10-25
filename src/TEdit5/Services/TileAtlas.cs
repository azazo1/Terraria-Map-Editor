using System;
using System.Collections.Generic;
using System.Reflection;
using SkiaSharp;

namespace TEdit5.Services;

/// <summary>
/// 方块与墙体的图集. 图集是从游戏 XNB 解出的 PNG, 每帧 16x16,
/// 以嵌入资源随程序集发布, 因此渲染不依赖用户自备素材, 也不依赖界面平台初始化.
/// </summary>
public static class TileAtlas
{
    /// <summary>单帧边长.</summary>
    public const int FrameSize = 16;

    private static readonly Assembly Owner = typeof(TileAtlas).Assembly;
    private static readonly Dictionary<(string Kind, int Id), SKBitmap?> Cache = new();
    private static readonly Dictionary<(string Kind, int Id), SKColor?> AverageCache = new();
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal);

    private static bool _namesLoaded;

    /// <summary>取方块图集, 编号不存在时返回 null.</summary>
    public static SKBitmap? GetTile(int id) => Load("Tiles", id);

    /// <summary>取墙体图集, 编号不存在时返回 null.</summary>
    public static SKBitmap? GetWall(int id) => Load("Walls", id);

    /// <summary>图集是否存在.</summary>
    public static bool Has(string kind, int id) => ResolveName(kind, id) != null;

    /// <summary>图集的帧列数与行数, 图集不存在时返回 0.</summary>
    public static (int Columns, int Rows) GetFrameGrid(string kind, int id)
    {
        var bitmap = Load(kind, id);
        return bitmap == null
            ? (0, 0)
            : (bitmap.Width / FrameSize, bitmap.Height / FrameSize);
    }

    /// <summary>取图集首帧的平均色, 用于缩略图等按格取色的场合; 整帧透明时返回 null.</summary>
    public static SKColor? GetAverageColor(string kind, int id)
    {
        var key = (kind, id);
        if (AverageCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        SKColor? result = null;
        var bitmap = Load(kind, id);
        if (bitmap != null)
        {
            long r = 0, g = 0, b = 0, n = 0;
            int maxX = Math.Min(FrameSize, bitmap.Width);
            int maxY = Math.Min(FrameSize, bitmap.Height);
            for (int py = 0; py < maxY; py++)
            {
                for (int px = 0; px < maxX; px++)
                {
                    var pixel = bitmap.GetPixel(px, py);
                    if (pixel.Alpha < 32)
                    {
                        continue;
                    }

                    r += pixel.Red;
                    g += pixel.Green;
                    b += pixel.Blue;
                    n++;
                }
            }

            if (n > 0)
            {
                result = new SKColor((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
            }
        }

        AverageCache[key] = result;
        return result;
    }

    private static SKBitmap? Load(string kind, int id)
    {
        var key = (kind, id);
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        SKBitmap? bitmap = null;
        string? name = ResolveName(kind, id);
        if (name != null)
        {
            try
            {
                using var stream = Owner.GetManifestResourceStream(name);
                if (stream != null)
                {
                    bitmap = SKBitmap.Decode(stream);
                }
            }
            catch (Exception)
            {
                bitmap = null;
            }
        }

        Cache[key] = bitmap;
        return bitmap;
    }

    private static string? ResolveName(string kind, int id)
    {
        if (!_namesLoaded)
        {
            foreach (string name in Owner.GetManifestResourceNames())
            {
                Names.Add(name);
            }

            _namesLoaded = true;
        }

        string suffix = $".Textures.{kind}.{id}.png";
        foreach (string name in Names)
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return name;
            }
        }

        return null;
    }
}
