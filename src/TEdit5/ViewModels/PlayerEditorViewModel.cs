using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using ReactiveUI;
using TEdit.Terraria.Player;
using TEdit5.Services;

namespace TEdit5.ViewModels;

/// <summary>
/// 玩家编辑器. 目前是只读视图: 打开 .plr 后显示基本信息.
/// </summary>
public class PlayerEditorViewModel : ReactiveObject
{
    private PlayerCharacter? _player;
    private string _fileName = string.Empty;
    private string _currentPath = string.Empty;

    /// <summary>物品栏, 只读展示.</summary>
    public ObservableCollection<PlayerItemRow> InventoryRows { get; } = new();

    /// <summary>装备栏, 只读展示.</summary>
    public ObservableCollection<PlayerItemRow> ArmorRows { get; } = new();

    /// <summary>增益栏, 只读展示.</summary>
    public ObservableCollection<PlayerBuffRow> BuffRows { get; } = new();

    /// <summary>小猪存钱罐与保险箱的物品, 只读展示.</summary>
    public ObservableCollection<PlayerItemRow> BankRows { get; } = new();

    /// <summary>是否已经读入玩家.</summary>
    public bool HasPlayer => _player != null;

    /// <summary>已打开的玩家存档文件名.</summary>
    public string FileName
    {
        get => _fileName;
        private set => this.RaiseAndSetIfChanged(ref _fileName, value);
    }

    /// <summary>角色名字, 可直接修改.</summary>
    public string Name
    {
        get => _player?.Name ?? "(未打开)";
        set
        {
            if (_player == null || _player.Name == value)
            {
                return;
            }

            _player.Name = value;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>难度序号, 0 经典 1 中核 2 硬核 3 旅行.</summary>
    public int Difficulty
    {
        get => _player?.Difficulty ?? 0;
        set
        {
            if (_player == null || _player.Difficulty == value)
            {
                return;
            }

            _player.Difficulty = (byte)value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(DifficultyText));
        }
    }

    /// <summary>难度文字.</summary>
    public string DifficultyText => _player?.Difficulty switch
    {
        0 => "经典",
        1 => "中核",
        2 => "硬核",
        3 => "旅行",
        _ => "(未知)",
    };

    /// <summary>游戏时间, 按 时 分 显示.</summary>
    public string PlayTimeText
    {
        get
        {
            if (_player == null)
            {
                return "-";
            }

            var span = _player.PlayTime;
            return $"{(int)span.TotalHours} 小时 {span.Minutes} 分";
        }
    }

    /// <summary>当前生命, 可直接修改.</summary>
    public int StatLife
    {
        get => _player?.StatLife ?? 0;
        set
        {
            if (_player == null || _player.StatLife == value)
            {
                return;
            }

            _player.StatLife = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(HealthText));
        }
    }

    /// <summary>生命上限, 可直接修改.</summary>
    public int StatLifeMax
    {
        get => _player?.StatLifeMax ?? 0;
        set
        {
            if (_player == null || _player.StatLifeMax == value)
            {
                return;
            }

            _player.StatLifeMax = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(HealthText));
        }
    }

    /// <summary>当前魔力, 可直接修改.</summary>
    public int StatMana
    {
        get => _player?.StatMana ?? 0;
        set
        {
            if (_player == null || _player.StatMana == value)
            {
                return;
            }

            _player.StatMana = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(ManaText));
        }
    }

    /// <summary>魔力上限, 可直接修改.</summary>
    public int StatManaMax
    {
        get => _player?.StatManaMax ?? 0;
        set
        {
            if (_player == null || _player.StatManaMax == value)
            {
                return;
            }

            _player.StatManaMax = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(ManaText));
        }
    }

    /// <summary>生命值显示.</summary>
    public string HealthText => _player == null ? "-" : $"{_player.StatLife} / {_player.StatLifeMax}";

    /// <summary>队伍序号, 0 无队伍, 1 到 5 对应五个队伍.</summary>
    public int Team
    {
        get => _player?.Team ?? 0;
        set
        {
            if (_player == null || _player.Team == value)
            {
                return;
            }

            _player.Team = (byte)value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(JourneySummary));
        this.RaisePropertyChanged(nameof(IsGodMode));
        this.RaisePropertyChanged(nameof(IsTimeFrozen));
        this.RaisePropertyChanged(nameof(TimeSpeed));
        this.RaisePropertyChanged(nameof(JourneyPowersText));
        this.RaisePropertyChanged(nameof(TeamText));
        }
    }

    /// <summary>发型编号, 可直接修改.</summary>
    public int Hair
    {
        get => _player?.Appearance.Hair ?? 0;
        set
        {
            if (_player == null || _player.Appearance.Hair == value)
            {
                return;
            }

            _player.Appearance.Hair = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(HairText));
        }
    }

    /// <summary>旅程模式的研究记录, 次数可编辑.</summary>
    public ObservableCollection<PlayerSacrificeRow> SacrificeRows { get; } = new();

    /// <summary>旅程模式概况: 已研究物品种类与总次数.</summary>
    public string JourneySummary
    {
        get
        {
            if (_player == null)
            {
                return "-";
            }

            var sacrifices = _player.CreativeSacrifices;
            int total = 0;
            foreach (var pair in sacrifices)
            {
                total += pair.Value;
            }

            return $"{sacrifices.Count} 种物品, 累计研究 {total} 次";
        }
    }

    /// <summary>旅程模式开关状态.</summary>
    public string JourneyPowersText
    {
        get
        {
            if (_player == null)
            {
                return "-";
            }

            var powers = _player.JourneyPowers;
            string Format(string label, bool? value) => value == null ? $"{label} 未设置" : $"{label} {(value.Value ? "开" : "关")}";

            var parts = new[]
            {
                Format("无敌", powers.GetPowerBool(TEdit.Terraria.CreativePowerId.godmode)),
                Format("时间冻结", powers.GetPowerBool(TEdit.Terraria.CreativePowerId.time_setfrozen)),
            };
            float? speed = powers.GetPowerFloat(TEdit.Terraria.CreativePowerId.time_setspeed);
            string speedText = speed == null ? "时间流速 未设置" : $"时间流速 {speed.Value:0.00}";
            return string.Join("   ", parts) + "   " + speedText;
        }
    }

    /// <summary>无敌开关, 旅程模式能力.</summary>
    public bool? IsGodMode
    {
        get => _player?.JourneyPowers.GetPowerBool(TEdit.Terraria.CreativePowerId.godmode);
        set
        {
            if (_player == null || value == null)
            {
                return;
            }

            _player.JourneyPowers.SetPowerStateSafe(TEdit.Terraria.CreativePowerId.godmode, isEnabled: value.Value);
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(JourneyPowersText));
        }
    }

    /// <summary>时间冻结开关, 旅程模式能力.</summary>
    public bool? IsTimeFrozen
    {
        get => _player?.JourneyPowers.GetPowerBool(TEdit.Terraria.CreativePowerId.time_setfrozen);
        set
        {
            if (_player == null || value == null)
            {
                return;
            }

            _player.JourneyPowers.SetPowerStateSafe(TEdit.Terraria.CreativePowerId.time_setfrozen, isEnabled: value.Value);
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(JourneyPowersText));
        }
    }

    /// <summary>时间流速, 0 到 1 之间.</summary>
    public decimal TimeSpeed
    {
        get => (decimal)(_player?.JourneyPowers.GetPowerFloat(TEdit.Terraria.CreativePowerId.time_setspeed) ?? 0f);
        set
        {
            if (_player == null)
            {
                return;
            }

            float clamped = Math.Clamp((float)value, 0f, 1f);
            _player.JourneyPowers.SetPowerStateSafe(TEdit.Terraria.CreativePowerId.time_setspeed, clamped);
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(JourneyPowersText));
        }
    }

    /// <summary>肤色变体, 0 到 3 为男性, 4 到 7 为女性.</summary>
    public int SkinVariant
    {
        get => _player?.Appearance.SkinVariant ?? 0;
        set
        {
            if (_player == null || _player.Appearance.SkinVariant == value)
            {
                return;
            }

            byte clamped = (byte)Math.Clamp(value, 0, 7);
            _player.Appearance.SkinVariant = clamped;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(HairText));
        }
    }

    /// <summary>队伍显示, 0 表示无队伍.</summary>
    public string TeamText => _player == null
        ? "-"
        : _player.Team switch
        {
            1 => "红队",
            2 => "绿队",
            3 => "蓝队",
            4 => "黄队",
            5 => "粉队",
            _ => "无队伍",
        };

    /// <summary>外观概览: 发型与性别.</summary>
    public string HairText => _player == null
        ? "-"
        : $"发型 {_player.Appearance.Hair}, {(_player.Appearance.Male ? "男" : "女")}, 肤色 {_player.Appearance.SkinVariant}";

    /// <summary>外观配色, 按 头发/皮肤/眼睛/上衣/内衣/裤子/鞋 顺序.</summary>
    public string ColorText
    {
        get
        {
            if (_player == null)
            {
                return "-";
            }

            var a = _player.Appearance;
            return $"头发 {Hex(a.HairColor)}  皮肤 {Hex(a.SkinColor)}  眼睛 {Hex(a.EyeColor)}\n"
                 + $"上衣 {Hex(a.ShirtColor)}  内衣 {Hex(a.UnderShirtColor)}  裤子 {Hex(a.PantsColor)}  鞋 {Hex(a.ShoeColor)}";
        }
    }

    private static string Hex(TEdit.Common.TEditColor color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>解析 #RRGGBB 形式的颜色, 失败时返回 null.</summary>
    private static TEdit.Common.TEditColor? ParseHex(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string value = text.Trim().TrimStart('#');
        if (value.Length != 6)
        {
            return null;
        }

        if (!byte.TryParse(value[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r)
            || !byte.TryParse(value[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g)
            || !byte.TryParse(value[4..], System.Globalization.NumberStyles.HexNumber, null, out byte b))
        {
            return null;
        }

        return new TEdit.Common.TEditColor(r, g, b);
    }

    private string GetColorText(Func<TEdit.Terraria.Player.PlayerAppearance, TEdit.Common.TEditColor> pick)
        => _player == null ? "-" : Hex(pick(_player.Appearance));

    private void SetColor(Func<TEdit.Terraria.Player.PlayerAppearance, TEdit.Common.TEditColor> pick,
                          Action<TEdit.Terraria.Player.PlayerAppearance, TEdit.Common.TEditColor> apply,
                          string? text, string propertyName)
    {
        if (_player == null)
        {
            return;
        }

        var parsed = ParseHex(text);
        if (parsed == null || pick(_player.Appearance).Equals(parsed.Value))
        {
            return;
        }

        apply(_player.Appearance, parsed.Value);
        this.RaisePropertyChanged(propertyName);
    }

    public string HairColorHex
    {
        get => GetColorText(a => a.HairColor);
        set => SetColor(a => a.HairColor, (a, c) => a.HairColor = c, value, nameof(HairColorHex));
    }

    public string SkinColorHex
    {
        get => GetColorText(a => a.SkinColor);
        set => SetColor(a => a.SkinColor, (a, c) => a.SkinColor = c, value, nameof(SkinColorHex));
    }

    public string EyeColorHex
    {
        get => GetColorText(a => a.EyeColor);
        set => SetColor(a => a.EyeColor, (a, c) => a.EyeColor = c, value, nameof(EyeColorHex));
    }

    public string ShirtColorHex
    {
        get => GetColorText(a => a.ShirtColor);
        set => SetColor(a => a.ShirtColor, (a, c) => a.ShirtColor = c, value, nameof(ShirtColorHex));
    }

    public string UnderShirtColorHex
    {
        get => GetColorText(a => a.UnderShirtColor);
        set => SetColor(a => a.UnderShirtColor, (a, c) => a.UnderShirtColor = c, value, nameof(UnderShirtColorHex));
    }

    public string PantsColorHex
    {
        get => GetColorText(a => a.PantsColor);
        set => SetColor(a => a.PantsColor, (a, c) => a.PantsColor = c, value, nameof(PantsColorHex));
    }

    public string ShoeColorHex
    {
        get => GetColorText(a => a.ShoeColor);
        set => SetColor(a => a.ShoeColor, (a, c) => a.ShoeColor = c, value, nameof(ShoeColorHex));
    }

    /// <summary>魔力值显示.</summary>
    public string ManaText => _player == null ? "-" : $"{_player.StatMana} / {_player.StatManaMax}";

    /// <summary>读取指定存档.</summary>
    public void Load(string path)
    {
        try
        {
            var player = PlayerFile.Load(path);
            _player = player;
            _currentPath = path;
            FileName = Path.GetFileName(path);
            FillRows();
            RefreshPreview();
            LogManager.Info($"已打开玩家存档: {FileName}, 角色 {player.Name}");
            RaiseAll();
        }
        catch (Exception ex)
        {
            LogManager.Error($"读取玩家存档失败: {ex.Message}");
        }
    }

    private Avalonia.Media.Imaging.Bitmap? _previewBitmap;

    /// <summary>按当前配色画的外观示意图.</summary>
    public Avalonia.Media.Imaging.Bitmap? PreviewBitmap
    {
        get => _previewBitmap;
        private set => this.RaiseAndSetIfChanged(ref _previewBitmap, value);
    }

    /// <summary>重新生成外观示意图.</summary>
    public void RefreshPreview()
    {
        PreviewBitmap = Services.PlayerPreviewRenderer.Render(_player);
    }

    /// <summary>当前存档的完整路径, 未打开时为空.</summary>
    public string CurrentPath => _currentPath;

    /// <summary>就地保存到当前路径, 没有路径时返回 false.</summary>
    public bool SaveCurrent()
    {
        if (string.IsNullOrEmpty(_currentPath))
        {
            return false;
        }

        return Save(_currentPath);
    }

    /// <summary>重新读取当前存档, 丢弃未保存的改动.</summary>
    public void Reload()
    {
        if (string.IsNullOrEmpty(_currentPath))
        {
            LogManager.Warn("还没有打开玩家存档, 无法重新载入");
            return;
        }

        Load(_currentPath);
    }

    /// <summary>把当前玩家写回指定路径.</summary>
    public bool Save(string path)
    {
        if (_player == null)
        {
            LogManager.Warn("还没有打开玩家存档, 无法保存");
            return false;
        }

        try
        {
            PlayerFile.Save(path, _player);
            LogManager.Info($"玩家存档已保存: {Path.GetFileName(path)}");
            return true;
        }
        catch (Exception ex)
        {
            LogManager.Error($"保存玩家存档失败: {ex.Message}");
            return false;
        }
    }

    private void FillRows()
    {
        InventoryRows.Clear();
        ArmorRows.Clear();
        BuffRows.Clear();
        BankRows.Clear();

        if (_player == null)
        {
            return;
        }

        for (int i = 0; i < _player.Inventory.Count; i++)
        {
            var item = _player.Inventory[i];
            if (item.NetId > 0)
            {
                InventoryRows.Add(new PlayerItemRow(item, i));
            }
        }

        for (int i = 0; i < _player.Armor.Count; i++)
        {
            var item = _player.Armor[i];
            if (item.NetId > 0)
            {
                ArmorRows.Add(new PlayerItemRow(item, i));
            }
        }
        for (int i = 0; i < _player.Buffs.Count; i++)
        {
            var buff = _player.Buffs[i];
            if (buff.Type > 0)
            {
                BuffRows.Add(new PlayerBuffRow(i, buff.Type, buff.Time));
            }
        }

        SacrificeRows.Clear();
        foreach (var pair in _player.CreativeSacrifices)
        {
            SacrificeRows.Add(new PlayerSacrificeRow(_player.CreativeSacrifices, pair.Key, pair.Value));
        }

        AddBank("存钱罐", _player.Bank1);
        AddBank("保险箱", _player.Bank2);
        AddBank("护卫熔炉", _player.Bank3);
        AddBank("虚空保险库", _player.Bank4);
    }

    private void AddBank(string label, System.Collections.Generic.IReadOnlyList<PlayerItem> bank)
    {
        for (int i = 0; i < bank.Count; i++)
        {
            var item = bank[i];
            if (item.NetId > 0)
            {
                BankRows.Add(new PlayerItemRow(item, i, label));
            }
        }
    }

    private void RaiseAll()
    {
        this.RaisePropertyChanged(nameof(HasPlayer));
        this.RaisePropertyChanged(nameof(Name));
        this.RaisePropertyChanged(nameof(DifficultyText));
        this.RaisePropertyChanged(nameof(PlayTimeText));
        this.RaisePropertyChanged(nameof(HealthText));
        this.RaisePropertyChanged(nameof(ManaText));
        this.RaisePropertyChanged(nameof(JourneySummary));
        this.RaisePropertyChanged(nameof(IsGodMode));
        this.RaisePropertyChanged(nameof(IsTimeFrozen));
        this.RaisePropertyChanged(nameof(TimeSpeed));
        this.RaisePropertyChanged(nameof(JourneyPowersText));
        this.RaisePropertyChanged(nameof(TeamText));
        this.RaisePropertyChanged(nameof(Difficulty));
        this.RaisePropertyChanged(nameof(Team));
        this.RaisePropertyChanged(nameof(Hair));
        this.RaisePropertyChanged(nameof(SkinVariant));
        this.RaisePropertyChanged(nameof(StatLife));
        this.RaisePropertyChanged(nameof(StatLifeMax));
        this.RaisePropertyChanged(nameof(StatMana));
        this.RaisePropertyChanged(nameof(StatManaMax));
        this.RaisePropertyChanged(nameof(HairText));
        this.RaisePropertyChanged(nameof(ColorText));
        this.RaisePropertyChanged(nameof(HairColorHex));
        this.RaisePropertyChanged(nameof(SkinColorHex));
        this.RaisePropertyChanged(nameof(EyeColorHex));
        this.RaisePropertyChanged(nameof(ShirtColorHex));
        this.RaisePropertyChanged(nameof(UnderShirtColorHex));
        this.RaisePropertyChanged(nameof(PantsColorHex));
        this.RaisePropertyChanged(nameof(ShoeColorHex));
        this.RaisePropertyChanged(nameof(InventoryRows));
        this.RaisePropertyChanged(nameof(ArmorRows));
        this.RaisePropertyChanged(nameof(BuffRows));
        this.RaisePropertyChanged(nameof(BankRows));
    }
}

/// <summary>玩家物品栏里的一行.</summary>
public sealed class PlayerItemRow : ReactiveObject
{
    private readonly PlayerItem _item;

    public PlayerItemRow(PlayerItem item, int slot, string label = "")
    {
        _item = item;
        Slot = slot;
        Label = label;
    }

    /// <summary>物品编号, 可直接修改.</summary>
    public int NetId
    {
        get => _item.NetId;
        set
        {
            if (_item.NetId == value)
            {
                return;
            }

            _item.NetId = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(Name));
        }
    }

    /// <summary>槽位序号, 从 0 开始.</summary>
    public int Slot { get; }

    /// <summary>叫法前缀, 银行物品用来说明来源, 其它情况为空.</summary>
    public string Label { get; }

    /// <summary>物品名字, 会带上来源前缀.</summary>
    public string Name => string.IsNullOrEmpty(Label) ? _item.Name : $"{Label} · {_item.Name}";

    /// <summary>堆叠数量, 可直接修改.</summary>
    public int Stack
    {
        get => _item.StackSize;
        set
        {
            if (_item.StackSize == value)
            {
                return;
            }

            _item.StackSize = value;
            this.RaisePropertyChanged();
        }
    }

    /// <summary>前缀编号, 可直接修改.</summary>
    public int PrefixId
    {
        get => _item.Prefix;
        set
        {
            byte clamped = (byte)Math.Clamp(value, 0, 255);
            if (_item.Prefix == clamped)
            {
                return;
            }

            _item.Prefix = clamped;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(Prefix));
        }
    }

    /// <summary>前缀名, 没有前缀时为空.</summary>
    public string Prefix => _item.PrefixName;
}

/// <summary>增益栏里的一项.</summary>
public sealed class PlayerBuffRow(int slot, int type, int time)
{
    /// <summary>槽位序号, 从 0 开始.</summary>
    public int Slot { get; } = slot;

    /// <summary>增益编号.</summary>
    public int Type { get; } = type;

    /// <summary>剩余时间, 游戏内分钟.</summary>
    public int Time { get; } = time;
}

/// <summary>旅程模式研究记录的一行, 改动直接写回字典.</summary>
public sealed class PlayerSacrificeRow : ReactiveObject
{
    private readonly Dictionary<string, int> _source;
    private int _count;

    public PlayerSacrificeRow(Dictionary<string, int> source, string key, int count)
    {
        _source = source;
        Key = key;
        _count = count;
    }

    /// <summary>物品键, 通常是物品编号的字符串.</summary>
    public string Key { get; }

    /// <summary>研究次数, 可直接修改.</summary>
    public int Count
    {
        get => _count;
        set
        {
            if (_count == value)
            {
                return;
            }

            _count = value;
            _source[Key] = value;
            this.RaisePropertyChanged(nameof(Count));
        }
    }
}
