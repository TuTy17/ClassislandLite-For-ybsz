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
    public SettingsService SettingsService { get; }
    public RecipeService RecipeService { get; }
    public QqNotificationMirrorService QqNotificationMirrorService { get; }
    public ObservableCollection<LessonTimePointChoice> RecipeLessonChoices { get; } = [];

    public ExperimentalSettingsViewModel(
        SettingsService settingsService,
        IProfileService profileService,
        RecipeService recipeService,
        QqNotificationMirrorService qqNotificationMirrorService)
    {
        SettingsService = settingsService;
        RecipeService = recipeService;
        QqNotificationMirrorService = qqNotificationMirrorService;

        foreach (var (layoutId, layout) in profileService.Profile.TimeLayouts.OrderBy(pair => pair.Value.Name))
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
    }
}
