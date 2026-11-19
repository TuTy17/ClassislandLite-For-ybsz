using System;
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
    /// </summary>
    public void RefreshLessonChoices()
    {
        VisibilityGroups.Clear();
        VisibilityLessonChoices.Clear();

        foreach (var (layoutId, layout) in _profileService.Profile.TimeLayouts.OrderBy(pair => pair.Value.Name))
        {
            var group = new LessonTimePointGroup { Name = layout.Name };
            var lessonNumber = 0;
            foreach (var item in layout.Layouts.Where(item => item.TimeType == 0))
            {
                lessonNumber++;
                var key = LessonTimePointHelper.CreateKey(layoutId, item);
                var shortName = $"第 {lessonNumber} 节";
                var choice = new LessonTimePointChoice
                {
                    Key = key,
                    DisplayName = $"{layout.Name} · {shortName} · {LessonTimePointHelper.FormatRange(item.StartTime, item.EndTime)}",
                    ShortName = shortName,
                    TimeText = LessonTimePointHelper.FormatRange(item.StartTime, item.EndTime),
                    GroupName = layout.Name,
                    IsSelected = SettingsService.Settings.DecibelVisibleLessonKeys.Contains(key),
                    SelectionChanged = (changed, selected) =>
                    {
                        if (selected && !SettingsService.Settings.DecibelVisibleLessonKeys.Contains(changed.Key))
                        {
                            SettingsService.Settings.DecibelVisibleLessonKeys.Add(changed.Key);
                        }
                        else if (!selected)
                        {
                            SettingsService.Settings.DecibelVisibleLessonKeys.Remove(changed.Key);
                        }
                    }
                };
                group.Items.Add(choice);
                VisibilityLessonChoices.Add(choice);
            }

            if (group.Items.Count > 0)
            {
                VisibilityGroups.Add(group);
            }
        }

        OnPropertyChanged(nameof(HasVisibilityLessonChoices));
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
