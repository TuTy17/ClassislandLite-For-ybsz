using System;
using System.Collections.ObjectModel;
using System.Linq;
using ClassIsland.Core.Abstractions.Services;
using System.Collections.Generic;
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
    /// 按当前档案重建午餐/晚餐触发课程列表（支持多选，单周/双周自动合并）。
    /// </summary>
    public void RefreshLessonChoices()
    {
        LunchGroups.Clear();
        DinnerGroups.Clear();

        var settings = SettingsService.Settings;
        foreach (var group in LessonChoiceBuilder.Build(
                     _profileService,
                     key => settings.RecipeLunchTriggerLessonKeys.Contains(key),
                     (keys, selected) => Toggle(settings.RecipeLunchTriggerLessonKeys, keys, selected)))
        {
            LunchGroups.Add(group);
        }

        foreach (var group in LessonChoiceBuilder.Build(
                     _profileService,
                     key => settings.RecipeDinnerTriggerLessonKeys.Contains(key),
                     (keys, selected) => Toggle(settings.RecipeDinnerTriggerLessonKeys, keys, selected)))
        {
            DinnerGroups.Add(group);
        }

        OnPropertyChanged(nameof(HasRecipeLessonChoices));
    }

    private static void Toggle(ObservableCollection<string> selectedKeys, IReadOnlyList<string> keys, bool selected)
    {
        foreach (var key in keys)
        {
            if (selected && !selectedKeys.Contains(key))
            {
                selectedKeys.Add(key);
            }
            else if (!selected)
            {
                selectedKeys.Remove(key);
            }
        }
    }
}
