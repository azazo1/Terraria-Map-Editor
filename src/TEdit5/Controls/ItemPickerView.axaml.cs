using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using TEdit.Terraria;
using TEdit5.ViewModels;

namespace TEdit5.Controls;

/// <summary>
/// 可搜索的物品选择器. 通过 SelectedId 双向绑定到物品编号, 选中项变化时回写.
/// </summary>
public partial class ItemPickerView : UserControl
{
    public static readonly StyledProperty<int> SelectedIdProperty =
        AvaloniaProperty.Register<ItemPickerView, int>(
            nameof(SelectedId), defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<ItemPickerView, string> SearchTextProperty =
        AvaloniaProperty.RegisterDirect<ItemPickerView, string>(
            nameof(SearchText), o => o.SearchText, (o, v) => o.SearchText = v);

    private static IReadOnlyList<PaletteItem>? _catalog;

    private string _searchText = string.Empty;
    private bool _syncing;

    public ItemPickerView()
    {
        InitializeComponent();
        Entries = new ObservableCollection<PaletteItem>();
        RefreshEntries();

        var list = this.FindControl<ListBox>("ItemList");
        if (list != null)
        {
            list.SelectionChanged += (_, _) =>
            {
                if (_syncing || list.SelectedItem is not PaletteItem item)
                {
                    return;
                }

                SelectedId = item.Id;
            };
        }
    }

    /// <summary>当前过滤后的可选物品.</summary>
    public ObservableCollection<PaletteItem> Entries { get; }

    public int SelectedId
    {
        get => GetValue(SelectedIdProperty);
        set => SetValue(SelectedIdProperty, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            SetAndRaise(SearchTextProperty, ref _searchText, value);
            RefreshEntries();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != SelectedIdProperty)
        {
            return;
        }

        var list = this.FindControl<ListBox>("ItemList");
        if (list == null || _syncing)
        {
            return;
        }

        _syncing = true;
        list.SelectedItem = Entries.FirstOrDefault(e => e.Id == SelectedId);
        _syncing = false;
    }

    private void RefreshEntries()
    {
        var catalog = _catalog ??= WorldConfiguration.ItemProperties
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new PaletteItem(p.Id, p.Name, default, "Item"))
            .ToList();

        string text = SearchText?.Trim() ?? string.Empty;
        Entries.Clear();
        foreach (var item in catalog)
        {
            if (text.Length > 0
                && !item.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !item.Id.ToString().Contains(text, StringComparison.Ordinal))
            {
                continue;
            }

            Entries.Add(item);
            if (Entries.Count >= 500)
            {
                break;
            }
        }
    }
}
