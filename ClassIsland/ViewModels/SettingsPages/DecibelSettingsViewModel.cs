using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Helpers;
using ClassIsland.Models;
using ClassIsland.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.ViewModels.SettingsPages;

public sealed class DecibelSettingsViewModel : ObservableRecipient
{
    private readonly IProfileService _profileService;

    public SettingsService SettingsService { get; }
    public DecibelService DecibelService { get; }
    public ObservableCollection<string> DeviceNames { get; } = [];
    public ObservableCollection<LessonTimePointGroup> VisibilityGroups { get; } = [];
    public ObservableCollection<LessonTimePointChoice> VisibilityLessonChoices { get; } = [];

    public bool HasVisibilityLessonChoices => VisibilityGroups.Any(group => group.Items.Count > 0);

    public string CurrentDecibelText => DecibelService.IsMonitoring
        ? $"{DecibelService.CurrentDecibel:0.0} dB"
        : "-- dB";
    public string PeakDecibelText => $"峰值 {DecibelService.PeakDecibel:0.0} dB";
    public string StatusText => DecibelService.StatusText;
    public double DecibelPercent => System.Math.Clamp((DecibelService.CurrentDecibel - 30) / 90 * 100, 0, 100);

    public DecibelSettingsViewModel(
        SettingsService settingsService,
        DecibelService decibelService,
        IProfileService profileService)
    {
        SettingsService = settingsService;
        DecibelService = decibelService;
        _profileService = profileService;
        DecibelService.PropertyChanged += DecibelServiceOnPropertyChanged;
        RefreshLessonChoices();
    }

    /// <summary>
    /// 按当前档案重建“小组件显示时段”列表。设置页可能被缓存，档案里的时间表变化后需要重新读取。
    /// 单周/双周的同名时间表会自动合并成一组，勾选一节同时作用于两份时间表。
    /// </summary>
    public void RefreshLessonChoices()
    {
        VisibilityGroups.Clear();
        VisibilityLessonChoices.Clear();

        var settings = SettingsService.Settings;
        foreach (var group in LessonChoiceBuilder.Build(
                     _profileService,
                     key => settings.DecibelVisibleLessonKeys.Contains(key),
                     (keys, selected) => Toggle(keys, selected)))
        {
            VisibilityGroups.Add(group);
            foreach (var choice in group.Items)
            {
                VisibilityLessonChoices.Add(choice);
            }
        }

        OnPropertyChanged(nameof(HasVisibilityLessonChoices));
    }

    private void Toggle(IReadOnlyList<string> keys, bool selected)
    {
        foreach (var key in keys)
        {
            if (selected && !SettingsService.Settings.DecibelVisibleLessonKeys.Contains(key))
            {
                SettingsService.Settings.DecibelVisibleLessonKeys.Add(key);
            }
            else if (!selected)
            {
                SettingsService.Settings.DecibelVisibleLessonKeys.Remove(key);
            }
        }
    }

    public async System.Threading.Tasks.Task RefreshDevicesAsync()
    {
        DeviceNames.Clear();
        foreach (var deviceName in await DecibelService.GetCaptureDevicesAsync())
        {
            DeviceNames.Add(deviceName);
        }
        OnPropertyChanged(nameof(StatusText));
    }

    private void DecibelServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CurrentDecibelText));
        OnPropertyChanged(nameof(PeakDecibelText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(DecibelPercent));
    }
}
