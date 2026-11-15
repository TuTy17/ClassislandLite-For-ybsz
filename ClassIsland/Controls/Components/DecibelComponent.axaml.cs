using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Helpers;
using ClassIsland.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Controls.Components;

[ComponentInfo("6AC9B76D-6B69-4C8B-8E4D-103C87B2BBA2", "分贝显示", "\uE9D9", "显示麦克风实时分贝，可按课程时段控制显示。")]
public partial class DecibelComponent : ComponentBase, INotifyPropertyChanged
{
    private readonly DecibelService _decibelService;
    private readonly SettingsService _settingsService;
    private readonly ILessonsService _lessonsService;
    private bool _attached;

    public string CurrentDecibelText => _decibelService.IsMonitoring
        ? $"{_decibelService.CurrentDecibel:0.0} dB"
        : "麦克风未启用";

    public string PeakText => $"峰值 {_decibelService.PeakDecibel:0.0}";

    public double DecibelPercent => Math.Clamp((_decibelService.CurrentDecibel - 30) / 90 * 100, 0, 100);

    public DecibelComponent(DecibelService decibelService, SettingsService settingsService, ILessonsService lessonsService)
    {
        _decibelService = decibelService;
        _settingsService = settingsService;
        _lessonsService = lessonsService;
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
    }

    private async void OnAttached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _attached = true;
        _decibelService.PropertyChanged += DecibelServiceOnPropertyChanged;
        _lessonsService.PostMainTimerTicked += LessonsServiceOnPostMainTimerTicked;
        UpdateVisibility();
        if (_settingsService.Settings.IsDecibelMonitorEnabled)
        {
            await _decibelService.StartAsync();
        }
    }

    private void OnDetached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        _decibelService.PropertyChanged -= DecibelServiceOnPropertyChanged;
        _lessonsService.PostMainTimerTicked -= LessonsServiceOnPostMainTimerTicked;
    }

    private void LessonsServiceOnPostMainTimerTicked(object? sender, EventArgs e)
    {
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        var plan = _lessonsService.CurrentClassPlan;
        var item = _lessonsService.CurrentTimeLayoutItem;
        IsVisible = _attached &&
                    plan?.TimeLayout != null &&
                    item?.TimeType == 0 &&
                    _settingsService.Settings.DecibelVisibleLessonKeys.Contains(
                        LessonTimePointHelper.CreateKey(plan.TimeLayoutId, item));
    }

    private void DecibelServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CurrentDecibelText));
        OnPropertyChanged(nameof(PeakText));
        OnPropertyChanged(nameof(DecibelPercent));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
