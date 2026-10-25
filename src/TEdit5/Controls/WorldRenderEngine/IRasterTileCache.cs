using System;

namespace TEdit5.Controls.WorldRenderEngine;

public interface IRasterTileCache : IDisposable
{
    void SetTile(RasterTile tile, int x, int y);
    RasterTile? GetTile(int x, int y);
    void Clear();

    void SetPixelDirty(int x, int y);

    /// <summary>把一个方块矩形标脏.</summary>
    void SetWorldRegionDirty(int x, int y, int width, int height);

    int TileSizeX { get; }
    int TileSizeY { get; }

    int TilesX { get; }
    int TilesY { get; }
}
