using System;
using SkiaSharp;
using TEdit.Common;
using TEdit.Terraria;

namespace TEdit5.Services;

/// <summary>
/// 把世界的一块区域渲染成缩略图. 每格对应一个像素, 颜色取自方块与墙体属性,
/// 与调色板里色块的取色方式一致, 不依赖游戏素材.
/// </summary>
public static class WorldThumbnailRenderer
{
    /// <summary>渲染指定区域, 宽高按格数算.</summary>
    public static SKBitmap? Render(World? world, int x, int y, int width, int height)
    {
        if (world == null || width <= 0 || height <= 0)
        {
            return null;
        }

        int totalWidth = world.Tiles.GetLength(0);
        int totalHeight = world.Tiles.GetLength(1);
        int w = Math.Min(width, totalWidth - x);
        int h = Math.Min(height, totalHeight - y);
        if (w <= 0 || h <= 0)
        {
            return null;
        }

        var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { IsAntialias = false };

        for (int ty = 0; ty < h; ty++)
        {
            for (int tx = 0; tx < w; tx++)
            {
                int worldX = x + tx;
                int worldY = y + ty;
                var tile = world.Tiles[worldX, worldY];

                SKColor color = SKColors.Transparent;
                if (tile.Wall > 0)
                {
                    // 优先用图集首帧的平均色, 这样缩略图的色调与游戏材质一致.
                    color = TileAtlas.GetAverageColor("Walls", tile.Wall)
                        ?? ToSkColor(WallColor(tile.Wall), 0.75f);
                }

                if (tile.IsActive)
                {
                    color = TileAtlas.GetAverageColor("Tiles", tile.Type)
                        ?? ToSkColor(TileColor(tile.Type), 1f);
                }

                paint.Color = color;
                canvas.DrawRect(tx, ty, 1, 1, paint);
            }
        }

        return bitmap;
    }

    private static TEditColor TileColor(int id)
    {
        var properties = WorldConfiguration.TileProperties;
        return id >= 0 && id < properties.Count ? properties[id].Color : TEditColor.White;
    }

    private static TEditColor WallColor(int id)
    {
        var properties = WorldConfiguration.WallProperties;
        return id >= 0 && id < properties.Count ? properties[id].Color : TEditColor.White;
    }

    private static SKColor ToSkColor(TEditColor color, float scale)
    {
        byte r = (byte)Math.Clamp(color.R * scale, 0, 255);
        byte g = (byte)Math.Clamp(color.G * scale, 0, 255);
        byte b = (byte)Math.Clamp(color.B * scale, 0, 255);
        return new SKColor(r, g, b, 255);
    }
}
