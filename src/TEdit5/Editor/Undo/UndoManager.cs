using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TEdit.Editor;
using TEdit.Editor.Undo;
using TEdit.Geometry;
using TEdit.Terraria;
using TEdit5.Services;
using TEdit5.Utility;

namespace TEdit5.Editor.Undo;

/// <summary>
/// 磁盘分页的撤销管理器. 逻辑与 Windows 版一致: 每一笔操作写入一个撤销文件,
/// 撤销时写回重做文件, 依靠临时目录里的文件完成可增长的历史记录.
/// </summary>
public partial class UndoManager : ReactiveObject, IUndoManager
{
    private static readonly string UniqueValue;
    private static readonly string Dir;
    private static readonly string UndoFile;
    private static readonly string RedoFile;
    private static readonly string UndoAliveFile;
    private static readonly Timer UndoAliveTimer;

    static UndoManager()
    {
        UniqueValue = DateTime.Now.ToString("yyyyMMddHHmmss");
        Dir = Path.Combine(AppPaths.UndoPath, "undo_" + UniqueValue);
        UndoFile = Path.Combine(Dir, "undo_temp_{0}");
        RedoFile = Path.Combine(Dir, "redo_temp_{0}");
        UndoAliveFile = Path.Combine(Dir, "alive.txt");

        try
        {
            if (!Directory.Exists(Dir))
            {
                LogManager.Info($"创建撤销缓存目录: {Dir}");
                Directory.CreateDirectory(Dir);
                File.Create(UndoAliveFile).Close();
            }
        }
        catch (Exception ex)
        {
            LogManager.Exception(ex, $"无法创建撤销缓存目录 {Dir}");
        }

        CleanupOldUndoFolders();
        UndoAliveTimer = new Timer(UndoAlive, null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
    }

    private static void UndoAlive(object? state)
    {
        try
        {
            if (!File.Exists(UndoAliveFile))
            {
                File.Create(UndoAliveFile).Close();
            }

            File.SetLastWriteTimeUtc(UndoAliveFile, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            LogManager.Debug($"更新撤销保活文件失败: {ex.Message}");
        }
    }

    /// <summary>清理超过 7 天没有保活记录的撤销目录.</summary>
    private static void CleanupOldUndoFolders()
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(AppPaths.UndoPath, "undo*", SearchOption.AllDirectories))
            {
                try
                {
                    var fi = new FileInfo(Path.Combine(dir, "alive.txt"));
                    if (fi.Exists && fi.LastWriteTime < DateTime.Now.AddDays(-7))
                    {
                        Directory.Delete(dir, true);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Debug($"清理撤销目录 {dir} 失败: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LogManager.Debug($"扫描撤销目录失败: {ex.Message}");
        }
    }

    private static bool IsUndoDirAlive(string directory)
    {
        string aliveFile = Path.Combine(directory, "alive.txt");

        if (!File.Exists(aliveFile))
        {
            return false;
        }

        // 保活记录超过 20 分钟没有更新, 认为这个撤销目录已经死了.
        if (File.GetLastAccessTimeUtc(aliveFile) < DateTime.UtcNow.AddMinutes(-20))
        {
            return false;
        }

        return true;
    }

    private static void CleanupOldUndoFiles(bool forceCleanup = false)
    {
        try
        {
            foreach (var file in Directory.GetFiles(AppPaths.UndoPath).ToList())
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // 撤销临时文件可能被并发操作占用, 下个周期再删.
                }
            }

            foreach (var dir in Directory.GetDirectories(AppPaths.UndoPath).ToList())
            {
                if (!Equals(dir, AppPaths.UndoPath) && !IsUndoDirAlive(dir))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch (IOException)
                    {
                        // 目录可能被并发撤销操作占用, 下个周期再删.
                    }
                }
            }

            if (forceCleanup && Directory.Exists(AppPaths.UndoPath))
            {
                Directory.Delete(AppPaths.UndoPath, true);
            }
        }
        catch (Exception ex)
        {
            LogManager.Exception(ex, "清理撤销缓存失败");
        }
    }

    private readonly World _world;
    private readonly NotifyTileChanged? _notifyTileChanged;
    private readonly Action? _undoApplied;

    [Reactive]
    private UndoBuffer? _buffer;

    private int _currentIndex;
    private int _maxIndex;
    private Task? _pendingClose;

    public int CurrentIndex => _currentIndex;

    public string CacheDirectory => Dir;

    /// <summary>当前是否还有可撤销的操作.</summary>
    public bool CanUndo => _currentIndex > 0;

    /// <summary>当前是否还有可重做的操作.</summary>
    public bool CanRedo => _currentIndex <= _maxIndex;

    public UndoManager(
        World world,
        NotifyTileChanged? notifyTileChanged = null,
        Action? undoApplied = null)
    {
        if (!Directory.Exists(Dir))
        {
            Directory.CreateDirectory(Dir);
        }

        _world = world;
        _buffer = new UndoBuffer(GetUndoFileName(), _world);
        _notifyTileChanged = notifyTileChanged;
        _undoApplied = undoApplied;
    }

    public string GetUndoFileName() => string.Format(UndoFile, _currentIndex);

    public void SaveUndo(bool updateMax = true)
    {
        if (_buffer == null || _buffer.IsEmpty)
        {
            return;
        }

        ValidateAndRemoveChests();
        if (updateMax)
        {
            _maxIndex = _currentIndex;
        }

        _currentIndex++;
        _pendingClose = _buffer.CloseAsync();
        CreateBuffer();
        _undoApplied?.Invoke();
    }

    /// <summary>等待上一次撤销文件序列化完成, 读取撤销/重做文件前必须调用.</summary>
    private void WaitForPendingClose()
    {
        if (_pendingClose != null)
        {
            _pendingClose.Wait();
            _pendingClose = null;
        }
    }

    private void CreateBuffer()
    {
        _buffer?.Dispose();
        _buffer = null;
        Buffer = new UndoBuffer(GetUndoFileName(), _world);
    }

    public CacheCleanupResult ClearHistory()
    {
        WaitForPendingClose();
        _buffer?.Dispose();
        _buffer = null;

        var result = FileMaintenance.ClearDirectoryContents(
            Dir,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { UndoAliveFile });

        _currentIndex = 0;
        _maxIndex = 0;
        CreateBuffer();
        _undoApplied?.Invoke();
        return result;
    }

    public void SaveTile(Vector2Int32 p) => SaveTile(p.X, p.Y);

    public void SaveTile(int x, int y, bool removeEntities = false)
    {
        if (_buffer == null)
        {
            CreateBuffer();
        }

        ValidateAndRemoveChests();
        SaveTileToBuffer(Buffer, x, y, removeEntities);
    }

    private void SaveTileToBuffer(UndoBuffer buffer, int x, int y, bool removeEntities = false)
    {
        if (x < 0 || x >= _world.TilesWide || y < 0 || y >= _world.TilesHigh)
        {
            return;
        }

        var curTile = _world.Tiles[x, y];

        bool isModTile = curTile.IsActive && curTile.Type >= WorldConfiguration.TileCount;
        bool isVanillaFramed = curTile.IsActive &&
            curTile.Type < buffer.TileImportance.Length &&
            buffer.TileImportance[curTile.Type];

        if (isVanillaFramed || isModTile)
        {
            // 原版方块用锚点判断, 模组方块本身就在锚点上, 直接查即可.
            bool isAnchor = isModTile || _world.IsAnchor(x, y);

            if (isAnchor)
            {
                var curchest = isModTile
                    ? _world.GetChestAtTile(x, y, true)
                    : _world.GetChestAtTile(x, y);
                if (curchest != null && buffer.Chests.All(c => c.X != curchest.X || c.Y != curchest.Y))
                {
                    if (removeEntities)
                    {
                        _world.Chests.Remove(curchest);
                    }

                    buffer.Chests.Add(curchest.Copy());
                }

                var cursign = isModTile
                    ? _world.GetSignAtTile(x, y, true)
                    : _world.GetSignAtTile(x, y);
                if (cursign != null && buffer.Signs.All(s => s.X != cursign.X || s.Y != cursign.Y))
                {
                    if (removeEntities)
                    {
                        _world.Signs.Remove(cursign);
                    }

                    buffer.Signs.Add(cursign.Copy());
                }

                var curTe = isModTile
                    ? _world.GetTileEntityAtTile(x, y, true)
                    : _world.GetTileEntityAtTile(x, y);
                if (curTe != null && buffer.TileEntities.All(te => te.PosX != curTe.PosX || te.PosY != curTe.PosY))
                {
                    if (removeEntities)
                    {
                        _world.TileEntities.Remove(curTe);
                    }

                    buffer.TileEntities.Add(curTe.Copy());
                }
            }
        }

        buffer.Add(new Vector2Int32(x, y), curTile);
    }

    private void ValidateAndRemoveChests()
    {
        if (Buffer == null || Buffer.LastTile == null)
        {
            return;
        }

        var lastTile = Buffer.LastTile;

        // 快速路径: 保存的方块和世界里当前的方块都不是容器时直接跳过, 省掉刷子涂抹时的昂贵判断.
        bool savedIsEntity = lastTile.Tile.IsActive &&
            (lastTile.Tile.IsChest() || lastTile.Tile.IsSign() || lastTile.Tile.IsTileEntity());

        var existingLastTile = _world.Tiles[lastTile.Location.X, lastTile.Location.Y];
        bool currentIsEntity = existingLastTile.IsActive &&
            (existingLastTile.IsChest() || existingLastTile.IsSign() || existingLastTile.IsTileEntity());

        if (!savedIsEntity && !currentIsEntity)
        {
            return;
        }

        _world.RemoveOrphanedContainerAt(lastTile.Location.X, lastTile.Location.Y);

        if (_world.IsAnchor(lastTile.Location.X, lastTile.Location.Y))
        {
            if (existingLastTile.IsChest())
            {
                if (_world.GetChestAtTile(lastTile.Location.X, lastTile.Location.Y) == null)
                {
                    _world.Chests.Add(new Chest(lastTile.Location.X, lastTile.Location.Y));
                }
            }
            else if (existingLastTile.IsSign())
            {
                if (_world.GetSignAtTile(lastTile.Location.X, lastTile.Location.Y) == null)
                {
                    _world.Signs.Add(new Sign(lastTile.Location.X, lastTile.Location.Y, string.Empty));
                }
            }
            else if (existingLastTile.IsTileEntity())
            {
                if (_world.GetTileEntityAtTile(lastTile.Location.X, lastTile.Location.Y) == null)
                {
                    _world.TileEntities.Add(TileEntity.CreateForTile(
                        existingLastTile,
                        lastTile.Location.X,
                        lastTile.Location.Y,
                        _world.TileEntities.Count));
                }
            }
        }
    }

    public IReadOnlyList<Vector2Int32> Undo()
    {
        if (_currentIndex <= 0)
        {
            return Array.Empty<Vector2Int32>();
        }

        WaitForPendingClose();

        var version = _world?.Version ?? WorldConfiguration.CompatibleVersion;

        string undoFileName = string.Format(UndoFile, _currentIndex - 1);
        string redoFileName = string.Format(RedoFile, _currentIndex);
        UndoBuffer redo = new UndoBuffer(redoFileName, _world);

        LogManager.Debug($"打开撤销文件: {Path.GetFileNameWithoutExtension(undoFileName)}");
        var changedTiles = new List<Vector2Int32>();

        if (!File.Exists(undoFileName))
        {
            LogManager.Warn($"撤销文件缺失, 跳过: {undoFileName}");
            redo.Dispose();
            _currentIndex--;
            CreateBuffer();
            return changedTiles;
        }

        using (var stream = new FileStream(undoFileName, FileMode.Open))
        using (var br = new BinaryReader(stream, System.Text.Encoding.UTF8, false))
        {
            foreach (var undoTile in UndoBuffer.ReadUndoTilesFromStream(br, _world.TileFrameImportant))
            {
                Vector2Int32 pixel = undoTile.Location;
                SaveTileToBuffer(redo, pixel.X, pixel.Y, true);
                _world.Tiles[pixel.X, pixel.Y] = (Tile)undoTile.Tile;
                changedTiles.Add(pixel);
            }

            redo.Close();
            redo.Dispose();

            var loadedChests = World.LoadChestData(br, version).ToList();
            foreach (var chest in loadedChests)
            {
                _world.Chests.Add(chest);
            }

            foreach (var sign in World.LoadSignData(br))
            {
                _world.Signs.Add(sign);
            }

            var loadedEntities = World.LoadTileEntityData(br, version).ToList();
            foreach (var te in loadedEntities)
            {
                _world.TileEntities.Add(te);
            }

            ModDataSerializer.LoadModPayload(
                br,
                loadedChests,
                loadedEntities,
                out var modTiles,
                out var modWalls,
                _world.TwldData);

            // 把模组方块/墙体身份从 NBT 覆盖回来, 原版序列化会丢掉这部分信息.
            ModDataSerializer.ApplyOverlays(
                _world.Tiles,
                _world.TilesWide,
                _world.TilesHigh,
                modTiles,
                modWalls);
        }

        _currentIndex--;
        CreateBuffer();
        _undoApplied?.Invoke();

        return changedTiles;
    }

    public IReadOnlyList<Vector2Int32> Redo()
    {
        if (_currentIndex > _maxIndex)
        {
            return Array.Empty<Vector2Int32>();
        }

        WaitForPendingClose();

        var version = _world?.Version ?? WorldConfiguration.CompatibleVersion;
        string redoFileName = string.Format(RedoFile, _currentIndex + 1);

        if (!File.Exists(redoFileName))
        {
            return Array.Empty<Vector2Int32>();
        }

        LogManager.Debug($"打开重做文件: {Path.GetFileNameWithoutExtension(redoFileName)}");
        var changedTiles = new List<Vector2Int32>();

        using (var stream = new FileStream(redoFileName, FileMode.Open))
        using (var br = new BinaryReader(stream))
        {
            foreach (var undoTile in UndoBuffer.ReadUndoTilesFromStream(br, _world.TileFrameImportant))
            {
                var curTile = (Tile)_world.Tiles[undoTile.Location.X, undoTile.Location.Y];

                // 直接写回共享撤销缓冲, 跳过 SaveTile/ValidateAndRemoveChests,
                // 避免重做循环里生成多余的空容器.
                SaveTileToBuffer(Buffer, undoTile.Location.X, undoTile.Location.Y, false);

                if (curTile.IsChest())
                {
                    var curchest = _world.GetChestAtTile(undoTile.Location.X, undoTile.Location.Y, true);
                    if (curchest != null)
                    {
                        _world.Chests.Remove(curchest);
                    }
                }

                if (curTile.IsSign())
                {
                    var cursign = _world.GetSignAtTile(undoTile.Location.X, undoTile.Location.Y, true);
                    if (cursign != null)
                    {
                        _world.Signs.Remove(cursign);
                    }
                }

                if (curTile.IsTileEntity())
                {
                    var curTe = _world.GetTileEntityAtTile(undoTile.Location.X, undoTile.Location.Y, true);
                    if (curTe != null)
                    {
                        _world.TileEntities.Remove(curTe);
                    }
                }

                _world.Tiles[undoTile.Location.X, undoTile.Location.Y] = (Tile)undoTile.Tile;
                changedTiles.Add(undoTile.Location);
            }

            var loadedChests = World.LoadChestData(br, version).ToList();
            foreach (var chest in loadedChests)
            {
                _world.Chests.Add(chest);
            }

            foreach (var sign in World.LoadSignData(br))
            {
                _world.Signs.Add(sign);
            }

            var loadedEntities = World.LoadTileEntityData(br, version).ToList();
            foreach (var te in loadedEntities)
            {
                _world.TileEntities.Add(te);
            }

            ModDataSerializer.LoadModPayload(
                br,
                loadedChests,
                loadedEntities,
                out var modTiles,
                out var modWalls,
                _world.TwldData);

            ModDataSerializer.ApplyOverlays(
                _world.Tiles,
                _world.TilesWide,
                _world.TilesHigh,
                modTiles,
                modWalls);
        }

        SaveUndo(updateMax: false);

        return changedTiles;
    }

    #region IUndoManager

    public void SaveTile(World world, Vector2Int32 location, bool removeEntities = false) =>
        SaveTile(location.X, location.Y, removeEntities);

    public void SaveTile(World world, int x, int y, bool removeEntities = false) =>
        SaveTile(x, y, removeEntities);

    public Task SaveUndoAsync()
    {
        SaveUndo();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Vector2Int32>> UndoAsync(World world) =>
        Task.FromResult(Undo());

    public Task<IReadOnlyList<Vector2Int32>> RedoAsync(World world) =>
        Task.FromResult(Redo());

    public Task StartUndoAsync() => Task.CompletedTask;

    #endregion

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _buffer?.Dispose();
            _buffer = null;
        }

        CleanupOldUndoFiles(false);
        _disposed = true;
    }

    ~UndoManager()
    {
        Dispose(false);
    }
}
