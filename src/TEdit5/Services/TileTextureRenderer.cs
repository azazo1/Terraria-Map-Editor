using System;
using SkiaSharp;
using TEdit.Terraria;

namespace TEdit5.Services;

/// <summary>
/// 按真实图集绘制一片世界区域. 每格从对应图集取 16x16 帧贴上去,
/// 与游戏中看到的样子一致, 而不是把一整格画成一个纯色像素.
/// </summary>
public static class TileTextureRenderer
{
    /// <summary>
    /// 按坐标选出图集首行的一帧. 同一坐标结果固定, 相邻格会取到不同变体.
    /// </summary>
    public static int SelectVariant(int worldX, int worldY, int columns)
    {
        if (columns <= 1)
        {
            return 0;
        }

        // 只在前三列里选: 这三列是游戏用于随机变体的位置, 再往后的列是受损或特殊形态.
        int usable = Math.Min(columns, 3);
        int hash = unchecked((worldX * 73856093) ^ (worldY * 19349663));
        return Math.Abs(hash) % usable;
    }

    /// <summary>
    /// 把 (startX, startY) 起的 cols x rows 格画到 canvas 上, 每格边长 tilePixels.
    /// 图集缺失的方块回落到纯色, 保证不会留空洞.
    /// </summary>
    public static void DrawRegion(
        SKCanvas canvas,
        World world,
        int startX,
        int startY,
        int cols,
        int rows,
        int tilePixels,
        SKColor fallbackColor)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(world);

        if (tilePixels <= 0 || cols <= 0 || rows <= 0)
        {
            return;
        }

        int worldWidth = world.Tiles.GetLength(0);
        int worldHeight = world.Tiles.GetLength(1);

        using var paint = new SKPaint { IsAntialias = false };
        var source = new SKRect(0, 0, TileAtlas.FrameSize, TileAtlas.FrameSize);
        var destination = new SKRect(0, 0, tilePixels, tilePixels);

        for (int row = 0; row < rows; row++)
        {
            int worldY = startY + row;
            if (worldY < 0 || worldY >= worldHeight)
            {
                continue;
            }

            for (int col = 0; col < cols; col++)
            {
                int worldX = startX + col;
                if (worldX < 0 || worldX >= worldWidth)
                {
                    continue;
                }

                var tile = world.Tiles[worldX, worldY];
                destination.Left = col * tilePixels;
                destination.Top = row * tilePixels;
                destination.Right = destination.Left + tilePixels;
                destination.Bottom = destination.Top + tilePixels;

                SKBitmap? atlas = null;
                if (tile.Wall > 0)
                {
                    atlas = TileAtlas.GetWall(tile.Wall);
                }

                if (tile.IsActive)
                {
                    atlas = TileAtlas.GetTile(tile.Type) ?? atlas;
                }

                if (atlas != null)
                {
                    // 先按四邻拼接选帧, 拿不到时退回按坐标选变体.
                    int columns = Math.Max(1, atlas.Width / TileAtlas.FrameSize);
                    int variant = SelectVariant(worldX, worldY, columns);
                    int frameLeft = variant * TileAtlas.FrameSize;
                    int frameTop = 0;
                    var resolved = TileMaskCalculator.ResolveFrame(world, worldX, worldY, variant);
                    if (resolved.HasValue)
                    {
                        frameLeft = resolved.Value.X;
                        frameTop = resolved.Value.Y;
                    }

                    if (frameLeft >= atlas.Width || frameTop >= atlas.Height)
                    {
                        frameLeft = 0;
                        frameTop = 0;
                    }

                    source.Left = frameLeft;
                    source.Top = frameTop;
                    source.Right = frameLeft + TileAtlas.FrameSize;
                    source.Bottom = frameTop + TileAtlas.FrameSize;
                    canvas.DrawBitmap(atlas, source, destination, new SKSamplingOptions(SKFilterMode.Nearest));
                }
                else if (tile.IsActive || tile.Wall > 0)
                {
                    paint.Color = fallbackColor;
                    canvas.DrawRect(destination, paint);
                }
            }
        }
    }
}
