using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using ReactiveUI.Primitives;
using TEdit.Terraria;
using TEdit5.Services;

namespace TEdit5.ViewModels;

/// <summary>
/// 特殊方块面板的状态. 移植自 Windows 版 WorldViewModel 里 SelectedSpecialTile
/// 相关的逻辑, 目前实现了指引与告示牌两类编辑器.
/// </summary>
public partial class SpecialTileViewModel : ReactiveObject
{
    private readonly DocumentViewModel _document;

    [Reactive] private int _selectedSpecialTile = -1;
    [Reactive] private Sign? _selectedSign;
    [Reactive] private Chest? _selectedChest;
    [Reactive] private TileEntity? _selectedTileEntity;
    [Reactive] private string _signText = string.Empty;
    [Reactive] private Item? _selectedItem;
    [Reactive] private TileEntityItem? _selectedSlot;
    [Reactive] private int _newItemId = 1;

    private List<(int NetId, int StackSize, byte Prefix)> _chestSnapshot = new();
    private TileEntity? _entitySnapshot;

    public SpecialTileViewModel(DocumentViewModel document)
    {
        _document = document;
        SaveSignCommand = ReactiveCommand.Create<bool>(SaveSign);
        SaveChestCommand = ReactiveCommand.Create<bool>(SaveChest);
        AddItemCommand = ReactiveCommand.Create(AddChestItem);
        RemoveItemCommand = ReactiveCommand.Create(RemoveChestItem);
        MaxStackItemCommand = ReactiveCommand.Create<Item>(MaxStackItem);
        ClearChestCommand = ReactiveCommand.Create(ClearChest);
        SaveTileEntityCommand = ReactiveCommand.Create<bool>(SaveTileEntity);
        MaxStackEntityCommand = ReactiveCommand.Create<TileEntityItem>(MaxStackEntityItem);
    }

    public ReactiveCommand<bool, RxVoid> SaveSignCommand { get; }

    public ReactiveCommand<bool, RxVoid> SaveChestCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> AddItemCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RemoveItemCommand { get; }

    public ReactiveCommand<Item, RxVoid> MaxStackItemCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearChestCommand { get; }

    public ReactiveCommand<bool, RxVoid> SaveTileEntityCommand { get; }

    public ReactiveCommand<TileEntityItem, RxVoid> MaxStackEntityCommand { get; }

    /// <summary>当前是否选中了图格实体.</summary>
    public bool HasEntity => SelectedTileEntity != null;

    public string EntityLocation => SelectedTileEntity == null
        ? string.Empty
        : $"({SelectedTileEntity.PosX}, {SelectedTileEntity.PosY})";

    /// <summary>物品框类实体, 只有单件物品.</summary>
    public bool ShowEntityFrame => SelectedTileEntity != null && SelectedTileEntity.EntityType == TileEntityType.ItemFrame;

    /// <summary>带装备槽的实体 (模特, 武器架, 帽子架等).</summary>
    public bool ShowEntitySlots => SelectedTileEntity != null
        && SelectedTileEntity.EntityType != TileEntityType.ItemFrame
        && SelectedTileEntity.Items.Count > 0;

    public bool ShowInstructions => SelectedSpecialTile == -1;

    public bool ShowSign => SelectedSpecialTile == 12;

    public bool ShowChest => SelectedSpecialTile == 13;

    /// <summary>其余类型 (物品框, 模特, 武器架, 帽子架, 盘子等) 暂未移植编辑器.</summary>
    public bool ShowUnported => SelectedSpecialTile > 0 && SelectedSpecialTile != 12 && SelectedSpecialTile != 13;

    public string SignLocation => SelectedSign == null
        ? string.Empty
        : $"({SelectedSign.X}, {SelectedSign.Y})";

    /// <summary>直接选中一个箱子, 供查找结果跳转使用.</summary>
    public void SelectChest(Chest chest)
    {
        SelectedSign = null;
        SelectedTileEntity = null;
        SelectedChest = chest;
        SelectedSpecialTile = 13;
        SelectedItem = null;
        _chestSnapshot = chest.Items.Select(i => (i.NetId, i.StackSize, i.Prefix)).ToList();
        NotifyVisibility();
    }

    /// <summary>按光标所在方块刷新当前选中的特殊方块.</summary>
    public void UpdateAtCursor()
    {
        World? world = _document.World;
        if (world == null)
        {
            SelectedSpecialTile = -1;
            return;
        }

        int x = (int)_document.CursorTileCoordinate.X;
        int y = (int)_document.CursorTileCoordinate.Y;

        var sign = world.GetSignAtTile(x, y, true);
        var chest = world.GetChestAtTile(x, y, true);
        var entity = world.GetTileEntityAtTile(x, y, true);

        SelectedSign = sign;
        SelectedChest = chest;
        SelectedTileEntity = entity;

        SelectedSpecialTile = sign != null
            ? 12
            : chest != null
                ? 13
                : entity != null
                    ? (int)entity.EntityType
                    : -1;

        SignText = sign?.Text ?? string.Empty;
        SelectedItem = null;
        _entitySnapshot = entity?.Copy();
        _chestSnapshot = chest?.Items.Select(i => (i.NetId, i.StackSize, i.Prefix)).ToList()
            ?? new List<(int NetId, int StackSize, byte Prefix)>();
        NotifyVisibility();
    }

    private void SaveSign(bool save)
    {
        if (SelectedSign == null)
        {
            return;
        }

        if (save)
        {
            SelectedSign.Text = SignText;
            _document.MarkDirty();
            LogManager.Info($"保存告示牌 ({SelectedSign.X}, {SelectedSign.Y})");
        }
        else
        {
            SignText = SelectedSign.Text;
        }

        NotifyVisibility();
    }

    private void AddChestItem()
    {
        if (SelectedChest == null)
        {
            return;
        }

        var item = new Item { NetId = NewItemId, StackSize = 1 };
        SelectedChest.Items.Add(item);
        SelectedItem = item;
    }

    private void RemoveChestItem()
    {
        if (SelectedChest == null || SelectedItem == null)
        {
            return;
        }

        SelectedChest.Items.Remove(SelectedItem);
        SelectedItem = null;
    }

    private void MaxStackItem(Item? item)
    {
        if (item == null)
        {
            return;
        }

        int max = 999;
        var property = WorldConfiguration.ItemProperties.FirstOrDefault(p => p.Id == item.NetId);
        if (property != null && property.MaxStackSize > 0)
        {
            max = property.MaxStackSize;
        }

        item.StackSize = max;
    }

    private void ClearChest()
    {
        SelectedChest?.Items.Clear();
        SelectedItem = null;
    }

    private void SaveChest(bool save)
    {
        if (SelectedChest == null)
        {
            return;
        }

        if (save)
        {
            _document.MarkDirty();
            _chestSnapshot = SelectedChest.Items.Select(i => (i.NetId, i.StackSize, i.Prefix)).ToList();
            LogManager.Info($"保存箱子 ({SelectedChest.X}, {SelectedChest.Y}), 物品 {SelectedChest.Items.Count} 件");
            return;
        }

        SelectedChest.Items.Clear();
        foreach (var (netId, stackSize, prefix) in _chestSnapshot)
        {
            SelectedChest.Items.Add(new Item { NetId = netId, StackSize = stackSize, Prefix = prefix });
        }
    }

    private void MaxStackEntityItem(TileEntityItem? item)
    {
        if (item == null)
        {
            return;
        }

        int max = 999;
        var property = WorldConfiguration.ItemProperties.FirstOrDefault(p => p.Id == item.Id);
        if (property != null && property.MaxStackSize > 0)
        {
            max = property.MaxStackSize;
        }

        item.StackSize = (short)max;
    }

    private void SaveTileEntity(bool save)
    {
        if (SelectedTileEntity == null)
        {
            return;
        }

        if (save)
        {
            _document.MarkDirty();
            _entitySnapshot = SelectedTileEntity.Copy();
            LogManager.Info($"保存图格实体 {SelectedTileEntity.EntityType} 于 ({SelectedTileEntity.PosX}, {SelectedTileEntity.PosY})");
            return;
        }

        if (_entitySnapshot == null)
        {
            return;
        }

        SelectedTileEntity.NetId = _entitySnapshot.NetId;
        SelectedTileEntity.StackSize = _entitySnapshot.StackSize;
        SelectedTileEntity.Prefix = _entitySnapshot.Prefix;
        CopySlots(_entitySnapshot.Items, SelectedTileEntity.Items);
        CopySlots(_entitySnapshot.Dyes, SelectedTileEntity.Dyes);
        CopySlots(_entitySnapshot.Misc, SelectedTileEntity.Misc);
    }

    private static void CopySlots(
        System.Collections.ObjectModel.ObservableCollection<TileEntityItem> source,
        System.Collections.ObjectModel.ObservableCollection<TileEntityItem> target)
    {
        target.Clear();
        foreach (var slot in source)
        {
            target.Add(new TileEntityItem
            {
                Id = slot.Id,
                StackSize = slot.StackSize,
                Prefix = slot.Prefix,
            });
        }
    }

    private void NotifyVisibility()
    {
        this.RaisePropertyChanged(nameof(ShowInstructions));
        this.RaisePropertyChanged(nameof(ShowSign));
        this.RaisePropertyChanged(nameof(ShowChest));
        this.RaisePropertyChanged(nameof(ShowUnported));
        this.RaisePropertyChanged(nameof(HasEntity));
        this.RaisePropertyChanged(nameof(EntityLocation));
        this.RaisePropertyChanged(nameof(ShowEntityFrame));
        this.RaisePropertyChanged(nameof(ShowEntitySlots));
        this.RaisePropertyChanged(nameof(SignLocation));
    }
}
