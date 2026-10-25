using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using System;
using System.Collections.Generic;
using TEdit.Common;
using TEdit.Terraria;
using TEdit5.Services;

namespace TEdit5.Controls.WorldRenderEngine.Layers;


public class RasterTileRenderer
{
    private static TEditColor GetBackgroundColor(World _world, int y)
    {
        if (_world == null) { return TEditColor.White; }
        if (y < 80)
            return WorldConfiguration.GlobalColors["Space"];
        else if (y > _world.TilesHigh - 192)
            return WorldConfiguration.GlobalColors["Hell"];
        else if (y > _world.RockLevel)
            return WorldConfiguration.GlobalColors["Rock"];
        else if (y > _world.GroundLevel)
            return WorldConfiguration.GlobalColors["Earth"];
        else
            return WorldConfiguration.GlobalColors["Sky"];
    }

    public static SKColor GetBlockColor(World _world, int currentBlockX, int currentBlockY)
    {
        var block = _world.Tiles[currentBlockX, currentBlockY];
        var bgColor = GetBackgroundColor(_world, currentBlockY);
        return PixelMap.GetTileColor(block, bgColor).ToSKColor().WithAlpha(255);
    }

    /// <summary>查找命中的坐标集合, 供画面高亮.</summary>
    public static HashSet<(int X, int Y)>? FindHighlights { get; set; }

    /// <summary>命中查找结果时给像素叠一层亮色.</summary>
    private static SKColor ApplyHighlight(int x, int y, SKColor color)
    {
        var highlights = FindHighlights;
        if (highlights == null || highlights.Count == 0 || !highlights.Contains((x, y)))
        {
            return color;
        }

        return new SKColor(
            (byte)Math.Min(255, color.Red + 60),
            (byte)Math.Min(255, color.Green + 90),
            (byte)Math.Max(0, color.Blue - 40),
            color.Alpha);
    }

    /// <summary>当前生效的渲染过滤器, 由世界视图设置.</summary>
    public static WorldFilter? Filter { get; set; }

    /// <summary>按过滤条件调整单个方块的颜色.</summary>
    private static SKColor ApplyFilter(Tile block, SKColor color)
    {
        var filter = Filter;
        if (filter == null || !filter.IsEnabled)
        {
            return color;
        }

        bool filtered =
            (block.IsActive && filter.IsTileFiltered(block.Type))
            || (block.Wall > 0 && filter.IsWallFiltered(block.Wall))
            || (block.HasLiquid && filter.IsLiquidFiltered((int)block.LiquidType))
            || (block.WireRed && filter.IsWireFiltered(0))
            || (block.WireBlue && filter.IsWireFiltered(1))
            || (block.WireGreen && filter.IsWireFiltered(2))
            || (block.WireYellow && filter.IsWireFiltered(3));

        if (!filtered)
        {
            return color;
        }

        if (filter.HideFilteredTiles)
        {
            return new SKColor(0, 0, 0, 0);
        }

        // 淡化与去色.
        double darken = Math.Clamp(filter.DarkenAmount / 100d, 0d, 1d);
        double desaturate = Math.Clamp(filter.DesaturateAmount / 100d, 0d, 1d);
        double grey = (color.Red * 0.299d) + (color.Green * 0.587d) + (color.Blue * 0.114d);
        double r = (color.Red * (1 - desaturate)) + (grey * desaturate);
        double g = (color.Green * (1 - desaturate)) + (grey * desaturate);
        double b = (color.Blue * (1 - desaturate)) + (grey * desaturate);
        double k = 1d - darken;
        return new SKColor(
            (byte)Math.Clamp(r * k, 0, 255),
            (byte)Math.Clamp(g * k, 0, 255),
            (byte)Math.Clamp(b * k, 0, 255),
            color.Alpha);
    }

    public static SKBitmap CreateBitmapTile(World _world, int xTile, int yTile, int tileSizeX, int tileSizeY)
    {
        ArgumentNullException.ThrowIfNull(_world);

        var bmp = new SKBitmap(tileSizeX, tileSizeY, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

        var world = _world;
        int worldWidth = world.TilesWide;
        int worldHeight = world.TilesHigh;

        var blocks = world.Tiles;

        int blockX = xTile * tileSizeX;
        int blockY = yTile * tileSizeY;

        for (int x = 0; x + blockX < worldWidth && x < tileSizeX; x++)
        {
            for (int y = 0; y + blockY < worldHeight && y < tileSizeY; y++)
            {
                var currentBlockX = x + blockX;
                var currentBlockY = y + blockY;

                var block = blocks[currentBlockX, currentBlockY];
                //if (block.IsActive)
                {
                    var bgColor = GetBackgroundColor(_world, currentBlockY);
                    var tileColor = PixelMap.GetTileColor(block, bgColor).ToSKColor().WithAlpha(255);
                    tileColor = ApplyFilter(block, tileColor);
                    tileColor = ApplyHighlight(currentBlockX, currentBlockY, tileColor);

                    bmp.SetPixel(
                        x,
                        y,
                        tileColor);
                }
            }
        }

        //bmp.SetImmutable();


        return bmp;
    }

    public static void UpdateBitmapTileRegion(SKBitmap bmp, World _world, int xTile, int yTile, int tileSizeX, int tileSizeY, SKRectI dirtyRegion)
    {
        ArgumentNullException.ThrowIfNull(_world);
        ArgumentNullException.ThrowIfNull(bmp);

        int worldWidth = _world.TilesWide;
        int worldHeight = _world.TilesHigh;
        var blocks = _world.Tiles;

        int blockX = xTile * tileSizeX;
        int blockY = yTile * tileSizeY;

        int startX = Math.Max(0, dirtyRegion.Left);
        int endX = Math.Min(tileSizeX, dirtyRegion.Right);
        int startY = Math.Max(0, dirtyRegion.Top);
        int endY = Math.Min(tileSizeY, dirtyRegion.Bottom);

        for (int x = startX; x < endX && x + blockX < worldWidth; x++)
        {
            for (int y = startY; y < endY && y + blockY < worldHeight; y++)
            {
                var currentBlockX = x + blockX;
                var currentBlockY = y + blockY;

                var block = blocks[currentBlockX, currentBlockY];
                var bgColor = GetBackgroundColor(_world, currentBlockY);
                var tileColor = PixelMap.GetTileColor(block, bgColor).ToSKColor().WithAlpha(255);
                tileColor = ApplyFilter(block, tileColor);
                tileColor = ApplyHighlight(currentBlockX, currentBlockY, tileColor);

                bmp.SetPixel(x, y, tileColor);
            }
        }
    }
}

public class WorldPixelsCustomDrawOp : ICustomDrawOperation
{
    private readonly Vector _offset;
    private readonly World? _world;
    private readonly IRasterTileCache _tileCache;
    /// <summary>缩放达到该值时改用图集贴图绘制.</summary>
    private const double TexturedZoomThreshold = 8d;

    private readonly double _zoom = 1f;

    public WorldPixelsCustomDrawOp(
        Rect bounds,
        Vector offset,
        World? world,
        IRasterTileCache tileCache,
        double tileScale = 1f
        )
    {
        Bounds = bounds;
        _offset = offset;
        _world = world;
        this._tileCache = tileCache;
        _zoom = tileScale;
        LoadContent();
    }

    public void LoadContent()
    {

    }

    public void Dispose()
    {
        _redHighlight?.Dispose();
        _greenBorder?.Dispose();
        _bitmapPaint?.Dispose();
    }

    public Rect Bounds { get; }
    public bool HitTest(Point p) => false;
    public bool Equals(ICustomDrawOperation other) => false;

    public void Render(ImmediateDrawingContext context)
    {
        if (_world == null) { return; }

        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature == null) { return; }

        using var lease = leaseFeature.Lease();

        var canvas = lease.SkCanvas;
        DrawWorldPixelTiles(canvas);
    }

    private RasterTile? GetTile(int x, int y)
    {
        var tile = _tileCache.GetTile(x, y);
        return tile;
    }

    SKPaint _greenBorder = new SKPaint
    {
        Color = SKColors.Green,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f,
    };

    SKPaint _redHighlight = new SKPaint
    {
        Color = SKColors.Red.WithAlpha(128),
        Style = SKPaintStyle.Fill,
    };

    SKPaint _bitmapPaint = new SKPaint
    {
        IsAntialias = false,
        IsDither = false,
        Color = SKColors.White,
        Style = SKPaintStyle.Fill,
        BlendMode = SKBlendMode.SrcOver,
    };

    private void DrawWorldPixelTiles(SKCanvas canvas)
    {
        if (_world is not { } world) { return; }

        canvas.Save();

        int tileSizeX = _tileCache.TileSizeX;
        int tileSizeY = _tileCache.TileSizeY;

        int numTilesX = (int)Math.Ceiling((double)Bounds.Width / tileSizeX / _zoom) + 1;
        int numTilesY = (int)Math.Ceiling((double)Bounds.Height / tileSizeY / _zoom) + 1;

        var xTileOffset = (int)Math.Floor((double)_offset.X / tileSizeX / _zoom);
        var yTileOffset = (int)Math.Floor((double)_offset.Y / tileSizeY / _zoom);

        var maxTileX = Math.Min(_tileCache.TilesX, numTilesX + xTileOffset);
        var maxTileY = Math.Min(_tileCache.TilesY, numTilesY + yTileOffset);

        int dirtyTilesProcessed = 0;
        const int maxDirtyTilesPerFrame = 10;

        for (int x = xTileOffset; x < maxTileX; x++)
        {
            for (int y = yTileOffset; y < maxTileY; y++)
            {
                var tileCanvasX = x * _zoom * tileSizeX - (int)_offset.X + (int)Bounds.X;
                var tileCanvasY = y * _zoom * tileSizeY - (int)_offset.Y + (int)Bounds.Y;
                var tileCanvasWidth = tileSizeX * _zoom;
                var tileCanvasHeight = tileSizeY * _zoom;

                var tile = GetTile(x, y);

                var canvasRect = new SKRect(
                            (float)tileCanvasX,
                            (float)tileCanvasY,
                            (float)(tileCanvasX + tileCanvasWidth),
                            (float)(tileCanvasY + tileCanvasHeight));

                // Update dirty tiles
                if (tile != null && tile.IsDirty && dirtyTilesProcessed < maxDirtyTilesPerFrame)
                {
                    dirtyTilesProcessed++;

                    if (tile.Bitmap == null)
                    {
                        // First time creation - full tile
                        tile.Bitmap = RasterTileRenderer.CreateBitmapTile(world, x, y, tileSizeX, tileSizeY);
                    }
                    else if (tile.DirtyRegion.HasValue)
                    {
                        // Partial update - only dirty region
                        RasterTileRenderer.UpdateBitmapTileRegion(
                            tile.Bitmap, 
                            world, 
                            x, 
                            y, 
                            tileSizeX, 
                            tileSizeY, 
                            tile.DirtyRegion.Value);
                    }
                    else
                    {
                        // Full tile update (fallback)
                        tile.Bitmap?.Dispose();
                        tile.Bitmap = RasterTileRenderer.CreateBitmapTile(world, x, y, tileSizeX, tileSizeY);
                    }

                    tile.ClearDirtyRegion();
                }

                // 放大到足够倍数时直接用图集贴图, 而不是把整格放大成一个色块.
                if (_zoom >= TexturedZoomThreshold)
                {
                    canvas.Save();
                    canvas.Translate(canvasRect.Left, canvasRect.Top);
                    canvas.ClipRect(new SKRect(0, 0, (float)tileCanvasWidth, (float)tileCanvasHeight));
                    TEdit5.Services.TileTextureRenderer.DrawRegion(
                        canvas,
                        world,
                        x * tileSizeX,
                        y * tileSizeY,
                        tileSizeX,
                        tileSizeY,
                        (int)Math.Max(1, Math.Round(_zoom)),
                        SKColors.Transparent);
                    canvas.Restore();
                }
                else if (tile?.Bitmap != null)
                {
                    canvas.DrawBitmap(
                        tile.Bitmap,
                        new SKRect(0, 0, tile.Bitmap.Width, tile.Bitmap.Height),
                        canvasRect,
                        new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None),
                        _bitmapPaint);
                }

                canvas.DrawRect(canvasRect, _greenBorder);
            }
        }

        canvas.Restore();
    }
}
