using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using ReactiveUI.Primitives;
using TEdit.Terraria;
using TEdit5.Services;

namespace TEdit5.ViewModels;

/// <summary>
/// 图鉴面板. 移植自 Windows 版的 BestiaryViewModel, 去掉确认对话框与图标预览.
/// </summary>
public partial class BestiaryEditorViewModel : ReactiveObject
{
    private readonly DocumentViewModel _document;
    private readonly ObservableCollection<BestiaryEntry> _all = new();

    private string _filterText = string.Empty;

    private bool _onlyUnlocked;

    public BestiaryEditorViewModel(DocumentViewModel document)
    {
        _document = document;
        LoadCommand = ReactiveCommand.Create(Load);
        ResetCommand = ReactiveCommand.Create(Reset);
        CompleteCommand = ReactiveCommand.Create(Complete);
        SaveCommand = ReactiveCommand.Create(Save);
        UpdateKillTallyCommand = ReactiveCommand.Create(UpdateKillTally);
        Load();
    }

    /// <summary>过滤后的列表.</summary>
    public ObservableCollection<BestiaryEntry> Entries { get; } = new();

    public ReactiveCommand<RxVoid, RxVoid> LoadCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ResetCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> CompleteCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SaveCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> UpdateKillTallyCommand { get; }

    public string FilterText
    {
        get => _filterText;
        set
        {
            this.RaiseAndSetIfChanged(ref _filterText, value);
            ApplyFilter();
        }
    }

    /// <summary>只显示已经解锁的条目.</summary>
    public bool OnlyUnlocked
    {
        get => _onlyUnlocked;
        set
        {
            this.RaiseAndSetIfChanged(ref _onlyUnlocked, value);
            ApplyFilter();
        }
    }

    /// <summary>从世界读取图鉴数据.</summary>
    public void Load()
    {
        _all.Clear();
        World? world = _document.World;
        if (world == null)
        {
            ApplyFilter();
            return;
        }

        var data = WorldConfiguration.BestiaryData;
        foreach (string entity in data.BestiaryKilledIDs
                     .Union(data.BestiaryNearIDs)
                     .Union(data.BestiaryTalkedIDs)
                     .Distinct()
                     .OrderBy(e => e))
        {
            world.Bestiary.NPCKills.TryGetValue(entity, out int kills);
            bool near = world.Bestiary.NPCNear.Contains(entity);
            bool talked = world.Bestiary.NPCChat.Contains(entity);

            int npcId = 0;
            int index = 0;
            int stars = 1;
            if (data.NpcData.TryGetValue(entity, out var npcData))
            {
                npcId = npcData.Id;
                index = npcData.BestiaryDisplayIndex;
                stars = npcData.BestiaryStars;
            }

            _all.Add(new BestiaryEntry
            {
                Name = entity,
                NpcId = npcId,
                BestiaryIndex = index,
                BestiaryStars = stars,
                CanKill = data.BestiaryKilledIDs.Contains(entity),
                CanNear = data.BestiaryNearIDs.Contains(entity),
                CanTalk = data.BestiaryTalkedIDs.Contains(entity),
                Defeated = kills,
                Near = near,
                Talked = talked,
                Unlocked = kills > 0 || near || talked,
            });
        }

        ApplyFilter();
        LogManager.Info($"图鉴读取完成, 共 {_all.Count} 条");
    }

    private void ApplyFilter()
    {
        string text = FilterText?.Trim() ?? string.Empty;
        Entries.Clear();

        foreach (var entry in _all)
        {
            if (OnlyUnlocked && !entry.Unlocked)
            {
                continue;
            }

            if (text.Length > 0
                && !entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !entry.NpcId.ToString().Contains(text, StringComparison.Ordinal)
                && !entry.BestiaryIndex.ToString().Contains(text, StringComparison.Ordinal))
            {
                continue;
            }

            Entries.Add(entry);
        }
    }

    private void Reset()
    {
        World? world = _document.World;
        if (world == null)
        {
            return;
        }

        world.Bestiary = new Bestiary();
        world.KilledMobs.Clear();
        _document.MarkDirty();
        Load();
        LogManager.Info("图鉴已重置");
    }

    private void Complete()
    {
        World? world = _document.World;
        if (world == null)
        {
            return;
        }

        var data = WorldConfiguration.BestiaryData;
        var edits = new Bestiary();

        foreach (string entity in data.BestiaryKilledIDs)
        {
            world.Bestiary.NPCKills.TryGetValue(entity, out int kills);
            edits.NPCKills[entity] = kills < 150 ? 150 : kills;
        }

        foreach (string entity in data.BestiaryTalkedIDs)
        {
            edits.NPCChat.Add(entity);
        }

        foreach (string entity in data.BestiaryNearIDs)
        {
            edits.NPCNear.Add(entity);
        }

        world.Bestiary = edits;
        _document.MarkDirty();
        Load();
        LogManager.Info("图鉴已补全");
    }

    private void Save()
    {
        World? world = _document.World;
        if (world == null)
        {
            return;
        }

        var edits = new Bestiary();
        foreach (var entry in _all)
        {
            if (entry.Defeated > 0)
            {
                edits.NPCKills[entry.Name] = entry.Defeated;
            }

            if (entry.Talked)
            {
                edits.NPCChat.Add(entry.Name);
            }

            if (entry.Near)
            {
                edits.NPCNear.Add(entry.Name);
            }
        }

        world.Bestiary = edits;
        _document.MarkDirty();
        Load();
        LogManager.Info("图鉴已写回世界");
    }

    private void UpdateKillTally()
    {
        World? world = _document.World;
        if (world == null)
        {
            return;
        }

        for (int i = 0; i < world.KilledMobs.Count; i++)
        {
            world.KilledMobs[i] = 0;
        }

        var data = WorldConfiguration.BestiaryData;
        foreach (var entry in _all)
        {
            if (data.NpcData.TryGetValue(entry.Name, out var npcData)
                && npcData.BannerId >= 0
                && world.KilledMobs.Count > npcData.BannerId)
            {
                world.KilledMobs[npcData.BannerId] += entry.Defeated;
            }
        }

        _document.MarkDirty();
        LogManager.Info("击杀统计已刷新");
    }
}
