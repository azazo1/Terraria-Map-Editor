using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using ReactiveUI.Primitives;
using TEdit.Geometry;
using TEdit.Terraria;
using TEdit5.Services;

namespace TEdit5.ViewModels;

/// <summary>
/// NPC 名称面板的视图模型. 移植自 Windows 版 WorldViewModel 里的 NPC 相关部分,
/// 负责目录、地图上 NPC 的增删与跳转.
/// </summary>
public partial class NpcEditorViewModel : ReactiveObject
{
    private readonly DocumentViewModel _document;

    private readonly ObservableCollection<NpcEntry> _allNpcs = new();
    private readonly ObservableCollection<NpcEntry> _filtered = new();

    private string _searchText = string.Empty;

    private bool _onlyOnMap;

    /// <summary>搜索框文本, 变化时重新过滤列表.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            ApplyFilter();
        }
    }

    public NpcEditorViewModel(DocumentViewModel document)
    {
        _document = document;
        AddCommand = ReactiveCommand.Create<int>(Add);
        RemoveCommand = ReactiveCommand.Create<NpcEntry>(Remove);
        GoToCommand = ReactiveCommand.Create<NpcEntry>(GoTo);
    }

    /// <summary>界面上显示的列表, 受搜索框过滤.</summary>
    public ObservableCollection<NpcEntry> Npcs => _filtered;

    public ReactiveCommand<int, RxVoid> AddCommand { get; }

    public ReactiveCommand<NpcEntry, RxVoid> RemoveCommand { get; }

    public ReactiveCommand<NpcEntry, RxVoid> GoToCommand { get; }

    /// <summary>世界装载或 NPC 增删后重建列表.</summary>
    public void Refresh()
    {
        World? world = _document.World;
        int selectedId = _filtered.Count > 0 ? _filtered[0].SpriteId : -1;
        _filtered.Clear();
        _allNpcs.Clear();

        if (world == null)
        {
            return;
        }

        foreach (var pair in WorldConfiguration.NpcNames.OrderBy(x => x.Value))
        {
            WorldConfiguration.NpcById.TryGetValue(pair.Key, out var npcData);
            _allNpcs.Add(new NpcEntry(pair.Key, pair.Value, npcData?.Variants, npcData?.CanShimmer ?? false));
        }

        // 把地图上已有的 NPC 挂到目录项上, 同一类型有多个时补额外的行.
        foreach (var entry in _allNpcs.ToList())
        {
            entry.World = world;
            var worldNpcs = world.NPCs.Where(n => n.SpriteId == entry.SpriteId).ToList();
            entry.WorldNpc = worldNpcs.FirstOrDefault();

            if (worldNpcs.Count > 1)
            {
                WorldConfiguration.NpcById.TryGetValue(entry.SpriteId, out var npcData);
                int index = _allNpcs.IndexOf(entry);
                for (int i = 1; i < worldNpcs.Count; i++)
                {
                    var duplicate = new NpcEntry(
                        entry.SpriteId,
                        entry.DefaultName,
                        npcData?.Variants,
                        npcData?.CanShimmer ?? false,
                        instanceIndex: i)
                    {
                        World = world,
                        WorldNpc = worldNpcs[i],
                    };
                    _allNpcs.Insert(index + i, duplicate);
                }
            }
        }

        ApplyFilter();

        if (selectedId >= 0)
        {
            LogManager.Info($"NPC 列表已刷新, 共 {_allNpcs.Count} 项");
        }
    }

    /// <summary>只显示世界里已经存在的 NPC.</summary>
    public bool OnlyOnMap
    {
        get => _onlyOnMap;
        set
        {
            this.RaiseAndSetIfChanged(ref _onlyOnMap, value);
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        string text = SearchText?.Trim() ?? string.Empty;
        var matches = string.IsNullOrEmpty(text)
            ? _allNpcs
            : _allNpcs.Where(i => i.DefaultName.Contains(text, StringComparison.OrdinalIgnoreCase)
                || i.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase));

        if (OnlyOnMap)
        {
            matches = matches.Where(i => i.IsOnMap).ToList();
        }

        _filtered.Clear();
        foreach (var entry in matches.Where(i => i.IsOnMap).Concat(matches.Where(i => !i.IsOnMap)))
        {
            _filtered.Add(entry);
        }
    }

    private void Add(int spriteId)
    {
        World? world = _document.World;
        if (world == null || !WorldConfiguration.NpcNames.TryGetValue(spriteId, out string? name))
        {
            return;
        }

        if (world.NPCs.Any(n => n.SpriteId == spriteId))
        {
            return;
        }

        var spawn = new Vector2Int32(world.SpawnX, world.SpawnY);
        var npc = new NPC
        {
            Home = spawn,
            IsHomeless = true,
            DisplayName = name,
            Name = name,
            Position = new Vector2FloatObservable(spawn.X * 16, spawn.Y * 16),
            SpriteId = spriteId,
        };

        world.NPCs.Add(npc);
        Refresh();
        _document.MarkDirty();
        LogManager.Info($"添加 NPC: {name} ({spriteId})");
    }

    private void Remove(NpcEntry entry)
    {
        World? world = _document.World;
        if (world == null || entry.WorldNpc == null)
        {
            return;
        }

        world.NPCs.Remove(entry.WorldNpc);
        Refresh();
        _document.MarkDirty();
        LogManager.Info($"移除 NPC: {entry.DefaultName} ({entry.SpriteId})");
    }

    private void GoTo(NpcEntry entry)
    {
        if (entry.WorldNpc == null)
        {
            return;
        }

        int tileX = (int)(entry.WorldNpc.Position.X / 16);
        int tileY = (int)(entry.WorldNpc.Position.Y / 16);
        _document.RequestFocus(tileX, tileY);
    }
}
