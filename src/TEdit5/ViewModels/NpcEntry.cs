using System.Collections.Generic;
using ReactiveUI;
using TEdit.Terraria;

namespace TEdit5.ViewModels;

/// <summary>
/// NPC 列表中一项. 移植自 Windows 版的 NpcListItem, 去掉了精灵图预览.
/// </summary>
public class NpcEntry : ReactiveObject
{
    public int SpriteId { get; }

    /// <summary>NPC 头像, 来自随包内置的游戏图集, 取不到时为 null.</summary>
    public Avalonia.Media.Imaging.Bitmap? IconBitmap => Controls.ItemIconCache.Get(SpriteId, "NPC");

    public string DefaultName { get; }

    public List<string>? AvailableVariants { get; }

    public bool CanShimmer { get; }

    public bool HasVariants => AvailableVariants?.Count > 1;

    public int InstanceIndex { get; }

    public bool IsDuplicateInstance => InstanceIndex >= 0;

    private World? _world;

    public World? World
    {
        get => _world;
        set
        {
            this.RaiseAndSetIfChanged(ref _world, value);
            this.RaisePropertyChanged(nameof(IsShimmered));
            this.RaisePropertyChanged(nameof(IsPartying));
        }
    }

    private NPC? _worldNpc;

    public NPC? WorldNpc
    {
        get => _worldNpc;
        set
        {
            this.RaiseAndSetIfChanged(ref _worldNpc, value);
            this.RaisePropertyChanged(nameof(IsOnMap));
            this.RaisePropertyChanged(nameof(VariantIndex));
            this.RaisePropertyChanged(nameof(DisplayName));
        }
    }

    public bool IsOnMap => WorldNpc != null;

    /// <summary>界面上可编辑的显示名.</summary>
    public string DisplayName
    {
        get => WorldNpc?.DisplayName ?? DefaultName;
        set
        {
            if (WorldNpc != null && WorldNpc.DisplayName != value)
            {
                WorldNpc.DisplayName = value;
                this.RaisePropertyChanged();
            }
        }
    }

    public int VariantIndex
    {
        get => WorldNpc?.TownNpcVariationIndex ?? 0;
        set
        {
            if (WorldNpc != null && WorldNpc.TownNpcVariationIndex != value)
            {
                WorldNpc.TownNpcVariationIndex = value;
                this.RaisePropertyChanged();
            }
        }
    }

    public bool IsShimmered
    {
        get => _world != null
            && SpriteId >= 0
            && SpriteId < _world.ShimmeredTownNPCs.Count
            && _world.ShimmeredTownNPCs[SpriteId] != 0;
        set
        {
            if (_world == null || SpriteId < 0)
            {
                return;
            }

            while (_world.ShimmeredTownNPCs.Count <= SpriteId)
            {
                _world.ShimmeredTownNPCs.Add(0);
            }

            _world.ShimmeredTownNPCs[SpriteId] = value ? 1 : 0;
            this.RaisePropertyChanged();
        }
    }

    public bool IsPartying
    {
        get => _world?.PartyingNPCs.Contains(SpriteId) ?? false;
        set
        {
            if (_world == null)
            {
                return;
            }

            bool current = _world.PartyingNPCs.Contains(SpriteId);
            if (value && !current)
            {
                _world.PartyingNPCs.Add(SpriteId);
            }
            else if (!value && current)
            {
                _world.PartyingNPCs.Remove(SpriteId);
            }

            this.RaisePropertyChanged();
        }
    }

    public NpcEntry(int spriteId, string defaultName, List<string>? variants = null, bool canShimmer = false, int instanceIndex = -1)
    {
        SpriteId = spriteId;
        DefaultName = defaultName;
        AvailableVariants = variants;
        CanShimmer = canShimmer;
        InstanceIndex = instanceIndex;
    }
}
