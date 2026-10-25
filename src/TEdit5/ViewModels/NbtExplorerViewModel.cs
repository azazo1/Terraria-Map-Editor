using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using ReactiveUI.Primitives;
using TEdit.Common.IO;
using TEdit5.Services;

namespace TEdit5.ViewModels;

/// <summary>
/// NBT 浏览器. 移植自 Windows 版的 NbtExplorerView, 展示世界里的 TwldData 树,
/// 支持按名字搜索与叶子值修改; 坐标跳转与实体编辑暂缺.
/// </summary>
public partial class NbtExplorerViewModel : ReactiveObject
{
    private readonly DocumentViewModel _document;

    private string _searchText = string.Empty;
    private NbtNode? _selectedNode;

    public NbtExplorerViewModel(DocumentViewModel document)
    {
        _document = document;
        RefreshCommand = ReactiveCommand.Create(Refresh);
        ZoomToSelectedCommand = ReactiveCommand.Create(ZoomToSelected);
        Refresh();
    }

    public ObservableCollection<NbtNode> RootNodes { get; } = new();

    public ReactiveCommand<RxVoid, RxVoid> RefreshCommand { get; }

    /// <summary>跳到当前选中根标签对应的方块位置.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ZoomToSelectedCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            Refresh();
        }
    }

    public NbtNode? SelectedNode
    {
        get => _selectedNode;
        set => this.RaiseAndSetIfChanged(ref _selectedNode, value);
    }

    public string Summary => RootNodes.Count == 0
        ? "这个存档没有可浏览的 NBT 数据"
        : $"共 {RootNodes.Count} 个根标签";

    private void Refresh()
    {
        RootNodes.Clear();
        SelectedNode = null;

        var world = _document.World;
        if (world == null)
        {
            this.RaisePropertyChanged(nameof(Summary));
            return;
        }

        string text = SearchText?.Trim() ?? string.Empty;

        // NBT 数据挂在带模组信息的物品上, 这里把箱子与图格实体里的都收进来.
        void AddRoot(string name, TagCompound? compound)
        {
            if (compound == null || compound.Count == 0)
            {
                return;
            }

            var node = new NbtNode(name, compound, null);
            if (text.Length > 0 && !Matches(node, text))
            {
                return;
            }

            RootNodes.Add(node);
        }

        foreach (var chest in world.Chests)
        {
            for (int i = 0; i < chest.Items.Count; i++)
            {
                var item = chest.Items[i];
                AddRoot($"箱子 ({chest.X}, {chest.Y}) 物品 {i}", item.ModItemData);
            }
        }

        foreach (var entity in world.TileEntities)
        {
            for (int i = 0; i < entity.Items.Count; i++)
            {
                var slot = entity.Items[i];
                AddRoot($"实体 ({entity.PosX}, {entity.PosY}) 槽 {i}", slot.ModItemData);
            }
        }

        this.RaisePropertyChanged(nameof(Summary));
        LogManager.Info($"NBT 浏览器刷新, 根标签 {RootNodes.Count} 个");
    }

    private void ZoomToSelected()
    {
        var node = SelectedNode;
        if (node?.Parent != null || _document.World == null)
        {
            return;
        }

        // 根标签的名字形如 "箱子 (12, 34) 物品 0", 从中取出坐标.
        int open = node.Name.IndexOf('(');
        int close = node.Name.IndexOf(')');
        if (open < 0 || close <= open)
        {
            LogManager.Info("这个根标签没有坐标信息, 无法跳转");
            return;
        }

        string[] parts = node.Name[(open + 1)..close].Split(',');
        if (parts.Length != 2
            || !int.TryParse(parts[0].Trim(), out int x)
            || !int.TryParse(parts[1].Trim(), out int y))
        {
            LogManager.Info("根标签里的坐标无法解析");
            return;
        }

        _document.RequestFocus(x, y);
        LogManager.Info($"NBT 浏览器跳到 ({x}, {y})");
    }

    private static bool Matches(NbtNode node, string text)
        => node.Matches(text) || node.Children.Any(child => Matches(child, text));
}
