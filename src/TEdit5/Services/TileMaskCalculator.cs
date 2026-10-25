using System;
using TEdit.Terraria;

namespace TEdit5.Services;

/// <summary>
/// 按四邻与四角算出拼接掩码, 供 <see cref="BlendRules"/> 选出边角帧.
/// 位定义与原版一致: 边 e=0x1, n=0x10, w=0x100, s=0x1000;
/// 角 ne=0x10000, nw=0x100000, sw=0x1000000, se=0x10000000.
/// </summary>
public static class TileMaskCalculator
{
    private const uint East = 0x00000001;
    private const uint North = 0x00000010;
    private const uint West = 0x00000100;
    private const uint South = 0x00001000;
    private const uint NorthEast = 0x00010000;
    private const uint NorthWest = 0x00100000;
    private const uint SouthWest = 0x01000000;
    private const uint SouthEast = 0x10000000;

    /// <summary>四邻与四角是否与中心格同种方块.</summary>
    public static uint ComputeSameStyle(World world, int x, int y)
    {
        var tile = world.Tiles[x, y];
        if (!tile.IsActive)
        {
            return 0;
        }

        int type = tile.Type;
        uint mask = 0;

        if (SameType(world, x + 1, y, type))
        {
            mask |= East;
        }

        if (SameType(world, x, y - 1, type))
        {
            mask |= North;
        }

        if (SameType(world, x - 1, y, type))
        {
            mask |= West;
        }

        if (SameType(world, x, y + 1, type))
        {
            mask |= South;
        }

        if (SameType(world, x + 1, y - 1, type))
        {
            mask |= NorthEast;
        }

        if (SameType(world, x - 1, y - 1, type))
        {
            mask |= NorthWest;
        }

        if (SameType(world, x - 1, y + 1, type))
        {
            mask |= SouthWest;
        }

        if (SameType(world, x + 1, y + 1, type))
        {
            mask |= SouthEast;
        }

        return mask;
    }

    /// <summary>四邻与四角是否是可以合并的另一种方块.</summary>
    public static uint ComputeMergeMask(World world, int x, int y, int mergeWith)
    {
        if (mergeWith < 0)
        {
            return 0;
        }

        uint mask = 0;
        if (IsType(world, x + 1, y, mergeWith))
        {
            mask |= East;
        }

        if (IsType(world, x, y - 1, mergeWith))
        {
            mask |= North;
        }

        if (IsType(world, x - 1, y, mergeWith))
        {
            mask |= West;
        }

        if (IsType(world, x, y + 1, mergeWith))
        {
            mask |= South;
        }

        if (IsType(world, x + 1, y - 1, mergeWith))
        {
            mask |= NorthEast;
        }

        if (IsType(world, x - 1, y - 1, mergeWith))
        {
            mask |= NorthWest;
        }

        if (IsType(world, x - 1, y + 1, mergeWith))
        {
            mask |= SouthWest;
        }

        if (IsType(world, x + 1, y + 1, mergeWith))
        {
            mask |= SouthEast;
        }

        return mask;
    }

    /// <summary>
    /// 计算某一格的图集帧左上角像素坐标; 无法判定时返回 null, 由调用方回落默认帧.
    /// </summary>
    public static (int X, int Y)? ResolveFrame(World world, int x, int y, int variant)
    {
        var tile = world.Tiles[x, y];
        if (!tile.IsActive || tile.Type >= WorldConfiguration.TileProperties.Count)
        {
            return null;
        }

        var property = WorldConfiguration.TileProperties[tile.Type];
        uint sameStyle = ComputeSameStyle(world, x, y);
        uint mergeMask = 0;
        int strictness = 0;

        if (property.MergeWith.HasValue && property.MergeWith.Value > -1)
        {
            mergeMask = ComputeMergeMask(world, x, y, property.MergeWith.Value);
            strictness = 1;
        }

        if (property.IsGrass)
        {
            strictness = 2;
        }

        var uv = BlendRules.Instance.GetUVForMasks(sameStyle, mergeMask, strictness, variant);
        return (uv.X * TileAtlas.FrameSize, uv.Y * TileAtlas.FrameSize);
    }

    private static bool SameType(World world, int x, int y, int type)
    {
        if (x < 0 || y < 0 || x >= world.Tiles.GetLength(0) || y >= world.Tiles.GetLength(1))
        {
            return false;
        }

        var neighbor = world.Tiles[x, y];
        return neighbor.IsActive && neighbor.Type == type;
    }

    private static bool IsType(World world, int x, int y, int type)
    {
        if (x < 0 || y < 0 || x >= world.Tiles.GetLength(0) || y >= world.Tiles.GetLength(1))
        {
            return false;
        }

        var neighbor = world.Tiles[x, y];
        return neighbor.IsActive && neighbor.Type == type;
    }
}
