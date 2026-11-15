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
    public ObservableCollection<LessonTimePointChoice> RecipeLessonChoices { get; } = [];

    public bool HasRecipeLessonChoices => RecipeLessonChoices.Count > 0;

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
    /// 按当前档案重建课程列表。设置页可能被缓存，档案里的时间表变化后需要重新读取。
    /// </summary>
    public void RefreshLessonChoices()
    {
        RecipeLessonChoices.Clear();
        foreach (var (layoutId, layout) in _profileService.Profile.TimeLayouts.OrderBy(pair => pair.Value.Name))
        {
            var lessonNumber = 0;
            foreach (var item in layout.Layouts.Where(item => item.TimeType == 0))
            {
                lessonNumber++;
                RecipeLessonChoices.Add(new LessonTimePointChoice
                {
                    Key = LessonTimePointHelper.CreateKey(layoutId, item),
                    DisplayName = $"{layout.Name} · 第 {lessonNumber} 节 · {LessonTimePointHelper.FormatRange(item.StartTime, item.EndTime)}"
                });
            }
        }

        OnPropertyChanged(nameof(HasRecipeLessonChoices));
    }
}
