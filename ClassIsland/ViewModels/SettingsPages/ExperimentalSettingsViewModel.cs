using System;
using System.Collections.ObjectModel;
using System.Linq;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Helpers;
using ClassIsland.Models;
using ClassIsland.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.ViewModels.SettingsPages;

public sealed class ExperimentalSettingsViewModel : ObservableRecipient
{
    private readonly IProfileService _profileService;

    public SettingsService SettingsService { get; }
    public RecipeService RecipeService { get; }
    public QqNotificationMirrorService QqNotificationMirrorService { get; }

    public ObservableCollection<LessonTimePointGroup> LunchGroups { get; } = [];
    public ObservableCollection<LessonTimePointGroup> DinnerGroups { get; } = [];

    public bool HasRecipeLessonChoices => LunchGroups.Any(group => group.Items.Count > 0);

    public ExperimentalSettingsViewModel(
        SettingsService settingsService,
        IProfileService profileService,
        RecipeService recipeService,
        QqNotificationMirrorService qqNotificationMirrorService)
    {
        SettingsService = settingsService;
        _profileService = profileService;
        RecipeService = recipeService;
        QqNotificationMirrorService = qqNotificationMirrorService;
        RefreshLessonChoices();
    }

    /// <summary>
    /// 按当前档案重建午餐/晚餐触发课程列表（支持多选）。
    /// </summary>
    public void RefreshLessonChoices()
    {
        LunchGroups.Clear();
        DinnerGroups.Clear();

        foreach (var (layoutId, layout) in _profileService.Profile.TimeLayouts.OrderBy(pair => pair.Value.Name))
        {
            var lunchGroup = new LessonTimePointGroup { Name = layout.Name };
            var dinnerGroup = new LessonTimePointGroup { Name = layout.Name };
            var lessonNumber = 0;
            foreach (var item in layout.Layouts.Where(item => item.TimeType == 0))
            {
                lessonNumber++;
                var key = LessonTimePointHelper.CreateKey(layoutId, item);
                var shortName = $"第 {lessonNumber} 节";
                var timeText = LessonTimePointHelper.FormatRange(item.StartTime, item.EndTime);

                lunchGroup.Items.Add(new LessonTimePointChoice
                {
                    Key = key,
                    DisplayName = $"{layout.Name} · {shortName} · {timeText}",
                    ShortName = shortName,
                    TimeText = timeText,
                    GroupName = layout.Name,
                    IsSelected = SettingsService.Settings.RecipeLunchTriggerLessonKeys.Contains(key),
                    SelectionChanged = (choice, selected) => Toggle(SettingsService.Settings.RecipeLunchTriggerLessonKeys, choice.Key, selected)
                });
                dinnerGroup.Items.Add(new LessonTimePointChoice
                {
                    Key = key,
                    DisplayName = $"{layout.Name} · {shortName} · {timeText}",
                    ShortName = shortName,
                    TimeText = timeText,
                    GroupName = layout.Name,
                    IsSelected = SettingsService.Settings.RecipeDinnerTriggerLessonKeys.Contains(key),
                    SelectionChanged = (choice, selected) => Toggle(SettingsService.Settings.RecipeDinnerTriggerLessonKeys, choice.Key, selected)
                });
            }

            if (lunchGroup.Items.Count > 0)
            {
                LunchGroups.Add(lunchGroup);
                DinnerGroups.Add(dinnerGroup);
            }
        }

        OnPropertyChanged(nameof(HasRecipeLessonChoices));
    }

    private static void Toggle(ObservableCollection<string> keys, string key, bool selected)
    {
        if (selected && !keys.Contains(key))
        {
            keys.Add(key);
        }
        else if (!selected)
        {
            keys.Remove(key);
        }
    }
}
