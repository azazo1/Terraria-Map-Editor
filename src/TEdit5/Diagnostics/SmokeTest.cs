using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using TEdit.Editor;
using TEdit.Editor.Undo;
using TEdit.Terraria;
using TEdit5.Editor.Undo;
using TEdit5.Services;

namespace TEdit5.Diagnostics;

/// <summary>
/// 无界面自检. 用命令行参数 --smoke-test 触发, 覆盖世界新建/保存/读取,
/// 方块绘制以及撤销重做. 用于在 CI runner 上验证核心编辑链路, 不依赖图形界面.
/// </summary>
public static class SmokeTest
{
    public static async Task<int> RunAsync()
    {
        LogManager.Initialize();

        string workDirectory = Path.Combine(Path.GetTempPath(), "tedit5-smoke");
        Directory.CreateDirectory(workDirectory);
        string worldPath = Path.Combine(workDirectory, "smoke.wld");
        string savedPath = Path.Combine(workDirectory, "smoke-saved.wld");

        if (File.Exists(worldPath))
        {
            File.Delete(worldPath);
        }

        if (File.Exists(savedPath))
        {
            File.Delete(savedPath);
        }

        try
        {
            Console.WriteLine("[1/22] 新建世界并保存");
            var world = new World(200, 200, "smoke", seed: 12345)
            {
                Version = WorldConfiguration.CompatibleVersion,
                Tiles = new Tile[200, 200],
            };

            await World.SaveAsync(world, worldPath);
            Expect(File.Exists(worldPath), "世界文件应当已经写出");

            Console.WriteLine("[2/22] 重新读取世界");
            var (loaded, loadError) = await World.LoadWorldAsync(worldPath);
            Expect(loaded != null, $"世界应当能重新读取: {loadError?.Message}");
            Expect(loaded!.TilesWide == 200 && loaded.TilesHigh == 200, "世界尺寸应当保持 200x200");

            Console.WriteLine("[3/22] 通过 WorldEditor 绘制方块");
            var picker = new TilePicker
            {
                Tile = 1,
                TileStyleActive = true,
                PaintMode = PaintMode.TileAndWall,
            };

            using var undo = new UndoManager(loaded);
            using var editor = new WorldEditor(
                picker,
                new TileMaskSettings(),
                loaded,
                new Selection(),
                undo,
                (_, _, _, _) => { });

            await editor.BeginOperationAsync();
            editor.PaintPixel(50, 50);
            await editor.EndOperationAsync();

            var painted = loaded.Tiles[50, 50];
            Expect(painted.IsActive, "绘制之后该位置应当有方块");

            Console.WriteLine("[4/22] 撤销");
            var undoCount = undo.Undo();
            Expect(undoCount.Count > 0, "撤销应当返回被恢复的方块列表");
            Expect(!loaded.Tiles[50, 50].IsActive, "撤销之后该位置应当恢复为空");

            Console.WriteLine("[5/22] 重做");
            var redoCount = undo.Redo();
            Expect(redoCount.Count > 0, "重做应当返回被恢复的方块列表");
            Expect(loaded.Tiles[50, 50].IsActive, "重做之后该位置应当再次有方块");

            Console.WriteLine("[6/22] 刷子成片涂抹");
            editor.Brush.Shape = BrushShape.Square;
            editor.Brush.Width = 3;
            editor.Brush.Height = 3;

            await editor.BeginOperationAsync();
            foreach (var point in editor.GetShapePoints(new TEdit.Geometry.Vector2Int32(60, 60)))
            {
                editor.PaintPixel(point.X, point.Y);
            }
            await editor.EndOperationAsync();

            Expect(loaded.Tiles[61, 61].IsActive, "刷子应当涂到中心周围的格子");
            undo.Undo();
            Expect(!loaded.Tiles[61, 61].IsActive, "刷子这一笔应当可以撤销");
            undo.Redo();

            Console.WriteLine("[7/22] 筛选器按方块编号生效");
            var filter = new TEdit5.Services.WorldFilter();
            var option = new TEdit5.Services.FilterOption(1, "石头", "方块");
            filter.TileOptions.Add(option);
            Expect(!filter.IsTileFiltered(option.Id), "默认不应过滤任何方块");
            option.IsSelected = true;
            Expect(filter.IsTileFiltered(option.Id), "勾选之后该方块应当被过滤");
            option.IsSelected = false;
            Expect(!filter.IsTileFiltered(option.Id), "取消勾选之后应当恢复显示");

            Console.WriteLine("[8/22] 调色板与物品数据已加载");
            Expect(TEdit.Terraria.WorldConfiguration.TileProperties.Count > 0, "方块表不应为空");
            Expect(TEdit.Terraria.WorldConfiguration.WallProperties.Count > 0, "墙体表不应为空");
            Expect(TEdit.Terraria.WorldConfiguration.ItemProperties.Count > 0, "物品表不应为空");

            Console.WriteLine("[9/22] 图标缓存缺文件时安全返回");
            Expect(TEdit5.Controls.ItemIconCache.Get(-1) == null, "不存在的图标应返回 null");
            Expect(TEdit5.Controls.ItemIconCache.Get(-1, "Tile") == null, "按前缀查询也应当安全");

            Console.WriteLine("[10/22] 图鉴星级文本按五格显示");
            var bestiaryEntry = new TEdit5.ViewModels.BestiaryEntry { BestiaryStars = 3 };
            Expect(TEdit5.Controls.StarRating.Clamp(3, 5) == 3, "三星应当保持三颗");
            bestiaryEntry.BestiaryStars = 9;
            Expect(TEdit5.Controls.StarRating.Clamp(7, 5) == 5, "超过五应当夹到五颗");

            Console.WriteLine("[11/22] 设置里的图标目录可用");
            var settings = TEdit5.Services.AppSettings.Load();
            Expect(!string.IsNullOrWhiteSpace(settings.TexturePath), "图标目录不应为空");
            Expect(System.IO.Path.IsPathRooted(settings.TexturePath), "图标目录应当是绝对路径");

            Console.WriteLine("[12/22] 玩家存档可写入并读回");
            var player = new TEdit.Terraria.Player.PlayerCharacter { Name = "SmokeTester", Difficulty = 2 };
            using (var playerStream = new System.IO.MemoryStream())
            {
                TEdit.Terraria.Player.PlayerFile.Save(playerStream, player);
                playerStream.Position = 0;
                var reloadedPlayer = TEdit.Terraria.Player.PlayerFile.Load(playerStream);
                Expect(reloadedPlayer.Name == "SmokeTester", "名字应当保持不变");
                Expect(reloadedPlayer.Difficulty == 2, "难度应当保持不变");
            }

            Console.WriteLine("[13/22] 玩家编辑器空态安全");
            var emptyPlayerEditor = new TEdit5.ViewModels.PlayerEditorViewModel();
            Expect(!emptyPlayerEditor.HasPlayer, "未打开文件时不应认为有玩家");
            Expect(emptyPlayerEditor.Name == "(未打开)", "未打开文件时名字应显示占位");
            Expect(emptyPlayerEditor.InventoryRows.Count == 0, "未打开文件时物品栏应为空");

            Console.WriteLine("[14/22] 配色输入校验");
            string tempPlayerPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"smoke-{Guid.NewGuid():N}.plr");
            try
            {
                TEdit.Terraria.Player.PlayerFile.Save(tempPlayerPath, new TEdit.Terraria.Player.PlayerCharacter { Name = "ColorTester" });
                var colorEditor = new TEdit5.ViewModels.PlayerEditorViewModel();
                colorEditor.Load(tempPlayerPath);
                Expect(colorEditor.HasPlayer, "临时玩家存档应当能打开");
                colorEditor.HairColorHex = "#112233";
                Expect(colorEditor.HairColorHex == "#112233", "合法色值应当写入");
                colorEditor.HairColorHex = "not-a-color";
                Expect(colorEditor.HairColorHex == "#112233", "非法色值不应改变原值");
                colorEditor.Name = "SavedTester";
                colorEditor.StatLifeMax = 400;
                string playerSavedPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"smoke-{Guid.NewGuid():N}.plr");
                try
                {
                    Expect(colorEditor.Save(playerSavedPath), "另存为应当成功");
                    var reopened = new TEdit5.ViewModels.PlayerEditorViewModel();
                    reopened.Load(playerSavedPath);
                    Expect(reopened.Name == "SavedTester", "改动应当能落盘");
                    Expect(reopened.HairColorHex == "#112233", "配色应当能落盘");
                    Expect(reopened.StatLifeMax == 400, "生命上限应当能落盘");
                }
                finally
                {
                    if (System.IO.File.Exists(playerSavedPath))
                    {
                        System.IO.File.Delete(playerSavedPath);
                    }
                }
            }
            finally
            {
                if (System.IO.File.Exists(tempPlayerPath))
                {
                    System.IO.File.Delete(tempPlayerPath);
                }
            }

            Console.WriteLine("[15/22] 玩家就地保存按路径可用");
            var noPathEditor = new TEdit5.ViewModels.PlayerEditorViewModel();
            Expect(!noPathEditor.SaveCurrent(), "没有打开文件时就地保存应当失败");
            Expect(string.IsNullOrEmpty(noPathEditor.CurrentPath), "没有打开文件时路径应当为空");

            Console.WriteLine("[16/22] 区域缩略图渲染");
            var thumb = TEdit5.Services.WorldThumbnailRenderer.Render(loaded, 100, 100, 32, 16);
            Expect(thumb != null, "缩略图不应为空");
            Expect(thumb!.Width == 32 && thumb.Height == 16, "缩略图尺寸应当与请求一致");
            thumb.Dispose();

            Console.WriteLine("[17/22] 脚本能改图格");
            string scriptResult = TEdit5.Services.ScriptRunner.Run("setTile(3, 4, 1); getTile(3, 4)", loaded);
            Expect(scriptResult == "1", $"脚本应当写入并读回方块编号, 实际: {scriptResult}");
            Expect(loaded.Tiles[3, 4].IsActive, "脚本写过的格子应当是活动的");
            TEdit5.Services.ScriptRunner.Run("fill(10, 10, 4, 3, 2)", loaded);
            int filled = 0;
            for (int ty = 10; ty < 13; ty++)
            {
                for (int tx = 10; tx < 14; tx++)
                {
                    if (loaded.Tiles[tx, ty].IsActive && loaded.Tiles[tx, ty].Type == 2)
                    {
                        filled++;
                    }
                }
            }

            Expect(filled == 12, $"批量填充应当涂满 12 格, 实际: {filled}");

            string scriptError = TEdit5.Services.ScriptRunner.Run("throw new Error('x')", loaded);
            Expect(scriptError.StartsWith("执行失败"), "脚本异常应当被捕获");

            Console.WriteLine("[18/22] Lua 引擎能改图格");
            string luaResult = TEdit5.Services.ScriptRunner.RunLua("setTile(7, 8, 3)", loaded);
            Expect(luaResult == "执行完成", $"Lua 应当正常执行, 实际: {luaResult}");
            Expect(loaded.Tiles[7, 8].IsActive && loaded.Tiles[7, 8].Type == 3, "Lua 写的格子应当生效");
            string luaError = TEdit5.Services.ScriptRunner.RunLua("error('boom')", loaded);
            Expect(luaError.StartsWith("执行失败"), "Lua 异常应当被捕获");

            Console.WriteLine("[19/22] 脚本能统计方块数量");
            string counted = TEdit5.Services.ScriptRunner.Run("countTile(1)", loaded);
            Expect(int.TryParse(counted, out int stoneCount) && stoneCount >= 1, $"统计应当返回正整数, 实际: {counted}");
            string luaCounted = TEdit5.Services.ScriptRunner.RunLua("log(countTile(3))", loaded);
            Expect(luaCounted == "执行完成", $"Lua 统计应当正常执行, 实际: {luaCounted}");

            Console.WriteLine("[20/22] 脚本能写墙体");
            string wallResult = TEdit5.Services.ScriptRunner.Run("setWall(5, 5, 4); getWall(5, 5)", loaded);
            Expect(wallResult == "4", $"墙体应当写入并读回, 实际: {wallResult}");
            TEdit5.Services.ScriptRunner.RunLua("setWall(6, 5, 7)", loaded);
            Expect(loaded.Tiles[6, 5].Wall == 7, "Lua 写的墙体应当生效");
            TEdit5.Services.ScriptRunner.Run("fillWall(20, 20, 3, 2, 5)", loaded);
            int wallFilled = 0;
            for (int wy = 20; wy < 22; wy++)
            {
                for (int wx = 20; wx < 23; wx++)
                {
                    if (loaded.Tiles[wx, wy].Wall == 5)
                    {
                        wallFilled++;
                    }
                }
            }

            Expect(wallFilled == 6, $"批量填墙应当涂满 6 格, 实际: {wallFilled}");
            string wallCount = TEdit5.Services.ScriptRunner.Run("countWall(5)", loaded);
            Expect(int.TryParse(wallCount, out int wallTotal) && wallTotal >= 6, $"统计墙体应当至少 6 格, 实际: {wallCount}");

            TEdit5.Services.ScriptRunner.Run("setLiquid(30, 30, 200, 2)", loaded);
            Expect(loaded.Tiles[30, 30].LiquidAmount == 200, "液体应当写到 200");
            Expect(loaded.Tiles[30, 30].LiquidType == TEdit.Terraria.LiquidType.Lava, "液体类型应当是岩浆");
            TEdit5.Services.ScriptRunner.RunLua("setLiquid(31, 30, 0, 0)", loaded);
            Expect(loaded.Tiles[31, 30].LiquidAmount == 0, "液体清空应当生效");

            int signsBefore = loaded.Signs.Count;
            TEdit5.Services.ScriptRunner.Run("addSign(40, 40, 'hello')", loaded);
            Expect(loaded.Signs.Count == signsBefore + 1, "脚本应当新增一个告示牌");
            Expect(loaded.Signs[^1].Text == "hello", "告示牌文本应当写入");
            string signCount = TEdit5.Services.ScriptRunner.Run("getSignCount()", loaded);
            Expect(int.TryParse(signCount, out int signTotal) && signTotal == signsBefore + 1, $"告示牌数量应当一致, 实际: {signCount}");

            string chestCount = TEdit5.Services.ScriptRunner.Run("getChestCount()", loaded);
            Expect(int.TryParse(chestCount, out int chestTotal) && chestTotal == loaded.Chests.Count,
                $"箱子数量应当一致, 实际: {chestCount}");

            loaded.Chests.Add(new TEdit.Terraria.Chest(12, 12));
            TEdit5.Services.ScriptRunner.Run("setChestItem(0, 0, 50, 3)", loaded);
            Expect(loaded.Chests[0].Items.Count >= 1, "箱子应当能放进物品");
            Expect(loaded.Chests[0].Items[0].NetId == 50, "物品编号应当写入");
            Expect(loaded.Chests[0].Items[0].StackSize == 3, "物品数量应当写入");
            string chestSlot = TEdit5.Services.ScriptRunner.Run("getChestItemId(0, 0)", loaded);
            Expect(chestSlot == "50", $"箱子物品编号应当读回 50, 实际: {chestSlot}");

            string npcBefore = TEdit5.Services.ScriptRunner.Run("getNpcCount()", loaded);
            Expect(int.TryParse(npcBefore, out int npcCountBefore) && npcCountBefore == loaded.NPCs.Count,
                $"NPC 数量应当一致, 实际: {npcBefore}");
            TEdit5.Services.ScriptRunner.Run("addNpc('guide', 22, 100.5, 200.5)", loaded);
            Expect(loaded.NPCs.Count == npcCountBefore + 1, "脚本应当新增一个 NPC");
            Expect(loaded.NPCs[^1].Name == "guide", "NPC 名字应当写入");
            string npcName = TEdit5.Services.ScriptRunner.Run("getNpcName(0)", loaded);
            Expect(npcName == "guide", $"NPC 名字应当能读回, 实际: {npcName}");
            string npcPos = TEdit5.Services.ScriptRunner.Run("getNpcPosition(0)", loaded);
            Expect(npcPos == "100.5,200.5", $"NPC 坐标应当能读回, 实际: {npcPos}");
            TEdit5.Services.ScriptRunner.Run("removeNpc(0)", loaded);
            Expect(loaded.NPCs.Count == npcCountBefore, "删除 NPC 应当生效");

            string worldTitle = TEdit5.Services.ScriptRunner.Run("getWorldTitle()", loaded);
            Expect(worldTitle == (loaded.Title ?? string.Empty), $"世界标题应当一致, 实际: {worldTitle}");
            string worldTime = TEdit5.Services.ScriptRunner.Run("getWorldTime()", loaded);
            Expect(double.TryParse(worldTime, out double parsedTime) && parsedTime >= 0 && parsedTime <= 1,
                $"世界时间应当在 0 到 1 之间, 实际: {worldTime}");

            string noSelection = TEdit5.Services.ScriptRunner.Run("getSelection()", loaded);
            Expect(noSelection.Length == 0, "无选区时应当返回空串");
            string selectionEcho = TEdit5.Services.ScriptRunner.Run("getSelection()", loaded, null, () => "1,2,3,4");
            Expect(selectionEcho == "1,2,3,4", $"选区应当能透传, 实际: {selectionEcho}");

            int selX = 0, selY = 0, selW = 0, selH = 0;
            TEdit5.Services.ScriptRunner.Run("setSelection(7, 8, 9, 10)", loaded, null, null, (x, y, w, h) =>
            {
                selX = x;
                selY = y;
                selW = w;
                selH = h;
            });
            Expect(selX == 7 && selY == 8 && selW == 9 && selH == 10,
                $"设定选区应当透传, 实际: {selX},{selY},{selW},{selH}");

            string dist = TEdit5.Services.ScriptRunner.Run("distance(0, 0, 3, 4)", loaded);
            Expect(dist == "5", $"距离应当是 5, 实际: {dist}");
            string inside = TEdit5.Services.ScriptRunner.Run("rectContains(10, 10, 5, 5, 12, 12)", loaded);
            Expect(inside == "true", $"点应当在矩形内, 实际: {inside}");
            string outside = TEdit5.Services.ScriptRunner.Run("rectContains(10, 10, 5, 5, 20, 20)", loaded);
            Expect(outside == "false", $"点应当在矩形外, 实际: {outside}");

            var previewPlayer = new TEdit.Terraria.Player.PlayerCharacter();
            byte[]? previewPng = TEdit5.Services.PlayerPreviewRenderer.RenderPng(previewPlayer);
            Expect(previewPng != null && previewPng.Length > 100, "玩家预览图不应为空");
            Expect(TEdit5.Services.PlayerPreviewRenderer.RenderPng(null) == null, "空角色应当返回空预览");

            var starPoints = TEdit5.Controls.StarRating.BuildStarPoints(new Avalonia.Point(0, 0), 10);
            Expect(starPoints.Length == 10, $"五角星应当是 10 个顶点, 实际: {starPoints.Length}");
            double outerDistance = Math.Sqrt(starPoints[0].X * starPoints[0].X + starPoints[0].Y * starPoints[0].Y);
            double innerDistance = Math.Sqrt(starPoints[1].X * starPoints[1].X + starPoints[1].Y * starPoints[1].Y);
            Expect(Math.Abs(outerDistance - 10) < 0.001, $"外顶点半径应当是 10, 实际: {outerDistance}");
            Expect(Math.Abs(innerDistance - 3.82) < 0.001, $"内顶点半径应当是 3.82, 实际: {innerDistance}");

            var tileAtlas = TEdit5.Services.TileAtlas.GetTile(0);
            Expect(tileAtlas != null, "方块图集应当能加载");
            Expect(tileAtlas!.Width == 288 && tileAtlas.Height == 270, $"方块 0 图集应当是 288x270, 实际: {tileAtlas.Width}x{tileAtlas.Height}");
            var wallAtlas = TEdit5.Services.TileAtlas.GetWall(1);
            Expect(wallAtlas != null, "墙体图集应当能加载");
            Expect(TEdit5.Services.TileAtlas.Has("Tiles", 99999) == false, "不存在的图集应当返回空");
            var average = TEdit5.Services.TileAtlas.GetAverageColor("Tiles", 1);
            Expect(average != null && average.Value.Alpha == 255, "图集平均色应当是不透明的");
            Expect(TEdit5.Services.TileAtlas.GetAverageColor("Tiles", 99999) == null, "不存在的图集没有平均色");

            int textureCols = 4, textureRows = 4, tilePixels = 16;
            using (var textured = new SkiaSharp.SKBitmap(textureCols * tilePixels, textureRows * tilePixels))
            using (var canvas = new SkiaSharp.SKCanvas(textured))
            {
                canvas.Clear(SkiaSharp.SKColors.Transparent);
                loaded.Tiles[5, 5] = new TEdit.Terraria.Tile { Type = 1, IsActive = true };
                TEdit5.Services.TileTextureRenderer.DrawRegion(
                    canvas, loaded, 5, 5, 1, 1, tilePixels, SkiaSharp.SKColors.Magenta);
                var drawn = textured.GetPixel(8, 8);
                Expect(drawn.Alpha > 0, "纹理渲染不应留下透明格");
                var atlas1 = TEdit5.Services.TileAtlas.GetTile(1)!;
                bool matched = false;
                for (int fy = 0; fy + 16 <= atlas1.Height && !matched; fy += 16)
                {
                    for (int fx = 0; fx + 16 <= atlas1.Width && !matched; fx += 16)
                    {
                        var sample = atlas1.GetPixel(fx + 8, fy + 8);
                        matched = sample.Red == drawn.Red && sample.Green == drawn.Green && sample.Blue == drawn.Blue;
                    }
                }

                Expect(matched, $"纹理渲染的像素应当来自图集, 实际: {drawn}");
            }

            using (var zoomed = new SkiaSharp.SKBitmap(32, 32))
            using (var zoomCanvas = new SkiaSharp.SKCanvas(zoomed))
            {
                zoomCanvas.Clear(SkiaSharp.SKColors.Transparent);
                loaded.Tiles[6, 6] = new TEdit.Terraria.Tile { Type = 1, IsActive = true };
                TEdit5.Services.TileTextureRenderer.DrawRegion(
                    zoomCanvas, loaded, 6, 6, 2, 2, 16, SkiaSharp.SKColors.Magenta);
                var big = zoomed.GetPixel(8, 8);
                Expect(big.Alpha > 0, "放大绘制应当填满格子");
            }

            Expect(TEdit5.Services.TileTextureRenderer.SelectVariant(0, 0, 1) == 0, "单列图集应当只有零号变体");
            int variantA = TEdit5.Services.TileTextureRenderer.SelectVariant(10, 20, 3);
            int variantB = TEdit5.Services.TileTextureRenderer.SelectVariant(10, 20, 3);
            Expect(variantA == variantB, "同一坐标的变体应当稳定");
            Expect(variantA >= 0 && variantA < 3, $"变体应当落在图集列范围内, 实际: {variantA}");

            // 孤立一格: 四邻皆空, 掩码应当为 0.
            loaded.Tiles[50, 50] = new TEdit.Terraria.Tile { Type = 1, IsActive = true };
            uint emptyMask = TEdit5.Services.TileMaskCalculator.ComputeSameStyle(loaded, 50, 50);
            Expect(emptyMask == 0, $"孤立方块的掩码应当是 0, 实际: 0x{emptyMask:X}");
            // 右侧与下方各补一格: 应当带 East 与 South 位.
            loaded.Tiles[51, 50] = new TEdit.Terraria.Tile { Type = 1, IsActive = true };
            loaded.Tiles[50, 51] = new TEdit.Terraria.Tile { Type = 1, IsActive = true };
            uint connectedMask = TEdit5.Services.TileMaskCalculator.ComputeSameStyle(loaded, 50, 50);
            Expect((connectedMask & 0x1) != 0, "右侧同种方块应当置 East 位");
            Expect((connectedMask & 0x1000) != 0, "下方同种方块应当置 South 位");
            var frame = TEdit5.Services.TileMaskCalculator.ResolveFrame(loaded, 50, 50, 0);
            Expect(frame.HasValue, "连接状态下应当能算出拼接帧");

            Expect(TEdit5.Controls.ItemIconCache.HasBuiltin("Item", 1), "物品 1 的内置图标应当存在");
            Expect(TEdit5.Controls.ItemIconCache.HasBuiltin("NPC", 1), "NPC 1 的内置图标应当存在");
            Expect(TEdit5.Controls.ItemIconCache.HasBuiltin("Item", 999999) == false, "不存在的物品图标应当返回 false");
            Expect(TEdit5.Controls.ItemIconCache.HasBuiltin("Wall", 1), "墙体 1 的图标应当存在");

            Console.WriteLine("[21/22] 生成世界分析报告");
            string analysis = TEdit.Editor.WorldAnalysis.AnalyzeWorld(loaded);
            Expect(!string.IsNullOrWhiteSpace(analysis), "世界分析报告不应为空");
            Expect(analysis.Contains(":"), "报告应当是键值形式");

            Console.WriteLine("[22/22] 保存修改并再次校验");
            await World.SaveAsync(loaded, savedPath);
            var (reloaded, reloadError) = await World.LoadWorldAsync(savedPath);
            Expect(reloaded != null, $"修改后的世界应当能重新读取: {reloadError?.Message}");
            Expect(reloaded!.Tiles[50, 50].IsActive, "保存后的世界应当保留绘制结果");

            Console.WriteLine("SMOKE TEST PASSED");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SMOKE TEST FAILED: {ex}");
            LogManager.Exception(ex, "自检失败");
            return 1;
        }
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }

        Console.WriteLine($"  通过: {message}");
    }
}
