using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using ReactiveUI.Primitives;
using TEdit.Terraria;
using TEdit.Terraria.Objects;
using TEdit5.Services;

namespace TEdit5.ViewModels;

/// <summary>
/// 精灵面板. 移植自 Windows 版的 SpriteView2, 列出方块精灵表与样式;
/// 缩略图依赖游戏素材渲染, 这里用文字信息代替.
/// </summary>
public partial class SpriteEditorViewModel : ReactiveObject
{
    private string _spriteFilter = string.Empty;
    private SpriteSheet? _selectedSpriteSheet;
    private double _thumbSize = 64;
    private TEdit.Terraria.World? _world;
    private Avalonia.Media.Imaging.Bitmap? _previewBitmap;
    private int _previewTiles = 16;

    private readonly DocumentViewModel? _document;

    public SpriteEditorViewModel(DocumentViewModel? document = null)
    {
        _document = document;
        JumpToSpriteCommand = ReactiveCommand.Create(JumpToSprite);

        ClearSelectionCommand = ReactiveCommand.Create(() =>
        {
            SelectedSpriteSheet = null;
            Styles.Clear();
        });

        Load();
    }

    /// <summary>当前世界, 用于画预览图.</summary>
    public TEdit.Terraria.World? World
    {
        get => _world;
        set
        {
            _world = value;
            RefreshPreview();
        }
    }

    /// <summary>预览覆盖的格数, 8 到 64.</summary>
    public int PreviewTiles
    {
        get => _previewTiles;
        set
        {
            int clamped = Math.Clamp(value, 8, 64);
            if (_previewTiles == clamped)
            {
                return;
            }

            _previewTiles = clamped;
            this.RaisePropertyChanged();
            RefreshPreview();
        }
    }

    /// <summary>当前精灵表的预览图, 找不到实例时为空.</summary>
    public Avalonia.Media.Imaging.Bitmap? PreviewBitmap
    {
        get => _previewBitmap;
        private set => this.RaiseAndSetIfChanged(ref _previewBitmap, value);
    }

    /// <summary>过滤后的精灵表.</summary>
    public ObservableCollection<SpriteSheet> Sheets { get; } = new();

    /// <summary>当前精灵表的样式.</summary>
    public ObservableCollection<SpriteItem> Styles { get; } = new();

    public ReactiveCommand<RxVoid, RxVoid> ClearSelectionCommand { get; }

    /// <summary>把视口移到该精灵的第一处实例.</summary>
    public ReactiveCommand<RxVoid, RxVoid> JumpToSpriteCommand { get; }

    private void JumpToSprite()
    {
        var sheet = SelectedSpriteSheet;
        if (_world == null || sheet == null || _document == null)
        {
            return;
        }

        if (!TryFindTile(sheet.Tile, out int x, out int y))
        {
            Services.LogManager.Warn("世界里还没有这种方块, 无法定位");
            return;
        }

        _document.RequestFocus(x, y);
    }

    public string SpriteFilter
    {
        get => _spriteFilter;
        set
        {
            this.RaiseAndSetIfChanged(ref _spriteFilter, value);
            Load();
        }
    }

    public SpriteSheet? SelectedSpriteSheet
    {
        get => _selectedSpriteSheet;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSpriteSheet, value);
            Styles.Clear();
            RefreshPreview();
            if (value != null)
            {
                foreach (var style in value.Styles)
                {
                    Styles.Add(style);
                }
            }
        }
    }

    /// <summary>缩略图尺寸, 原版用于控制网格大小.</summary>
    public double ThumbnailSize
    {
        get => _thumbSize;
        set => this.RaiseAndSetIfChanged(ref _thumbSize, value);
    }

    public string Summary => $"{Sheets.Count} 个精灵表";

    private void RefreshPreview()
    {
        PreviewBitmap = null;
        var sheet = SelectedSpriteSheet;
        if (_world == null || sheet == null)
        {
            return;
        }

        if (!TryFindTile(sheet.Tile, out int x, out int y))
        {
            return;
        }

        int span = _previewTiles;
        using var bitmap = Services.WorldThumbnailRenderer.Render(_world, x - span / 2, y - span / 2, span, span);
        if (bitmap == null)
        {
            return;
        }

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var stream = new System.IO.MemoryStream(data.ToArray());
        PreviewBitmap = new Avalonia.Media.Imaging.Bitmap(stream);
    }

    private bool TryFindTile(int tileId, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (_world == null)
        {
            return false;
        }

        int width = _world.Tiles.GetLength(0);
        int height = _world.Tiles.GetLength(1);
        for (int ty = 0; ty < height; ty++)
        {
            for (int tx = 0; tx < width; tx++)
            {
                var tile = _world.Tiles[tx, ty];
                if (tile.IsActive && tile.Type == tileId)
                {
                    x = tx;
                    y = ty;
                    return true;
                }
            }
        }

        return false;
    }

    private void Load()
    {
        Sheets.Clear();
        string text = SpriteFilter?.Trim() ?? string.Empty;

        foreach (var sheet in WorldConfiguration.Sprites2
                     .Where(s => text.Length == 0
                         || s.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                         || s.Tile.ToString().Contains(text, StringComparison.Ordinal))
                     .OrderBy(s => s.Tile))
        {
            Sheets.Add(sheet);
        }

        this.RaisePropertyChanged(nameof(Summary));
        LogManager.Info($"精灵面板载入 {Sheets.Count} 个精灵表");
    }
}
