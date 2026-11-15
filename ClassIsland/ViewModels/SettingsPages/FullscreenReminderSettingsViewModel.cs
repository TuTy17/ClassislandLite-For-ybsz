using System.Collections.ObjectModel;
using System.Linq;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Helpers;
using ClassIsland.Models;
using ClassIsland.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.ViewModels.SettingsPages;

public sealed class FullscreenReminderSettingsViewModel : ObservableRecipient
{
    private readonly IProfileService _profileService;

    public SettingsService SettingsService { get; }
    public ObservableCollection<LessonTimePointChoice> LessonChoices { get; } = [];

    public bool HasLessonChoices => LessonChoices.Count > 0;

    public FullscreenReminderSettingsViewModel(SettingsService settingsService, IProfileService profileService)
    {
        SettingsService = settingsService;
        _profileService = profileService;
        Refresh();
    }

    /// <summary>
    /// 按当前档案重建课程列表。设置页可能被缓存，档案里的时间表变化后需要重新读取。
    /// </summary>
    public void Refresh()
    {
        LessonChoices.Clear();
        foreach (var (layoutId, layout) in _profileService.Profile.TimeLayouts.OrderBy(pair => pair.Value.Name))
        {
            var lessonNumber = 0;
            foreach (var item in layout.Layouts.Where(item => item.TimeType == 0))
            {
                lessonNumber++;
                var key = LessonTimePointHelper.CreateKey(layoutId, item);
                LessonChoices.Add(new LessonTimePointChoice
                {
                    Key = key,
                    DisplayName = $"{layout.Name} · 第 {lessonNumber} 节 · {LessonTimePointHelper.FormatRange(item.StartTime, item.EndTime)}",
                    IsSelected = SettingsService.Settings.FullscreenEmphasisLessonKeys.Contains(key),
                    SelectionChanged = (choice, selected) =>
                    {
                        if (selected && !SettingsService.Settings.FullscreenEmphasisLessonKeys.Contains(choice.Key))
                        {
                            SettingsService.Settings.FullscreenEmphasisLessonKeys.Add(choice.Key);
                        }
                        else if (!selected)
                        {
                            SettingsService.Settings.FullscreenEmphasisLessonKeys.Remove(choice.Key);
                        }
                    }
                });
            }
        }

        OnPropertyChanged(nameof(HasLessonChoices));
    }
}
