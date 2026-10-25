using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Primitives;
using TEdit.Terraria;
using TEdit5.Services;

namespace TEdit5.ViewModels;

/// <summary>查找结果里的一条.</summary>
public class FindResult
{
    public FindResult(int x, int y, string displayText, TEdit.Terraria.Chest? chest = null)
    {
        X = x;
        Y = y;
        DisplayText = displayText;
        Chest = chest;
    }

    /// <summary>命中的箱子, 选中该结果时可以直接打开箱子编辑器.</summary>
    public TEdit.Terraria.Chest? Chest { get; }

    public int X { get; }

    public int Y { get; }

    public string DisplayText { get; }
}

/// <summary>
/// 查找面板. 移植自 Windows 版的 FindSidebarViewModel, 支持按箱子名, 方块编号,
/// 墙体编号查找, 精灵查找暂缺.
/// </summary>
public partial class FindEditorViewModel : ReactiveObject
{
    private readonly DocumentViewModel _document;

    private string _searchText = string.Empty;
    private int _selectedTabIndex;
    private FindResult? _selectedResult;
    private string _summary = "尚未查找";
    private bool _isSearching;

    public FindEditorViewModel(DocumentViewModel document)
    {
        _document = document;
        ExecuteSearchCommand = ReactiveCommand.Create(ExecuteSearch);
        ClearCommand = ReactiveCommand.Create(ClearResults);
        NavigatePreviousCommand = ReactiveCommand.Create(() => Navigate(-1));
        NavigateNextCommand = ReactiveCommand.Create(() => Navigate(1));
    }

    public ObservableCollection<FindResult> Results { get; } = new();

    public ReactiveCommand<RxVoid, RxVoid> ExecuteSearchCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> NavigatePreviousCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> NavigateNextCommand { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public FindResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedResult, value);
            if (value != null)
            {
                _document.RequestFocus(value.X, value.Y);

                if (value.Chest != null)
                {
                    _document.SpecialTiles.SelectChest(value.Chest);
                    _document.SelectedPanelIndex = 2;
                }

                UpdateSummary();
            }
        }
    }

    public string Summary
    {
        get => _summary;
        private set => this.RaiseAndSetIfChanged(ref _summary, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set => this.RaiseAndSetIfChanged(ref _isSearching, value);
    }

    public string SelectionSummary => _document.Selection.IsActive
        ? $"选区 {_document.Selection.SelectionArea.Width} x {_document.Selection.SelectionArea.Height}"
        : "没有选区, 会在整个世界里查找";

    private void ExecuteSearch()
    {
        World? world = _document.World;
        if (world == null || IsSearching)
        {
            return;
        }

        string text = SearchText?.Trim() ?? string.Empty;
        int tab = SelectedTabIndex;

        IsSearching = true;
        Results.Clear();

        // 查找可能很慢, 放到后台线程避免界面卡住.
        Task.Run(() =>
        {
            var found = new List<FindResult>();

            if (tab == 0)
            {
                foreach (var chest in world.Chests)
                {
                    if (text.Length == 0
                        || chest.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                        || ChestContainsItem(chest, text))
                    {
                        found.Add(new FindResult(chest.X, chest.Y, $"箱子 {chest.Name} ({chest.X}, {chest.Y})", chest));
                    }
                }
            }
            else if (TryResolveIds(tab, text, out var ids))
            {
                for (int x = 0; x < world.TilesWide && found.Count < 5000; x++)
                {
                    for (int y = 0; y < world.TilesHigh; y++)
                    {
                        var tile = world.Tiles[x, y];
                        bool hit = tab == 1
                            ? tile.IsActive && ids.Contains(tile.Type)
                            : ids.Contains(tile.Wall);

                        if (hit)
                        {
                            found.Add(new FindResult(x, y, $"({x}, {y})"));
                            if (found.Count >= 5000)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            return found;
        }).ContinueWith(task =>
        {
            IsSearching = false;
            if (task.IsFaulted)
            {
                Summary = "查找失败, 详见日志";
                LogManager.Error($"查找失败: {task.Exception?.GetBaseException().Message}");
                return;
            }

            foreach (var item in task.Result)
            {
                Results.Add(item);
            }

            var highlights = new HashSet<(int X, int Y)>();
            foreach (var result in Results)
            {
                highlights.Add((result.X, result.Y));
            }

            TEdit5.Controls.WorldRenderEngine.Layers.RasterTileRenderer.FindHighlights = highlights;
            _document.RequestRedraw();

            UpdateSummary();
            LogManager.Info($"查找完成, 命中 {Results.Count} 处");
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ClearResults()
    {
        Results.Clear();
        TEdit5.Controls.WorldRenderEngine.Layers.RasterTileRenderer.FindHighlights = null;
        _document.RequestRedraw();
        SelectedResult = null;
        Summary = "尚未查找";
    }

    private void Navigate(int delta)
    {
        if (Results.Count == 0)
        {
            return;
        }

        int index = SelectedResult == null ? 0 : Results.IndexOf(SelectedResult) + delta;
        if (index < 0)
        {
            index = Results.Count - 1;
        }
        else if (index >= Results.Count)
        {
            index = 0;
        }

        SelectedResult = Results[index];
    }

    /// <summary>箱子里的物品名是否包含搜索词, 方便按物品找箱子.</summary>
    private static bool ChestContainsItem(Chest chest, string text)
    {
        foreach (var item in chest.Items)
        {
            if (item.NetId > 0
                && WorldConfiguration.ItemProperties.FirstOrDefault(p => p.Id == item.NetId) is { } property
                && property.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 把搜索词解析成一到多个编号: 数字直接当编号, 否则按名字匹配方块或墙体.
    /// </summary>
    private static bool TryResolveIds(int tab, string text, out HashSet<int> ids)
    {
        ids = new HashSet<int>();
        if (text.Length == 0)
        {
            return false;
        }

        if (int.TryParse(text, out int id))
        {
            ids.Add(id);
            return true;
        }

        if (tab == 1)
        {
            foreach (var property in WorldConfiguration.TileProperties)
            {
                if (property.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    ids.Add(property.Id);
                }
            }
        }
        else
        {
            foreach (var property in WorldConfiguration.WallProperties)
            {
                if (property.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    ids.Add(property.Id);
                }
            }
        }

        return ids.Count > 0;
    }

    private void UpdateSummary()
    {
        if (Results.Count == 0)
        {
            Summary = "没有命中结果";
            return;
        }

        int index = SelectedResult == null ? 0 : Results.IndexOf(SelectedResult) + 1;
        string suffix = Results.Count >= 5000 ? " (已达上限, 结果可能不全)" : string.Empty;
        Summary = $"第 {index} / {Results.Count} 处{suffix}";
    }
}
