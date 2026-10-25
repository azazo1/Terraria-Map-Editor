using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using ReactiveUI.Primitives;
using TEdit.Terraria;
using TEdit5.Services;

namespace TEdit5.ViewModels;

/// <summary>
/// 旗帜面板. 移植自 Windows 版的 BannerViewModel, 导入功能需要文件对话框, 暂未接线.
/// </summary>
public partial class BannerEditorViewModel : ReactiveObject
{
    private readonly DocumentViewModel _document;
    private readonly ObservableCollection<BannerEntry> _all = new();

    private string _filterText = string.Empty;

    private bool _onlyOwned;
    private string _selectedCategory = "全部";

    public BannerEditorViewModel(DocumentViewModel document)
    {
        _document = document;
        Categories = new ObservableCollection<string> { "全部" };
        MaxOutCommand = ReactiveCommand.Create(MaxOut);
        SaveCommand = ReactiveCommand.Create(Save);
        ResetCommand = ReactiveCommand.Create(Reset);
        Populate();
    }

    public ObservableCollection<BannerEntry> Entries { get; } = new();

    public ObservableCollection<string> Categories { get; }

    public ReactiveCommand<RxVoid, RxVoid> MaxOutCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SaveCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ResetCommand { get; }

    public string FilterText
    {
        get => _filterText;
        set
        {
            this.RaiseAndSetIfChanged(ref _filterText, value);
            ApplyFilter();
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCategory, value);
            ApplyFilter();
        }
    }

    /// <summary>从世界读取旗帜数据.</summary>
    public void Populate()
    {
        _all.Clear();
        Entries.Clear();
        Categories.Clear();
        Categories.Add("全部");

        World? world = _document.World;
        if (world == null)
        {
            return;
        }

        var npcByBanner = WorldConfiguration.BestiaryData.NpcById.Values
            .Where(n => n.BannerId >= 0)
            .GroupBy(n => n.BannerId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var pair in WorldConfiguration.TallyNames.OrderBy(k => k.Key))
        {
            int tallyIndex = pair.Key;
            string bannerName = pair.Value;

            string category = string.Empty;
            int npcId = 0;
            int bestiaryIndex = 0;
            if (npcByBanner.TryGetValue(tallyIndex, out var npcData))
            {
                category = npcData.Category ?? string.Empty;
                npcId = npcData.Id;
                bestiaryIndex = npcData.BestiaryDisplayIndex;
            }

            int count = 0;
            if (tallyIndex >= 0 && tallyIndex < world.ClaimableBanners.Count)
            {
                count = world.ClaimableBanners[tallyIndex];
            }

            int kills = 0;
            if (tallyIndex >= 0 && tallyIndex < world.KilledMobs.Count)
            {
                kills = world.KilledMobs[tallyIndex];
            }

            _all.Add(new BannerEntry
            {
                TallyIndex = tallyIndex,
                Name = bannerName,
                Category = string.IsNullOrEmpty(category) ? "其他" : category,
                NpcId = npcId,
                BestiaryIndex = bestiaryIndex,
                Kills = kills,
                Count = count,
            });
        }

        foreach (string category in _all.Select(b => b.Category).Distinct().OrderBy(c => c))
        {
            if (!Categories.Contains(category))
            {
                Categories.Add(category);
            }
        }

        ApplyFilter();
        LogManager.Info($"旗帜列表读取完成, 共 {_all.Count} 项");
    }

    /// <summary>只显示世界里已经拥有的旗帜.</summary>
    public bool OnlyOwned
    {
        get => _onlyOwned;
        set
        {
            this.RaiseAndSetIfChanged(ref _onlyOwned, value);
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        string text = FilterText?.Trim() ?? string.Empty;
        Entries.Clear();

        foreach (var entry in _all)
        {
            if (OnlyOwned && entry.Count <= 0)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(SelectedCategory)
                && SelectedCategory != "全部"
                && !string.Equals(entry.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (text.Length > 0
                && !entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !entry.Category.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !entry.NpcId.ToString().Contains(text, StringComparison.Ordinal)
                && !entry.BestiaryIndex.ToString().Contains(text, StringComparison.Ordinal))
            {
                continue;
            }

            Entries.Add(entry);
        }
    }

    private void MaxOut()
    {
        foreach (var entry in _all)
        {
            entry.Count = 9999;
        }

        LogManager.Info("旗帜数量已全部设为上限");
    }

    private void Reset()
    {
        foreach (var entry in _all)
        {
            entry.Count = 0;
        }

        LogManager.Info("旗帜数量已清零");
    }

    private void Save()
    {
        World? world = _document.World;
        if (world == null)
        {
            return;
        }

        foreach (var entry in _all)
        {
            if (entry.TallyIndex >= 0 && entry.TallyIndex < world.ClaimableBanners.Count)
            {
                world.ClaimableBanners[entry.TallyIndex] = (ushort)Math.Clamp(entry.Count, 0, ushort.MaxValue);
            }
        }

        _document.MarkDirty();
        Populate();
        LogManager.Info("旗帜数据已写回世界");
    }
}
