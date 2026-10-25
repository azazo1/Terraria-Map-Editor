using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using ReactiveUI.Primitives;
using TEdit5.Controls;
using TEdit5.Editor.Undo;
using TEdit5.Services;
using TEdit.Editor;
using TEdit.Terraria;

namespace TEdit5.ViewModels;

/// <summary>方块变化事件参数, 坐标是世界方块坐标.</summary>
public sealed class TileChangedEventArgs : EventArgs
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; } = 1;
    public int Height { get; init; } = 1;
}

/// <summary>
/// 一个打开的世界文档. 负责世界编辑器, 撤销栈, 保存状态与视图状态(缩放, 光标坐标).
/// </summary>
public partial class DocumentViewModel : ReactiveObject, IDisposable
{
    [Reactive] private int _zoom = 100;
    [Reactive] private int _minZoom = 7;
    [Reactive] private int _maxZoom = 6400;
    [Reactive] private Point _cursorTileCoordinate;
    [Reactive] private SkiaWorldRenderBox.SelectionModes _selectionMode;
    [Reactive] private World _world;
    [Reactive] private WorldEditor _worldEditor;
    private string _fileName = string.Empty;
    [Reactive] private bool _isDirty;
    [Reactive] private bool _canUndo;
    [Reactive] private bool _canRedo;

    public ToolSelectionViewModel ToolSelection { get; }

    public TilePicker TilePicker { get; }

    public ISelection Selection { get; }

    public UndoManager UndoManager { get; }

    /// <summary>剪贴板缓冲.</summary>
    public ClipboardService Clipboard { get; }

    /// <summary>NPC 名称面板的数据与命令.</summary>
    public NpcEditorViewModel NpcEditor { get; }

    /// <summary>特殊方块面板的状态.</summary>
    public SpecialTileViewModel SpecialTiles { get; }

    /// <summary>右侧活动栏当前页签, 供面板之间互相跳转.</summary>
    [Reactive] private int _selectedPanelIndex;

    /// <summary>图鉴面板.</summary>
    public BestiaryEditorViewModel Bestiary { get; }

    /// <summary>旗帜面板.</summary>
    public BannerEditorViewModel Banners { get; }

    /// <summary>过滤面板.</summary>
    public FilterEditorViewModel Filters { get; }

    /// <summary>查找面板.</summary>
    public FindEditorViewModel Find { get; }

    /// <summary>创意能力面板.</summary>
    public CreativePowersViewModel Creative { get; }

    /// <summary>NBT 浏览器.</summary>
    public NbtExplorerViewModel Nbt { get; }

    /// <summary>玩家编辑器, 打开 .plr 后显示基本信息.</summary>
    public PlayerEditorViewModel Player { get; } = new();

    /// <summary>精灵面板.</summary>
    public SpriteEditorViewModel Sprites { get; }

    /// <summary>脚本面板.</summary>
    public ScriptEditorViewModel Scripts { get; }

    /// <summary>请求把视图滚动到指定方块位置.</summary>
    public event EventHandler<TEdit.Geometry.Vector2Int32>? FocusRequested;

    /// <summary>滚动视图到指定方块.</summary>
    public void RequestFocus(int tileX, int tileY)
        => FocusRequested?.Invoke(this, new TEdit.Geometry.Vector2Int32(tileX, tileY));

    /// <summary>请求把视图缩放到适应窗口.</summary>
    public void RequestFit() => FitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>视图适应窗口请求.</summary>
    public event EventHandler? FitRequested;

    /// <summary>只请求重绘, 不改变保存状态.</summary>
    public void RequestRedraw() => RenderInvalidated?.Invoke(this, EventArgs.Empty);

    /// <summary>标记世界有未保存的修改并请求重绘.</summary>
    public void MarkDirty()
    {
        IsDirty = true;
        RenderInvalidated?.Invoke(this, EventArgs.Empty);
    }

    [Reactive] private string _analysisText = string.Empty;

    public ReactiveCommand<RxVoid, RxVoid> AnalyzeWorldCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> AnalyzeWorldSaveCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> CopySelectionCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> PasteSelectionCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearClipboardCommand { get; }

    public ReactiveCommand<TEdit.Editor.Clipboard.ClipboardBuffer, RxVoid> PasteBufferCommand { get; }

    public ReactiveCommand<TEdit.Editor.Clipboard.ClipboardBuffer, RxVoid> RemoveBufferCommand { get; }

    public ReactiveCommand<TEdit.Editor.Clipboard.ClipboardBuffer, RxVoid> RotateBufferCommand { get; }

    public ReactiveCommand<TEdit.Editor.Clipboard.ClipboardBuffer, RxVoid> FlipXBufferCommand { get; }

    public ReactiveCommand<TEdit.Editor.Clipboard.ClipboardBuffer, RxVoid> FlipYBufferCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ReplaceAllCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ReplaceSelectionCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> UndoCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RedoCommand { get; }

    /// <summary>局部方块发生变化, 渲染端据此只重绘覆盖到的图块.</summary>
    public event EventHandler<TileChangedEventArgs>? TilesChanged;

    /// <summary>整幅画面需要重绘, 例如撤销之后或者世界整体替换.</summary>
    public event EventHandler? RenderInvalidated;

    /// <summary>世界文件路径, 空字符串表示尚未保存到磁盘.</summary>
    public string FileName
    {
        get => _fileName;
        set
        {
            if (_fileName == value)
            {
                return;
            }

            _fileName = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(Title));
        }
    }

    /// <summary>世界名称, 编辑后立即写回世界并标记为已修改.</summary>
    public string WorldTitle
    {
        get => World?.Title ?? string.Empty;
        set
        {
            if (World == null || World.Title == value)
            {
                return;
            }

            World.Title = value;
            IsDirty = true;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(Title));
        }
    }

    /// <summary>涂抹模式列表, 供设置面板选择.</summary>
    public IReadOnlyList<PaintMode> PaintModes { get; } = Enum.GetValues<PaintMode>();

    /// <summary>液体类型列表.</summary>
    public IReadOnlyList<LiquidType> LiquidTypes { get; } = Enum.GetValues<LiquidType>();

    /// <summary>液体量列表.</summary>
    public IReadOnlyList<LiquidAmountMode> LiquidAmountModes { get; } = Enum.GetValues<LiquidAmountMode>();

    /// <summary>砖样式列表.</summary>
    public IReadOnlyList<BrickStyle> BrickStyles { get; } = Enum.GetValues<BrickStyle>();

    /// <summary>可选方块.</summary>
    public IReadOnlyList<PaletteItem> TilePaletteItems { get; }

    /// <summary>可选墙体.</summary>
    public IReadOnlyList<PaletteItem> WallPaletteItems { get; }

    /// <summary>当前选中的方块项, 与 TilePicker.Tile 双向同步.</summary>
    public PaletteItem? SelectedTileItem
    {
        get => TilePaletteItems.FirstOrDefault(i => i.Id == TilePicker.Tile);
        set
        {
            if (value == null)
            {
                return;
            }

            TilePicker.Tile = value.Id;
            TilePicker.TileStyleActive = true;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>当前选中的墙体项.</summary>
    public PaletteItem? SelectedWallItem
    {
        get => WallPaletteItems.FirstOrDefault(i => i.Id == TilePicker.Wall);
        set
        {
            if (value == null)
            {
                return;
            }

            TilePicker.Wall = value.Id;
            TilePicker.WallStyleActive = true;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>方块漆颜色项.</summary>
    public PaletteItem? SelectedTilePaintItem
    {
        get => PaintPaletteItems.FirstOrDefault(i => i.Id == TilePicker.TileColor);
        set
        {
            if (value == null)
            {
                return;
            }

            TilePicker.TileColor = value.Id;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>墙体漆颜色项.</summary>
    public PaletteItem? SelectedWallPaintItem
    {
        get => PaintPaletteItems.FirstOrDefault(i => i.Id == TilePicker.WallColor);
        set
        {
            if (value == null)
            {
                return;
            }

            TilePicker.WallColor = value.Id;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>蒙版设置, 与原版 MaskSettings 对应.</summary>
    public TileMaskSettings MaskSettings => WorldEditor.MaskSettings;

    /// <summary>蒙版模式列表.</summary>
    public IReadOnlyList<MaskMode> MaskModes { get; } = Enum.GetValues<MaskMode>();

    /// <summary>液面蒙版模式列表.</summary>
    public IReadOnlyList<LiquidLevelMaskMode> LiquidLevelMaskModes { get; } = Enum.GetValues<LiquidLevelMaskMode>();

    /// <summary>可用于蒙版的方块.</summary>
    public IReadOnlyList<PaletteItem> TileMaskItems { get; }

    /// <summary>可用于蒙版的墙体.</summary>
    public IReadOnlyList<PaletteItem> WallMaskItems { get; }

    public PaletteItem? SelectedTileMaskItem
    {
        get => TileMaskItems.FirstOrDefault(i => i.Id == MaskSettings.TileMaskValue);
        set
        {
            if (value == null)
            {
                return;
            }

            MaskSettings.TileMaskValue = value.Id;
            this.RaisePropertyChanged();
        }
    }

    public PaletteItem? SelectedWallMaskItem
    {
        get => WallMaskItems.FirstOrDefault(i => i.Id == MaskSettings.WallMaskValue);
        set
        {
            if (value == null)
            {
                return;
            }

            MaskSettings.WallMaskValue = value.Id;
            this.RaisePropertyChanged();
        }
    }

    public PaletteItem? SelectedTilePaintMaskItem
    {
        get => PaintPaletteItems.FirstOrDefault(i => i.Id == MaskSettings.TilePaintMaskValue);
        set
        {
            if (value == null)
            {
                return;
            }

            MaskSettings.TilePaintMaskValue = value.Id;
            this.RaisePropertyChanged();
        }
    }

    public PaletteItem? SelectedWallPaintMaskItem
    {
        get => PaintPaletteItems.FirstOrDefault(i => i.Id == MaskSettings.WallPaintMaskValue);
        set
        {
            if (value == null)
            {
                return;
            }

            MaskSettings.WallPaintMaskValue = value.Id;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>液面蒙版数值, 供 Slider 使用.</summary>
    public double LiquidLevelMaskValue
    {
        get => MaskSettings.LiquidLevelMaskValue;
        set
        {
            int clamped = Math.Clamp((int)value, 0, 255);
            if (MaskSettings.LiquidLevelMaskValue == clamped)
            {
                return;
            }

            MaskSettings.LiquidLevelMaskValue = (byte)clamped;
            this.RaisePropertyChanged();
        }
    }

    public bool MaskPresetOff
    {
        get => MaskSettings.MaskPreset == MaskPreset.Off;
        set => SetMaskPreset(MaskPreset.Off, value);
    }

    public bool MaskPresetExactMatch
    {
        get => MaskSettings.MaskPreset == MaskPreset.ExactMatch;
        set => SetMaskPreset(MaskPreset.ExactMatch, value);
    }

    public bool MaskPresetCustom
    {
        get => MaskSettings.MaskPreset == MaskPreset.Custom;
        set => SetMaskPreset(MaskPreset.Custom, value);
    }

    private void SetMaskPreset(MaskPreset preset, bool isChecked)
    {
        if (!isChecked)
        {
            return;
        }

        MaskSettings.MaskPreset = preset;
        this.RaisePropertyChanged(nameof(MaskPresetOff));
        this.RaisePropertyChanged(nameof(MaskPresetExactMatch));
        this.RaisePropertyChanged(nameof(MaskPresetCustom));
    }

    /// <summary>清空所有蒙版条件.</summary>
    public void ClearAllMasks()
    {
        MaskSettings.ClearAll();
        this.RaisePropertyChanged(nameof(MaskPresetOff));
        this.RaisePropertyChanged(nameof(MaskPresetExactMatch));
        this.RaisePropertyChanged(nameof(MaskPresetCustom));
    }

    /// <summary>可选涂料.</summary>    /// <summary>可选涂料.</summary>
    public IReadOnlyList<PaletteItem> PaintPaletteItems { get; }

    /// <summary>画笔可选形状.</summary>
    public IReadOnlyList<BrushShape> BrushShapeOptions { get; } = Enum.GetValues<BrushShape>();

    /// <summary>画笔形状, 直接作用在世界编辑器上.</summary>
    public BrushShape SelectedBrushShape
    {
        get => WorldEditor?.Brush.Shape ?? TEdit.Editor.BrushShape.Square;
        set
        {
            if (WorldEditor == null || WorldEditor.Brush.Shape == value)
            {
                return;
            }

            WorldEditor.Brush.Shape = value;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>画笔边长, 宽高同步设置.</summary>
    public double BrushSize
    {
        get => WorldEditor?.Brush.Width ?? 1;
        set
        {
            if (WorldEditor == null)
            {
                return;
            }

            int size = Math.Max(1, (int)value);
            WorldEditor.Brush.Width = size;
            WorldEditor.Brush.Height = size;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>尺寸说明, 便于直接显示.</summary>
    public string SizeText => World == null ? string.Empty : $"{World.TilesWide} x {World.TilesHigh}";

    /// <summary>出生点说明.</summary>
    public string SpawnText => World == null ? string.Empty : $"{World.SpawnX}, {World.SpawnY}";

    /// <summary>世界编号说明.</summary>
    public string WorldIdText => World == null ? string.Empty : $"{World.WorldId:X8}";

    /// <summary>光标所在的方块坐标.</summary>
    public string CursorText => $"{CursorTileCoordinate.X:F0}, {CursorTileCoordinate.Y:F0}";

    /// <summary>光标处相对地表的深度, 单位为英尺.</summary>
    public string DepthText
    {
        get
        {
            if (World == null)
            {
                return string.Empty;
            }

            double depth = (CursorTileCoordinate.Y - World.GroundLevel) * 2;
            return depth >= 0 ? $"地下 {depth:F0} 英尺" : $"地表以上 {-depth:F0} 英尺";
        }
    }

    /// <summary>光标处方块名称.</summary>
    public string TileNameText
    {
        get
        {
            var tile = GetCursorTile();
            if (tile == null || !tile.Value.IsActive)
            {
                return "空";
            }

            return TEdit.Terraria.WorldConfiguration.TileProperties[tile.Value.Type]?.Name ?? "未知";
        }
    }

    /// <summary>光标处墙体名称.</summary>
    public string WallNameText
    {
        get
        {
            var tile = GetCursorTile();
            if (tile == null || tile.Value.Wall <= 0)
            {
                return "空";
            }

            return TEdit.Terraria.WorldConfiguration.WallProperties[tile.Value.Wall]?.Name ?? "未知";
        }
    }

    /// <summary>选区大小说明.</summary>
    public string SelectionSizeText =>
        Selection.IsActive ? $"{Selection.SelectionArea.Width} x {Selection.SelectionArea.Height}" : "无";

    private TEdit.Terraria.Tile? GetCursorTile()
    {
        if (World == null)
        {
            return null;
        }

        int x = (int)CursorTileCoordinate.X;
        int y = (int)CursorTileCoordinate.Y;

        if (x < 0 || y < 0 || x >= World.TilesWide || y >= World.TilesHigh)
        {
            return null;
        }

        return World.Tiles[x, y];
    }

    /// <summary>世界版本说明.</summary>
    public string VersionText => World == null ? string.Empty : World.WorldVersion.ToString();

    /// <summary>种子说明.</summary>
    public string SeedText => string.IsNullOrEmpty(World?.Seed) ? "(无)" : World!.Seed;

    /// <summary>地表高度说明.</summary>
    public string GroundLevelText => World == null ? string.Empty : World.GroundLevel.ToString("F0");

    /// <summary>岩石层高度说明.</summary>
    public string RockLevelText => World == null ? string.Empty : World.RockLevel.ToString("F0");

    /// <summary>世界 GUID 说明.</summary>
    public string GuidText => World == null ? string.Empty : World.WorldGUID.ToString();

    /// <summary>上次保存时间说明.</summary>
    public string LastSaveText => World == null ? string.Empty : World.LastSave.ToString("yyyy-MM-dd HH:mm");

    /// <summary>箱子数量说明.</summary>
    public string ChestCountText => World == null ? string.Empty : World.Chests.Count.ToString();

    /// <summary>告示牌数量说明.</summary>
    public string SignCountText => World == null ? string.Empty : World.Signs.Count.ToString();

    /// <summary>图格实体数量说明.</summary>
    public string TileEntityCountText => World == null ? string.Empty : World.TileEntitiesNumber.ToString();

    /// <summary>NPC 数量说明.</summary>
    public string NpcCountText => World == null ? string.Empty : World.NPCs.Count.ToString();

    /// <summary>保存状态说明.</summary>
    public string DirtyText => IsDirty ? "有未保存的修改" : "已保存";

    /// <summary>标题栏显示的名字: 优先用文件全名, 没有文件时退回世界名.</summary>
    public string Title
    {
        get
        {
            if (!string.IsNullOrEmpty(FileName))
            {
                return Path.GetFileName(FileName);
            }

            return string.IsNullOrWhiteSpace(World?.Title) ? "未命名世界" : World!.Title;
        }
    }

    public DocumentViewModel(
        World world,
        ToolSelectionViewModel toolSelection,
        TilePicker tilePicker,
        string? fileName = null)
    {
        _world = world;
        _fileName = fileName ?? string.Empty;
        ToolSelection = toolSelection;
        TilePicker = tilePicker;
        Selection = new Selection();
        Clipboard = new ClipboardService(Selection);

        TilePaletteItems = TEdit.Terraria.WorldConfiguration.TileBricks
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => new PaletteItem(t.Id, t.Name, t.Color))
            .ToList();

        WallPaletteItems = TEdit.Terraria.WorldConfiguration.WallProperties
            .OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase)
            .Select(w => new PaletteItem(w.Id, w.Name, w.Color, "Wall"))
            .ToList();

        TileMaskItems = TEdit.Terraria.WorldConfiguration.TileBricksMask
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => new PaletteItem(t.Id, t.Name, t.Color))
            .ToList();

        WallMaskItems = TEdit.Terraria.WorldConfiguration.WallPropertiesMask
            .OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase)
            .Select(w => new PaletteItem(w.Id, w.Name, w.Color, "Wall"))
            .ToList();

        PaintPaletteItems = TEdit.Terraria.WorldConfiguration.PaintProperties
            .Select(p => new PaletteItem(p.Id, p.Name, p.Color, "Paint"))
            .ToList();

        NpcEditor = new NpcEditorViewModel(this);
        NpcEditor.Refresh();

        SpecialTiles = new SpecialTileViewModel(this);
        Bestiary = new BestiaryEditorViewModel(this);
        Banners = new BannerEditorViewModel(this);
        Filters = new FilterEditorViewModel();
        Find = new FindEditorViewModel(this);
        Creative = new CreativePowersViewModel(this);
        Nbt = new NbtExplorerViewModel(this);
        Sprites = new SpriteEditorViewModel(this) { World = world };
        Scripts = new ScriptEditorViewModel
        {
            World = world,
            Refresh = RequestRedraw,
            SelectionProvider = () =>
            {
                if (!Selection.IsActive)
                {
                    return string.Empty;
                }

                var area = Selection.SelectionArea;
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3}",
                    area.X,
                    area.Y,
                    area.Width,
                    area.Height);
            },
            SelectionSetter = (x, y, width, height) =>
            {
                Selection.SetRectangle(
                    new TEdit.Geometry.Vector2Int32(x, y),
                    new TEdit.Geometry.Vector2Int32(x + width, y + height));
            },
            SaveUndoSnapshot = () =>
            {
                var undo = UndoManager;
                if (undo != null)
                {
                    undo.SaveUndo();
                }
            },
        };

        UndoManager = new UndoManager(world, OnTileChanged, OnUndoApplied);
        _worldEditor = new WorldEditor(
            tilePicker,
            new TileMaskSettings(),
            world,
            Selection,
            UndoManager,
            OnTileChanged);

        // 保存状态变化时同步刷新提示文案.
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsDirty))
            {
                this.RaisePropertyChanged(nameof(DirtyText));
            }

            if (e.PropertyName == nameof(CursorTileCoordinate))
            {
                this.RaisePropertyChanged(nameof(CursorText));
                this.RaisePropertyChanged(nameof(DepthText));
                SpecialTiles.UpdateAtCursor();
                this.RaisePropertyChanged(nameof(TileNameText));
                this.RaisePropertyChanged(nameof(WallNameText));
            }
        };

        AnalyzeWorldCommand = ReactiveCommand.Create(AnalyzeWorld);
        AnalyzeWorldSaveCommand = ReactiveCommand.Create(SaveAnalysis);
        CopySelectionCommand = ReactiveCommand.Create(CopySelectionToClipboard);
        PasteSelectionCommand = ReactiveCommand.Create(PasteFromClipboard);
        ClearClipboardCommand = ReactiveCommand.Create(ClearClipboard);
        PasteBufferCommand = ReactiveCommand.Create<TEdit.Editor.Clipboard.ClipboardBuffer>(PasteBuffer);
        RemoveBufferCommand = ReactiveCommand.Create<TEdit.Editor.Clipboard.ClipboardBuffer>(buffer => Clipboard.Remove(buffer));
        RotateBufferCommand = ReactiveCommand.Create<TEdit.Editor.Clipboard.ClipboardBuffer>(buffer => Clipboard.Rotate(buffer));
        FlipXBufferCommand = ReactiveCommand.Create<TEdit.Editor.Clipboard.ClipboardBuffer>(buffer => Clipboard.FlipX(buffer));
        FlipYBufferCommand = ReactiveCommand.Create<TEdit.Editor.Clipboard.ClipboardBuffer>(buffer => Clipboard.FlipY(buffer));
        ReplaceAllCommand = ReactiveCommand.Create(() => Replace(selectionOnly: false));
        ReplaceSelectionCommand = ReactiveCommand.Create(() => Replace(selectionOnly: true));
        UndoCommand = ReactiveCommand.Create(Undo, this.WhenAnyValue(x => x.CanUndo));
        RedoCommand = ReactiveCommand.Create(Redo, this.WhenAnyValue(x => x.CanRedo));
    }

    /// <summary>把世界保存到当前文件, 没有文件时返回 false, 由调用方改为另存为.</summary>
    public async Task<bool> SaveAsync(IProgress<ProgressChangedEventArgs>? progress = null)
    {
        if (string.IsNullOrEmpty(FileName))
        {
            return false;
        }

        await SaveAsAsync(FileName, progress);
        return true;
    }

    /// <summary>另存为指定路径.</summary>
    public async Task SaveAsAsync(string fileName, IProgress<ProgressChangedEventArgs>? progress = null)
    {
        LogManager.Info($"保存世界到 {fileName}");
        await World.SaveAsync(World, fileName, progress: progress);

        FileName = fileName;
        IsDirty = false;
    }

    /// <summary>生成世界分析报告.</summary>
    private void AnalyzeWorld()
    {
        if (World == null)
        {
            return;
        }

        AnalysisText = TEdit.Editor.WorldAnalysis.AnalyzeWorld(World);
        LogManager.Info("已生成世界分析报告");
    }

    /// <summary>把分析报告写入应用数据目录.</summary>
    private void SaveAnalysis()
    {
        if (string.IsNullOrEmpty(AnalysisText))
        {
            AnalyzeWorld();
        }

        if (string.IsNullOrEmpty(AnalysisText))
        {
            return;
        }

        string name = string.IsNullOrEmpty(FileName)
            ? "world"
            : Path.GetFileNameWithoutExtension(FileName);

        string directory = Path.Combine(AppPaths.ApplicationData, "analysis");
        Directory.CreateDirectory(directory);

        string target = Path.Combine(directory, $"{name}-analysis.txt");
        File.WriteAllText(target, AnalysisText);
        LogManager.Info($"分析报告已保存到 {target}");
    }

    private void CopySelectionToClipboard()
    {
        if (World == null)
        {
            return;
        }

        Clipboard.CopySelection(World);
    }

    private void PasteFromClipboard()
    {
        if (World == null)
        {
            return;
        }

        var anchor = new TEdit.Geometry.Vector2Int32((int)CursorTileCoordinate.X, (int)CursorTileCoordinate.Y);
        if (!Clipboard.Paste(World, anchor, UndoManager))
        {
            return;
        }

        IsDirty = true;
        UpdateUndoState();
        RenderInvalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>把指定的缓冲粘贴到光标处.</summary>
    private void PasteBuffer(TEdit.Editor.Clipboard.ClipboardBuffer buffer)
    {
        if (World == null || buffer == null)
        {
            return;
        }

        Clipboard.ActiveBuffer = buffer;
        PasteFromClipboard();
    }

    private void ClearClipboard()
    {
        Clipboard.ClearBuffers();
        this.RaisePropertyChanged(nameof(Clipboard));
    }

    /// <summary>把当前画笔的方块替换到全世界或选区中.</summary>
    private void Replace(bool selectionOnly)
    {
        if (WorldEditor == null)
        {
            return;
        }

        WorldEditor.ReplaceAll(selectionOnly);
        IsDirty = true;
        UpdateUndoState();
        RenderInvalidated?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        var changed = UndoManager.Undo();
        OnTilesRecovered(changed);
    }

    public void Redo()
    {
        var changed = UndoManager.Redo();
        OnTilesRecovered(changed);
    }

    private void OnTilesRecovered(System.Collections.Generic.IReadOnlyList<TEdit.Geometry.Vector2Int32> changed)
    {
        if (changed.Count == 0)
        {
            return;
        }

        IsDirty = true;
        UpdateUndoState();
        RenderInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void OnTileChanged(int x, int y, int height = 1, int width = 1)
    {
        TilesChanged?.Invoke(this, new TileChangedEventArgs { X = x, Y = y, Width = width, Height = height });
    }

    private void OnUndoApplied()
    {
        IsDirty = true;
        UpdateUndoState();
        RenderInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateUndoState()
    {
        CanUndo = UndoManager.CanUndo;
        CanRedo = UndoManager.CanRedo;
    }

    /// <summary>世界被外部修改后刷新撤销状态.</summary>
    public void RefreshUndoState() => UpdateUndoState();

    public void Dispose()
    {
        UndoManager.Dispose();
        _worldEditor.Dispose();
    }
}
