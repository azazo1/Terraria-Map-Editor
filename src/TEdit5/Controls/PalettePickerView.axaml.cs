using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using TEdit5.ViewModels;

namespace TEdit5.Controls;

/// <summary>
/// 可搜索的列表选择器, 用于方块, 墙体与涂料. Items 提供候选, SelectedId 双向回写编号.
/// </summary>
public partial class PalettePickerView : UserControl
{
    public static readonly StyledProperty<IEnumerable<PaletteItem>?> ItemsProperty =
        AvaloniaProperty.Register<PalettePickerView, IEnumerable<PaletteItem>?>(nameof(Items));

    public static readonly StyledProperty<int> SelectedIdProperty =
        AvaloniaProperty.Register<PalettePickerView, int>(
            nameof(SelectedId), defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<PalettePickerView, string> SearchTextProperty =
        AvaloniaProperty.RegisterDirect<PalettePickerView, string>(
            nameof(SearchText), o => o.SearchText, (o, v) => o.SearchText = v);

    private string _searchText = string.Empty;
    private bool _syncing;

    public PalettePickerView()
    {
        InitializeComponent();
        Entries = new ObservableCollection<PaletteItem>();

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

    public ObservableCollection<PaletteItem> Entries { get; }

    public IEnumerable<PaletteItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

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

        if (change.Property == ItemsProperty)
        {
            RefreshEntries();
            SyncSelection();
        }
        else if (change.Property == SelectedIdProperty)
        {
            SyncSelection();
        }
    }

    private void SyncSelection()
    {
        var list = this.FindControl<ListBox>("ItemList");
        if (list == null || _syncing)
        {
            return;
        }

        _syncing = true;
        list.SelectedItem = Entries.FirstOrDefault(e => e.Id == SelectedId);
        if (list.SelectedItem != null)
        {
            list.ScrollIntoView(list.SelectedItem);
        }

        _syncing = false;
    }

    private void RefreshEntries()
    {
        string text = SearchText?.Trim() ?? string.Empty;
        Entries.Clear();

        foreach (var item in Items ?? Enumerable.Empty<PaletteItem>())
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
