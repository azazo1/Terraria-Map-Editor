using System;
using ReactiveUI;

namespace TEdit5.ViewModels;

/// <summary>
/// 图鉴列表里的一项. 移植自 Windows 版的 BestiaryItem.
/// </summary>
public class BestiaryEntry : ReactiveObject
{
    private string _name = string.Empty;
    private int _defeated;
    private bool _unlocked;
    private bool _near;
    private bool _talked;
    private bool _canTalk;
    private bool _canNear;
    private bool _canKill = true;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    /// <summary>NPC 编号, 用于未来显示图标.</summary>
    public int NpcId { get; set; }

    /// <summary>游戏内图鉴显示的序号.</summary>
    public int BestiaryIndex { get; set; }

    /// <summary>星级 (1 到 5).</summary>
    public int BestiaryStars { get; set; }

    public int Defeated
    {
        get => _defeated;
        set => this.RaiseAndSetIfChanged(ref _defeated, CanKill ? value : 0);
    }

    public bool Unlocked
    {
        get => _unlocked;
        set => this.RaiseAndSetIfChanged(ref _unlocked, value);
    }

    public bool Near
    {
        get => _near;
        set => this.RaiseAndSetIfChanged(ref _near, CanNear && value);
    }

    public bool Talked
    {
        get => _talked;
        set => this.RaiseAndSetIfChanged(ref _talked, CanTalk && value);
    }

    public bool CanTalk
    {
        get => _canTalk;
        set
        {
            this.RaiseAndSetIfChanged(ref _canTalk, value);
            if (!value)
            {
                Talked = false;
            }
        }
    }

    public bool CanNear
    {
        get => _canNear;
        set
        {
            this.RaiseAndSetIfChanged(ref _canNear, value);
            if (!value)
            {
                Near = false;
            }
        }
    }

    public bool CanKill
    {
        get => _canKill;
        set
        {
            this.RaiseAndSetIfChanged(ref _canKill, value);
            if (!value)
            {
                Defeated = 0;
            }
        }
    }
}
