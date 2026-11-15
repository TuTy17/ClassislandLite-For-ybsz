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
    public SettingsService SettingsService { get; }
    public ObservableCollection<LessonTimePointChoice> LessonChoices { get; } = [];

    public FullscreenReminderSettingsViewModel(SettingsService settingsService, IProfileService profileService)
    {
        SettingsService = settingsService;
        foreach (var (layoutId, layout) in profileService.Profile.TimeLayouts.OrderBy(pair => pair.Value.Name))
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
                    IsSelected = settingsService.Settings.FullscreenEmphasisLessonKeys.Contains(key),
                    SelectionChanged = (choice, selected) =>
                    {
                        if (selected && !settingsService.Settings.FullscreenEmphasisLessonKeys.Contains(choice.Key))
                        {
                            settingsService.Settings.FullscreenEmphasisLessonKeys.Add(choice.Key);
                        }
                        else if (!selected)
                        {
                            settingsService.Settings.FullscreenEmphasisLessonKeys.Remove(choice.Key);
                        }
                    }
                });
            }
        }
    }
}
