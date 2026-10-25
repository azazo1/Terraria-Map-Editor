using System;
using ReactiveUI;
using ReactiveUI.Primitives;
using TEdit.Terraria;
using TEdit5.Services;

namespace TEdit5.ViewModels;

/// <summary>
/// 创意能力面板. 移植自 Windows 版的 CreativePowersViewModel, 直接把开关与数值
/// 映射到世界的 CreativePowers 上.
/// </summary>
public partial class CreativePowersViewModel : ReactiveObject
{
    private readonly DocumentViewModel _document;

    public CreativePowersViewModel(DocumentViewModel document)
    {
        _document = document;
        LoadCommand = ReactiveCommand.Create(Load);
        SaveCommand = ReactiveCommand.Create(Save);
    }

    public ReactiveCommand<RxVoid, RxVoid> LoadCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SaveCommand { get; }

    private CreativePowers? Powers => _document.World?.CreativePowers;

    public bool IsTimeFrozen
    {
        get => Powers?.GetPowerBool(CreativePowerId.time_setfrozen) ?? false;
        set => SetBool(CreativePowerId.time_setfrozen, value);
    }

    public bool IsRainFrozen
    {
        get => Powers?.GetPowerBool(CreativePowerId.rain_setfrozen) ?? false;
        set => SetBool(CreativePowerId.rain_setfrozen, value);
    }

    public bool IsWindFrozen
    {
        get => Powers?.GetPowerBool(CreativePowerId.wind_setfrozen) ?? false;
        set => SetBool(CreativePowerId.wind_setfrozen, value);
    }

    public bool IsBiomeSpreadFrozen
    {
        get => Powers?.GetPowerBool(CreativePowerId.biomespread_setfrozen) ?? false;
        set => SetBool(CreativePowerId.biomespread_setfrozen, value);
    }

    /// <summary>难度是否由创意能力接管.</summary>
    public bool EnableDifficulty
    {
        get => Powers?.GetPowerFloat(CreativePowerId.setdifficulty) != null;
        set
        {
            if (Powers == null)
            {
                return;
            }

            if (value)
            {
                Powers.SetPowerStateSafe(new CreativePowerArgs
                {
                    Id = CreativePowerId.setdifficulty,
                    Value = (float)Difficulty,
                    IsActive = true,
                });
            }

            this.RaisePropertyChanged();
        }
    }

    public double Difficulty
    {
        get => Powers?.GetPowerFloat(CreativePowerId.setdifficulty) ?? 0.5;
        set
        {
            Powers?.SetPowerStateSafe(CreativePowerId.setdifficulty, (float)value);
            this.RaisePropertyChanged();
        }
    }

    /// <summary>时间流速是否由创意能力接管.</summary>
    public bool EnableTimeSpeed
    {
        get => Powers?.GetPowerFloat(CreativePowerId.time_setspeed) != null;
        set
        {
            if (Powers == null)
            {
                return;
            }

            if (value)
            {
                Powers.SetPowerStateSafe(new CreativePowerArgs
                {
                    Id = CreativePowerId.time_setspeed,
                    Value = (float)TimeSpeed,
                    IsActive = true,
                });
            }

            this.RaisePropertyChanged();
        }
    }

    public double TimeSpeed
    {
        get => Powers?.GetPowerFloat(CreativePowerId.time_setspeed) ?? 0.5;
        set
        {
            Powers?.SetPowerStateSafe(CreativePowerId.time_setspeed, (float)value);
            this.RaisePropertyChanged();
        }
    }

    private void SetBool(CreativePowerId id, bool value)
    {
        Powers?.SetPowerStateSafe(new CreativePowerArgs
        {
            Id = id,
            IsActive = value,
        });
        this.RaisePropertyChanged();
    }

    /// <summary>刷新界面绑定.</summary>
    public void Load()
    {
        this.RaisePropertyChanged(string.Empty);
        LogManager.Info("创意能力已刷新");
    }

    /// <summary>把改动写回世界.</summary>
    public void Save()
    {
        _document.MarkDirty();
        LogManager.Info("创意能力已写回世界");
    }
}
