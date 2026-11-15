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
    public SettingsService SettingsService { get; }
    public DecibelService DecibelService { get; }
    public ObservableCollection<string> DeviceNames { get; } = [];
    public ObservableCollection<LessonTimePointChoice> VisibilityLessonChoices { get; } = [];

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
        DecibelService.PropertyChanged += DecibelServiceOnPropertyChanged;

        foreach (var (layoutId, layout) in profileService.Profile.TimeLayouts.OrderBy(pair => pair.Value.Name))
        {
            var lessonNumber = 0;
            foreach (var item in layout.Layouts.Where(item => item.TimeType == 0))
            {
                lessonNumber++;
                var key = LessonTimePointHelper.CreateKey(layoutId, item);
                VisibilityLessonChoices.Add(new LessonTimePointChoice
                {
                    Key = key,
                    DisplayName = $"{layout.Name} · 第 {lessonNumber} 节 · {LessonTimePointHelper.FormatRange(item.StartTime, item.EndTime)}",
                    IsSelected = settingsService.Settings.DecibelVisibleLessonKeys.Contains(key),
                    SelectionChanged = (choice, selected) =>
                    {
                        if (selected && !settingsService.Settings.DecibelVisibleLessonKeys.Contains(choice.Key))
                        {
                            settingsService.Settings.DecibelVisibleLessonKeys.Add(choice.Key);
                        }
                        else if (!selected)
                        {
                            settingsService.Settings.DecibelVisibleLessonKeys.Remove(choice.Key);
                        }
                    }
                });
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
