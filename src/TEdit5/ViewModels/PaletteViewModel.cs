using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using TEdit.Common;
using TEdit.Editor;
using TEdit.Terraria;

namespace TEdit5.ViewModels;

/// <summary>调色板里的一个条目: 方块或墙体.</summary>
public sealed class PaletteItem
{
    public PaletteItem(int id, string name, TEditColor color, string iconPrefix = "Tile")
    {
        Id = id;
        Name = name;
        IconPrefix = iconPrefix;
        Brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
    }

    /// <summary>方块或墙体的编号.</summary>
    public int Id { get; }

    /// <summary>名称, 已去掉模组前缀.</summary>
    public string Name { get; }

    /// <summary>色块颜色, 对应世界渲染里使用的颜色.</summary>
    public IBrush Brush { get; }

    public string DisplayName => $"{Name} ({Id})";

    /// <summary>图标文件名前缀, 与纹理目录里的命名对应.</summary>
    public string IconPrefix { get; }

    /// <summary>条目图标, 没有素材时为 null.</summary>
    public Avalonia.Media.Imaging.Bitmap? Icon => TEdit5.Controls.ItemIconCache.Get(Id, IconPrefix);
}

/// <summary>
/// 调色板. 列出可以涂抹的方块与墙体, 选中后写入共享的 TilePicker,
/// 这样所有文档的工具都会用同一个选择.
/// </summary>
public partial class PaletteViewModel : ReactiveObject
{
    private readonly TilePicker _tilePicker;
    private readonly List<PaletteItem> _allTiles = new();
    private readonly List<PaletteItem> _allWalls = new();

    private string _searchText = string.Empty;
    private PaletteItem? _selectedTile;
    private PaletteItem? _selectedWall;

    public ObservableCollection<PaletteItem> Tiles { get; } = new();

    public ObservableCollection<PaletteItem> Walls { get; } = new();

    /// <summary>按名称或编号过滤. 空字符串表示全部显示.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            this.RaisePropertyChanged();
            ApplyFilter();
        }
    }

    public PaletteItem? SelectedTile
    {
        get => _selectedTile;
        set
        {
            if (ReferenceEquals(_selectedTile, value))
            {
                return;
            }

            _selectedTile = value;
            this.RaisePropertyChanged();

            if (value != null)
            {
                _tilePicker.Tile = value.Id;
                _tilePicker.TileStyleActive = true;
                _tilePicker.WallStyleActive = false;
                _tilePicker.PaintMode = PaintMode.TileAndWall;
            }
        }
    }

    public PaletteItem? SelectedWall
    {
        get => _selectedWall;
        set
        {
            if (ReferenceEquals(_selectedWall, value))
            {
                return;
            }

            _selectedWall = value;
            this.RaisePropertyChanged();

            if (value != null)
            {
                _tilePicker.Wall = value.Id;
                _tilePicker.WallStyleActive = true;
                _tilePicker.TileStyleActive = false;
                _tilePicker.PaintMode = PaintMode.TileAndWall;
            }
        }
    }

    public PaletteViewModel(TilePicker tilePicker)
    {
        _tilePicker = tilePicker;

        foreach (var tile in WorldConfiguration.TileBricks.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            _allTiles.Add(new PaletteItem(tile.Id, tile.Name, tile.Color));
        }

        foreach (var wall in WorldConfiguration.WallProperties.OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase))
        {
            _allWalls.Add(new PaletteItem(wall.Id, wall.Name, wall.Color, "Wall"));
        }

        ApplyFilter();

        // 初始选中与 TilePicker 默认值保持一致.
        SelectedTile = _allTiles.FirstOrDefault(t => t.Id == _tilePicker.Tile) ?? _allTiles.FirstOrDefault();
    }

    private void ApplyFilter()
    {
        Fill(Tiles, _allTiles);
        Fill(Walls, _allWalls);
    }

    private void Fill(ObservableCollection<PaletteItem> target, List<PaletteItem> source)
    {
        target.Clear();

        string filter = _searchText.Trim();
        bool matchAll = filter.Length == 0;

        foreach (var item in source)
        {
            if (matchAll ||
                item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Id.ToString().Contains(filter, StringComparison.Ordinal))
            {
                target.Add(item);
            }
        }
    }
}
