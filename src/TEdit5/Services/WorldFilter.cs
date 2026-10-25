using System.Collections.Generic;
using System.Linq;
using ReactiveUI;

namespace TEdit5.Services;

/// <summary>
/// 过滤列表里的一项.
/// </summary>
public class FilterOption : ReactiveObject
{
    private bool _isSelected;

    public FilterOption(int id, string name, string category)
    {
        Id = id;
        Name = name;
        Category = category;
    }

    public int Id { get; }

    public string Name { get; }

    public string Category { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _isSelected, value);
            SelectionChanged?.Invoke(this, System.EventArgs.Empty);
        }
    }

    /// <summary>选中状态变化时通知过滤器刷新.</summary>
    public event System.EventHandler? SelectionChanged;
}

/// <summary>
/// 世界渲染过滤器. 对应 Windows 版的 FilterManager, 但只保留渲染与剪贴板需要的部分,
/// 颜色相关的模式暂时固定为隐藏与淡化两种.
/// </summary>
public class WorldFilter : ReactiveObject
{
    private bool _hideFilteredTiles;
    private double _darkenAmount = 50;
    private double _desaturateAmount;

    public WorldFilter()
    {
        Revision = 0;
    }

    public List<FilterOption> TileOptions { get; } = new();

    public List<FilterOption> WallOptions { get; } = new();

    public List<FilterOption> LiquidOptions { get; } = new();

    public List<FilterOption> WireOptions { get; } = new();

    public List<FilterOption> SpriteOptions { get; } = new();

    /// <summary>每次过滤条件变化加一, 渲染端据此判断是否需要重建图块.</summary>
    public int Revision { get; private set; }

    public bool HideFilteredTiles
    {
        get => _hideFilteredTiles;
        set
        {
            this.RaiseAndSetIfChanged(ref _hideFilteredTiles, value);
            Bump();
        }
    }

    /// <summary>淡化量, 0 到 100.</summary>
    public double DarkenAmount
    {
        get => _darkenAmount;
        set
        {
            this.RaiseAndSetIfChanged(ref _darkenAmount, value);
            Bump();
        }
    }

    /// <summary>去色量, 0 到 100.</summary>
    public double DesaturateAmount
    {
        get => _desaturateAmount;
        set
        {
            this.RaiseAndSetIfChanged(ref _desaturateAmount, value);
            Bump();
        }
    }

    public bool IsTileFiltered(int id) => TileOptions.Any(o => o.Id == id && o.IsSelected);

    public bool IsWallFiltered(int id) => WallOptions.Any(o => o.Id == id && o.IsSelected);

    public bool IsLiquidFiltered(int id) => LiquidOptions.Any(o => o.Id == id && o.IsSelected);

    public bool IsWireFiltered(int id) => WireOptions.Any(o => o.Id == id && o.IsSelected);

    public bool IsSpriteFiltered(int id) => SpriteOptions.Any(o => o.Id == id && o.IsSelected);

    /// <summary>是否有任何过滤条件生效.</summary>
    public bool IsEnabled => TileOptions.Any(o => o.IsSelected)
        || WallOptions.Any(o => o.IsSelected)
        || LiquidOptions.Any(o => o.IsSelected)
        || WireOptions.Any(o => o.IsSelected)
        || SpriteOptions.Any(o => o.IsSelected);

    public void AttachOption(FilterOption option) => option.SelectionChanged += (_, _) => Bump();

    public void Bump()
    {
        Revision++;
        this.RaisePropertyChanged(nameof(Revision));
    }

    /// <summary>清空所有勾选.</summary>
    public void ClearAll()
    {
        foreach (var list in new[] { TileOptions, WallOptions, LiquidOptions, WireOptions, SpriteOptions })
        {
            foreach (var option in list)
            {
                option.IsSelected = false;
            }
        }

        Bump();
    }
}
