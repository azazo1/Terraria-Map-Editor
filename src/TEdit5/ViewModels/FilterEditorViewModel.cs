using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using ReactiveUI.Primitives;
using TEdit.Terraria;
using TEdit5.Services;

namespace TEdit5.ViewModels;

/// <summary>
/// 过滤面板. 移植自 Windows 版的 FilterSidebarViewModel, 保留分类列表与隐藏/淡化选项.
/// </summary>
public partial class FilterEditorViewModel : ReactiveObject
{
    private string _filterText = string.Empty;
    private string _selectedCategory = "方块";

    public FilterEditorViewModel()
    {
        CategoryNames = new ObservableCollection<string> { "方块", "墙体", "液体", "电线" };
        ClearAllCommand = ReactiveCommand.Create(ClearAll);
        SelectAllCommand = ReactiveCommand.Create(SelectAll);
        BuildOptions();
    }

    public WorldFilter Filter { get; } = new();

    public ObservableCollection<string> CategoryNames { get; }

    /// <summary>当前分类下的选项.</summary>
    public ObservableCollection<FilterOption> Options { get; } = new();

    public ReactiveCommand<RxVoid, RxVoid> ClearAllCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SelectAllCommand { get; }

    public string FilterText
    {
        get => _filterText;
        set
        {
            this.RaiseAndSetIfChanged(ref _filterText, value);
            Refresh();
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCategory, value);
            Refresh();
        }
    }

    private void BuildOptions()
    {
        foreach (var tile in WorldConfiguration.TileBricks.OrderBy(t => t.Id))
        {
            var option = new FilterOption(tile.Id, tile.Name, "方块");
            Filter.AttachOption(option);
            Filter.TileOptions.Add(option);
        }

        foreach (var wall in WorldConfiguration.WallProperties.OrderBy(w => w.Id))
        {
            var option = new FilterOption(wall.Id, wall.Name, "墙体");
            Filter.AttachOption(option);
            Filter.WallOptions.Add(option);
        }

        foreach (var liquid in Enum.GetValues<LiquidType>())
        {
            var option = new FilterOption((int)liquid, liquid.ToString(), "液体");
            Filter.AttachOption(option);
            Filter.LiquidOptions.Add(option);
        }

        foreach (string wire in new[] { "红电线", "蓝电线", "绿电线", "黄电线" })
        {
            int id = Array.IndexOf(new[] { "红电线", "蓝电线", "绿电线", "黄电线" }, wire);
            var option = new FilterOption(id, wire, "电线");
            Filter.AttachOption(option);
            Filter.WireOptions.Add(option);
        }

        Refresh();
    }

    private void Refresh()
    {
        var source = SelectedCategory switch
        {
            "墙体" => Filter.WallOptions,
            "液体" => Filter.LiquidOptions,
            "电线" => Filter.WireOptions,
            _ => Filter.TileOptions,
        };

        string text = FilterText?.Trim() ?? string.Empty;
        Options.Clear();
        foreach (var option in source)
        {
            if (text.Length > 0
                && !option.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !option.Id.ToString().Contains(text, StringComparison.Ordinal))
            {
                continue;
            }

            Options.Add(option);
        }
    }

    private void ClearAll()
    {
        Filter.ClearAll();
        Refresh();
    }

    private void SelectAll()
    {
        foreach (var option in Options)
        {
            option.IsSelected = true;
        }

        Filter.Bump();
    }
}
