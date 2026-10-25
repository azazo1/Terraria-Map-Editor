using ReactiveUI;

namespace TEdit5.ViewModels;

/// <summary>
/// 旗帜列表里的一项. 移植自 Windows 版的 BannerItem.
/// </summary>
public class BannerEntry : ReactiveObject
{
    private string _name = string.Empty;
    private string _category = string.Empty;
    private int _kills;
    private int _count;

    /// <summary>在世界的旗帜计数数组里的下标.</summary>
    public int TallyIndex { get; set; }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string Category
    {
        get => _category;
        set => this.RaiseAndSetIfChanged(ref _category, value);
    }

    public int NpcId { get; set; }

    public int BestiaryIndex { get; set; }

    /// <summary>击杀数, 只读展示.</summary>
    public int Kills
    {
        get => _kills;
        set => this.RaiseAndSetIfChanged(ref _kills, value);
    }

    /// <summary>已领取的旗帜数量, 可编辑.</summary>
    public int Count
    {
        get => _count;
        set => this.RaiseAndSetIfChanged(ref _count, value);
    }
}
