using System;
using System.Threading;
using System.Threading.Tasks;
using Lua;
using Lua.Standard;
using Jint;
using TEdit.Terraria;

namespace TEdit5.Services;

/// <summary>
/// 用 Jint 执行脚本. 提供给脚本的最小 API 只做世界图格读写,
/// 与原生编辑共用同一份 Tile 数组, 执行完由调用方触发重绘.
/// </summary>
public static class ScriptRunner
{
    /// <summary>用 Lua 执行脚本, API 与 JS 引擎保持一致.</summary>
    public static string RunLua(string? code, World? world, Action? afterChange = null, Func<string>? selection = null, Action<int, int, int, int>? setSelection = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "脚本为空";
        }

        if (world == null)
        {
            return "没有打开的世界, 无法执行";
        }

        try
        {
            var state = Lua.LuaState.Create();
            state.OpenStandardLibraries();
            state.Environment["os"] = default(Lua.LuaValue);
            state.Environment["io"] = default(Lua.LuaValue);
            state.Environment["dofile"] = default(Lua.LuaValue);
            state.Environment["loadfile"] = default(Lua.LuaValue);
            state.Environment["package"] = default(Lua.LuaValue);

            state.Environment["tileWidth"] = world.Tiles.GetLength(0);
            state.Environment["tileHeight"] = world.Tiles.GetLength(1);

            state.Environment["log"] = new Lua.LuaFunction((ctx, _) =>
            {
                string text;
                try
                {
                    text = ctx.GetArgument<string>(0);
                }
                catch
                {
                    // Lua 的数字不是字符串, 传数字时退化成 double 再格式化.
                    text = ctx.GetArgument<double>(0).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                LogManager.Info($"[Lua] {text}");
                return new ValueTask<int>(0);
            });

            state.Environment["setTile"] = new Lua.LuaFunction((ctx, _) =>
            {
                int x = (int)ctx.GetArgument<double>(0);
                int y = (int)ctx.GetArgument<double>(1);
                int type = (int)ctx.GetArgument<double>(2);
                WriteTile(world, x, y, type);
                afterChange?.Invoke();
                return new ValueTask<int>(0);
            });

            state.Environment["fill"] = new Lua.LuaFunction((ctx, _) =>
            {
                Fill(
                    world,
                    (int)ctx.GetArgument<double>(0),
                    (int)ctx.GetArgument<double>(1),
                    (int)ctx.GetArgument<double>(2),
                    (int)ctx.GetArgument<double>(3),
                    (int)ctx.GetArgument<double>(4));
                afterChange?.Invoke();
                return new ValueTask<int>(0);
            });

            state.Environment["getTile"] = new Lua.LuaFunction((ctx, _) =>
            {
                int x = (int)ctx.GetArgument<double>(0);
                int y = (int)ctx.GetArgument<double>(1);
                ctx.Return((double)ReadTile(world, x, y));
                return new ValueTask<int>(1);
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            state.Environment["setWall"] = new Lua.LuaFunction((ctx, _) =>
            {
                WriteWall(world, (int)ctx.GetArgument<double>(0), (int)ctx.GetArgument<double>(1), (int)ctx.GetArgument<double>(2));
                afterChange?.Invoke();
                return new ValueTask<int>(0);
            });

            state.Environment["fillWall"] = new Lua.LuaFunction((ctx, _) =>
            {
                FillWall(
                    world,
                    (int)ctx.GetArgument<double>(0),
                    (int)ctx.GetArgument<double>(1),
                    (int)ctx.GetArgument<double>(2),
                    (int)ctx.GetArgument<double>(3),
                    (int)ctx.GetArgument<double>(4));
                afterChange?.Invoke();
                return new ValueTask<int>(0);
            });

            state.Environment["getWall"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return((double)ReadWall(world, (int)ctx.GetArgument<double>(0), (int)ctx.GetArgument<double>(1)));
                return new ValueTask<int>(1);
            });

            state.Environment["getNpcCount"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return((double)world.NPCs.Count);
                return new ValueTask<int>(1);
            });

            state.Environment["getNpcName"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return(NpcName(world, (int)ctx.GetArgument<double>(0)));
                return new ValueTask<int>(1);
            });

            state.Environment["setSelection"] = new Lua.LuaFunction((ctx, _) =>
            {
                setSelection?.Invoke(
                    (int)ctx.GetArgument<double>(0),
                    (int)ctx.GetArgument<double>(1),
                    (int)ctx.GetArgument<double>(2),
                    (int)ctx.GetArgument<double>(3));
                return new ValueTask<int>(0);
            });

            state.Environment["getSelection"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return(selection?.Invoke() ?? string.Empty);
                return new ValueTask<int>(1);
            });

            state.Environment["distance"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return(GeometryDistance(
                    (int)ctx.GetArgument<double>(0),
                    (int)ctx.GetArgument<double>(1),
                    (int)ctx.GetArgument<double>(2),
                    (int)ctx.GetArgument<double>(3)));
                return new ValueTask<int>(1);
            });

            state.Environment["rectContains"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return(RectContains(
                    (int)ctx.GetArgument<double>(0),
                    (int)ctx.GetArgument<double>(1),
                    (int)ctx.GetArgument<double>(2),
                    (int)ctx.GetArgument<double>(3),
                    (int)ctx.GetArgument<double>(4),
                    (int)ctx.GetArgument<double>(5)));
                return new ValueTask<int>(1);
            });

            state.Environment["getWorldTitle"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return(WorldTitle(world));
                return new ValueTask<int>(1);
            });

            state.Environment["getWorldTime"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return(WorldTime(world));
                return new ValueTask<int>(1);
            });

            state.Environment["removeNpc"] = new Lua.LuaFunction((ctx, _) =>
            {
                RemoveNpc(world, (int)ctx.GetArgument<double>(0));
                afterChange?.Invoke();
                return new ValueTask<int>(0);
            });

            state.Environment["getNpcPosition"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return(NpcPosition(world, (int)ctx.GetArgument<double>(0)));
                return new ValueTask<int>(1);
            });

            state.Environment["addNpc"] = new Lua.LuaFunction((ctx, _) =>
            {
                AddNpc(
                    world,
                    ctx.GetArgument<string>(0),
                    (int)ctx.GetArgument<double>(1),
                    (float)ctx.GetArgument<double>(2),
                    (float)ctx.GetArgument<double>(3));
                afterChange?.Invoke();
                return new ValueTask<int>(0);
            });

            state.Environment["getChestCount"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return((double)world.Chests.Count);
                return new ValueTask<int>(1);
            });

            state.Environment["getChestItemCount"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return((double)ChestItemCount(world, (int)ctx.GetArgument<double>(0)));
                return new ValueTask<int>(1);
            });

            state.Environment["setChestItem"] = new Lua.LuaFunction((ctx, _) =>
            {
                SetChestItem(
                    world,
                    (int)ctx.GetArgument<double>(0),
                    (int)ctx.GetArgument<double>(1),
                    (int)ctx.GetArgument<double>(2),
                    (int)ctx.GetArgument<double>(3));
                afterChange?.Invoke();
                return new ValueTask<int>(0);
            });

            state.Environment["getChestItemId"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return((double)ChestItemId(
                    world,
                    (int)ctx.GetArgument<double>(0),
                    (int)ctx.GetArgument<double>(1)));
                return new ValueTask<int>(1);
            });

            state.Environment["getSignCount"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return((double)world.Signs.Count);
                return new ValueTask<int>(1);
            });

            state.Environment["getSignText"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return(ReadSignText(world, (int)ctx.GetArgument<double>(0)));
                return new ValueTask<int>(1);
            });

            state.Environment["addSign"] = new Lua.LuaFunction((ctx, _) =>
            {
                world.Signs.Add(new Sign(
                    (int)ctx.GetArgument<double>(0),
                    (int)ctx.GetArgument<double>(1),
                    ctx.GetArgument<string>(2)));
                afterChange?.Invoke();
                return new ValueTask<int>(0);
            });

            state.Environment["setLiquid"] = new Lua.LuaFunction((ctx, _) =>
            {
                SetLiquid(
                    world,
                    (int)ctx.GetArgument<double>(0),
                    (int)ctx.GetArgument<double>(1),
                    (int)ctx.GetArgument<double>(2),
                    (int)ctx.GetArgument<double>(3));
                afterChange?.Invoke();
                return new ValueTask<int>(0);
            });

            state.Environment["countWall"] = new Lua.LuaFunction((ctx, _) =>
            {
                ctx.Return((double)CountWall(world, (int)ctx.GetArgument<double>(0)));
                return new ValueTask<int>(1);
            });

            state.Environment["countTile"] = new Lua.LuaFunction((ctx, _) =>
            {
                int type = (int)ctx.GetArgument<double>(0);
                ctx.Return((double)CountTile(world, type));
                return new ValueTask<int>(1);
            });

            state.DoStringAsync(code, cancellationToken: cts.Token).AsTask().GetAwaiter().GetResult();
            LogManager.Info("Lua 脚本执行完成");
            return "执行完成";
        }
        catch (Exception ex)
        {
            LogManager.Error($"Lua 脚本执行失败: {ex.Message}");
            return $"执行失败: {ex.Message}";
        }
    }

    /// <summary>写入墙体.</summary>
    private static void WriteWall(World world, int x, int y, int wall)
    {
        if (x < 0 || y < 0 || x >= world.Tiles.GetLength(0) || y >= world.Tiles.GetLength(1))
        {
            return;
        }

        var tile = world.Tiles[x, y];
        tile.Wall = (byte)Math.Clamp(wall, 0, 255);
        world.Tiles[x, y] = tile;
    }

    /// <summary>读墙体.</summary>
    private static int ReadWall(World world, int x, int y)
    {
        if (x < 0 || y < 0 || x >= world.Tiles.GetLength(0) || y >= world.Tiles.GetLength(1))
        {
            return -1;
        }

        return world.Tiles[x, y].Wall;
    }

    /// <summary>读 NPC 名字, 索引无效时返回空串.</summary>
    private static string NpcName(World world, int index)
    {
        return index >= 0 && index < world.NPCs.Count ? world.NPCs[index].Name ?? string.Empty : string.Empty;
    }

    /// <summary>两点距离.</summary>
    private static double GeometryDistance(int x1, int y1, int x2, int y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>点是否落在矩形内.</summary>
    private static bool RectContains(int x, int y, int width, int height, int px, int py)
    {
        return px >= x && px < x + width && py >= y && py < y + height;
    }

    /// <summary>世界标题, 没有时返回空串.</summary>
    private static string WorldTitle(World world) => world.Title ?? string.Empty;

    /// <summary>世界时间, 0 到 1, 0 是凌晨.</summary>
    private static double WorldTime(World world) => world.Time;

    /// <summary>删除指定序号的 NPC.</summary>
    private static void RemoveNpc(World world, int index)
    {
        if (index >= 0 && index < world.NPCs.Count)
        {
            world.NPCs.RemoveAt(index);
        }
    }

    /// <summary>读 NPC 位置, 索引无效时返回空串.</summary>
    private static string NpcPosition(World world, int index)
    {
        if (index < 0 || index >= world.NPCs.Count)
        {
            return string.Empty;
        }

        var position = world.NPCs[index].Position;
        if (position == null)
        {
            return string.Empty;
        }

        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", position.X, position.Y);
    }

    /// <summary>新增一个 NPC.</summary>
    private static void AddNpc(World world, string name, int spriteId, float x, float y)
    {
        world.NPCs.Add(new NPC
        {
            Name = name,
            SpriteId = spriteId,
            Position = new Vector2FloatObservable(x, y),
            Home = new Vector2Int32Observable((int)x, (int)y),
            DisplayName = name,
        });
    }

    /// <summary>往箱子某格写物品, 格子不足时补齐.</summary>
    private static void SetChestItem(World world, int chest, int slot, int id, int stack)
    {
        if (chest < 0 || chest >= world.Chests.Count || slot < 0)
        {
            return;
        }

        var items = world.Chests[chest].Items;
        while (items.Count <= slot)
        {
            items.Add(new Item());
        }

        var item = items[slot];
        item.NetId = Math.Clamp(id, 0, 6000);
        item.StackSize = Math.Clamp(stack, 0, 9999);
    }

    /// <summary>读箱子的物品数, 索引无效时返回 0.</summary>
    private static int ChestItemCount(World world, int index)
    {
        return index >= 0 && index < world.Chests.Count ? world.Chests[index].Items.Count : 0;
    }

    /// <summary>读箱子里指定物品的编号, 越界返回 0.</summary>
    private static int ChestItemId(World world, int chest, int slot)
    {
        if (chest < 0 || chest >= world.Chests.Count)
        {
            return 0;
        }

        var items = world.Chests[chest].Items;
        return slot >= 0 && slot < items.Count ? items[slot].NetId : 0;
    }

    /// <summary>读告示牌文本, 索引无效时返回空串.</summary>
    private static string ReadSignText(World world, int index)
    {
        return index >= 0 && index < world.Signs.Count ? world.Signs[index].Text : string.Empty;
    }

    /// <summary>写液体, amount 为 0 时清空.</summary>
    private static void SetLiquid(World world, int x, int y, int amount, int type)
    {
        if (x < 0 || y < 0 || x >= world.Tiles.GetLength(0) || y >= world.Tiles.GetLength(1))
        {
            return;
        }

        var tile = world.Tiles[x, y];
        tile.LiquidAmount = (byte)Math.Clamp(amount, 0, 255);
        tile.LiquidType = tile.LiquidAmount == 0
            ? LiquidType.None
            : (LiquidType)(byte)Math.Clamp(type, 1, 3);
        world.Tiles[x, y] = tile;
    }

    /// <summary>统计某种墙体的数量.</summary>
    private static int CountWall(World world, int wall)
    {
        int count = 0;
        for (int ty = 0; ty < world.Tiles.GetLength(1); ty++)
        {
            for (int tx = 0; tx < world.Tiles.GetLength(0); tx++)
            {
                if (world.Tiles[tx, ty].Wall == wall)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>批量填墙体.</summary>
    private static void FillWall(World world, int x, int y, int width, int height, int wall)
    {
        int changed = 0;
        for (int ty = y; ty < y + height && ty < world.Tiles.GetLength(1); ty++)
        {
            for (int tx = x; tx < x + width && tx < world.Tiles.GetLength(0); tx++)
            {
                if (tx < 0 || ty < 0)
                {
                    continue;
                }

                WriteWall(world, tx, ty, wall);
                changed++;
            }
        }

        LogManager.Info($"[脚本] fillWall 改了 {changed} 格");
    }

    /// <summary>统计某类方块在世界里的数量.</summary>
    private static int CountTile(World world, int type)
    {
        int count = 0;
        int width = world.Tiles.GetLength(0);
        int height = world.Tiles.GetLength(1);
        for (int ty = 0; ty < height; ty++)
        {
            for (int tx = 0; tx < width; tx++)
            {
                var tile = world.Tiles[tx, ty];
                if (type == 0 ? !tile.IsActive : tile.IsActive && tile.Type == type)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>写入一格.</summary>
    private static void WriteTile(World world, int x, int y, int type)
    {
        if (x < 0 || y < 0 || x >= world.Tiles.GetLength(0) || y >= world.Tiles.GetLength(1))
        {
            return;
        }

        var tile = world.Tiles[x, y];
        if (type <= 0)
        {
            tile.IsActive = false;
        }
        else
        {
            tile.IsActive = true;
            tile.Type = (ushort)type;
        }

        world.Tiles[x, y] = tile;
    }

    /// <summary>读一格.</summary>
    private static int ReadTile(World world, int x, int y)
    {
        if (x < 0 || y < 0 || x >= world.Tiles.GetLength(0) || y >= world.Tiles.GetLength(1))
        {
            return -1;
        }

        var tile = world.Tiles[x, y];
        return tile.IsActive ? tile.Type : 0;
    }

    /// <summary>填充矩形.</summary>
    private static void Fill(World world, int x, int y, int width, int height, int type)
    {
        int changed = 0;
        for (int ty = y; ty < y + height && ty < world.Tiles.GetLength(1); ty++)
        {
            for (int tx = x; tx < x + width && tx < world.Tiles.GetLength(0); tx++)
            {
                if (tx < 0 || ty < 0)
                {
                    continue;
                }

                WriteTile(world, tx, ty, type);
                changed++;
            }
        }

        LogManager.Info($"[Lua] fill 改了 {changed} 格");
    }

    /// <summary>执行脚本并返回最后一行的结果, 出错时返回错误描述.</summary>
    public static string Run(string? code, World? world, Action? afterChange = null, Func<string>? selection = null, Action<int, int, int, int>? setSelection = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "脚本为空";
        }

        if (world == null)
        {
            return "没有打开的世界, 无法执行";
        }

        var engine = new Engine(options => options.LimitRecursion(64).TimeoutInterval(TimeSpan.FromSeconds(5)));

        engine.SetValue("log", new Action<object?>(value => LogManager.Info($"[脚本] {value}")));

        engine.SetValue("setTile", new Action<int, int, int>((x, y, type) =>
        {
            if (x < 0 || y < 0 || x >= world.Tiles.GetLength(0) || y >= world.Tiles.GetLength(1))
            {
                return;
            }

            var tile = world.Tiles[x, y];
            if (type <= 0)
            {
                tile.IsActive = false;
            }
            else
            {
                tile.IsActive = true;
                tile.Type = (ushort)type;
            }

            // Tile 是值类型, 改完必须写回数组, 否则只改了副本.
            world.Tiles[x, y] = tile;
            afterChange?.Invoke();
        }));

        engine.SetValue("fill", new Action<int, int, int, int, int>((x, y, width, height, type) =>
        {
            int maxX = world.Tiles.GetLength(0);
            int maxY = world.Tiles.GetLength(1);
            int changed = 0;
            for (int ty = y; ty < y + height && ty < maxY; ty++)
            {
                for (int tx = x; tx < x + width && tx < maxX; tx++)
                {
                    if (tx < 0 || ty < 0)
                    {
                        continue;
                    }

                    var cell = world.Tiles[tx, ty];
                    if (type <= 0)
                    {
                        cell.IsActive = false;
                    }
                    else
                    {
                        cell.IsActive = true;
                        cell.Type = (ushort)type;
                    }

                    world.Tiles[tx, ty] = cell;
                    changed++;
                }
            }

            LogManager.Info($"[脚本] fill 改了 {changed} 格");
            afterChange?.Invoke();
        }));

        engine.SetValue("getTile", new Func<int, int, int>((x, y) =>
        {
            if (x < 0 || y < 0 || x >= world.Tiles.GetLength(0) || y >= world.Tiles.GetLength(1))
            {
                return -1;
            }

            var tile = world.Tiles[x, y];
            return tile.IsActive ? tile.Type : 0;
        }));

        engine.SetValue("setWall", new Action<int, int, int>((x, y, wall) =>
        {
            WriteWall(world, x, y, wall);
            afterChange?.Invoke();
        }));

        engine.SetValue("fillWall", new Action<int, int, int, int, int>((x, y, width, height, wall) =>
        {
            FillWall(world, x, y, width, height, wall);
            afterChange?.Invoke();
        }));

        engine.SetValue("getWall", new Func<int, int, int>((x, y) => ReadWall(world, x, y)));

        engine.SetValue("getNpcCount", new Func<int>(() => world.NPCs.Count));

        engine.SetValue("getNpcName", new Func<int, string>(index => NpcName(world, index)));

        engine.SetValue("setSelection", new Action<int, int, int, int>((x, y, width, height) => setSelection?.Invoke(x, y, width, height)));

        engine.SetValue("getSelection", new Func<string>(() => selection?.Invoke() ?? string.Empty));

        engine.SetValue("distance", new Func<int, int, int, int, double>((x1, y1, x2, y2) => GeometryDistance(x1, y1, x2, y2)));

        engine.SetValue("rectContains", new Func<int, int, int, int, int, int, bool>((x, y, width, height, px, py) => RectContains(x, y, width, height, px, py)));

        engine.SetValue("getWorldTitle", new Func<string>(() => WorldTitle(world)));

        engine.SetValue("getWorldTime", new Func<double>(() => WorldTime(world)));

        engine.SetValue("removeNpc", new Action<int>(index =>
        {
            RemoveNpc(world, index);
            afterChange?.Invoke();
        }));

        engine.SetValue("getNpcPosition", new Func<int, string>(index => NpcPosition(world, index)));

        engine.SetValue("addNpc", new Action<string, int, float, float>((name, spriteId, x, y) =>
        {
            AddNpc(world, name, spriteId, x, y);
            afterChange?.Invoke();
        }));

        engine.SetValue("getChestCount", new Func<int>(() => world.Chests.Count));

        engine.SetValue("getChestItemCount", new Func<int, int>(index => ChestItemCount(world, index)));

        engine.SetValue("setChestItem", new Action<int, int, int, int>((chest, slot, id, stack) =>
        {
            SetChestItem(world, chest, slot, id, stack);
            afterChange?.Invoke();
        }));

        engine.SetValue("getChestItemId", new Func<int, int, int>((chest, slot) => ChestItemId(world, chest, slot)));

        engine.SetValue("getSignCount", new Func<int>(() => world.Signs.Count));

        engine.SetValue("getSignText", new Func<int, string>(index => ReadSignText(world, index)));

        engine.SetValue("addSign", new Action<int, int, string>((x, y, text) =>
        {
            world.Signs.Add(new Sign(x, y, text));
            afterChange?.Invoke();
        }));

        engine.SetValue("setLiquid", new Action<int, int, int, int>((x, y, amount, type) =>
        {
            SetLiquid(world, x, y, amount, type);
            afterChange?.Invoke();
        }));

        engine.SetValue("countWall", new Func<int, int>(wall => CountWall(world, wall)));

        engine.SetValue("countTile", new Func<int, int>(type => CountTile(world, type)));

        engine.SetValue("tileWidth", world.Tiles.GetLength(0));
        engine.SetValue("tileHeight", world.Tiles.GetLength(1));

        try
        {
            var value = engine.Evaluate(code);
            LogManager.Info("脚本执行完成");
            return value.IsUndefined() || value.IsNull() ? "执行完成" : value.ToString();
        }
        catch (Exception ex)
        {
            LogManager.Error($"脚本执行失败: {ex.Message}");
            return $"执行失败: {ex.Message}";
        }
    }
}
