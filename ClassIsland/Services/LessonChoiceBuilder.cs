using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Helpers;
using ClassIsland.Models;
using ClassIsland.Shared.Models.Profile;

namespace ClassIsland.Services;

/// <summary>
/// 把档案里的时间表整理成可勾选的课程分组。
/// 同一张“周二”如果存在“周二 单周”和“周二 双周”两个时间表，会合并成一组，
/// 勾选一节时同时作用到单周和双周两份时间表上。
/// </summary>
public static class LessonChoiceBuilder
{
    private static readonly string[] ParitySuffixes = ["单周", "双周", "单", "双"];

    public static List<LessonTimePointGroup> Build(
        IProfileService profileService,
        Func<string, bool> isSelected,
        Action<IReadOnlyList<string>, bool> setSelected)
    {
        var layoutEntries = profileService.Profile.TimeLayouts
            .Select(pair => (Id: pair.Key, Layout: pair.Value))
            .OrderBy(entry => BaseName(entry.Layout.Name))
            .ThenBy(entry => ParityOrder(entry.Layout.Name))
            .ToList();

        var groups = new List<LessonTimePointGroup>();
        var groupMap = new Dictionary<string, (LessonTimePointGroup Group, List<(Guid Id, TimeLayout Layout)> Layouts)>();

        foreach (var entry in layoutEntries)
        {
            var baseName = BaseName(entry.Layout.Name);
            if (!groupMap.TryGetValue(baseName, out var existing))
            {
                existing = (new LessonTimePointGroup { Name = baseName }, []);
                groupMap[baseName] = existing;
                groups.Add(existing.Group);
            }

            existing.Layouts.Add((entry.Id, entry.Layout));
        }

        foreach (var (group, layouts) in groupMap.Values)
        {
            var hasParity = layouts.Count > 1 || layouts.Any(pair => ParityOrder(pair.Layout.Name) != 0);
            if (hasParity)
            {
                group.Name = $"{group.Name}（单/双周）";
            }

            var maxLessons = layouts
                .Select(pair => pair.Layout.Layouts.Count(item => item.TimeType == 0))
                .DefaultIfEmpty(0)
                .Max();

            for (var lessonIndex = 0; lessonIndex < maxLessons; lessonIndex++)
            {
                var keys = new List<string>();
                TimeLayoutItem? displayItem = null;
                foreach (var (layoutId, layout) in layouts)
                {
                    var item = layout.Layouts.Where(l => l.TimeType == 0).ElementAtOrDefault(lessonIndex);
                    if (item == null)
                    {
                        continue;
                    }

                    displayItem ??= item;
                    keys.Add(LessonTimePointHelper.CreateKey(layoutId, item));
                }

                if (displayItem == null || keys.Count == 0)
                {
                    continue;
                }

                var shortName = $"第 {lessonIndex + 1} 节";
                var timeText = LessonTimePointHelper.FormatRange(displayItem.StartTime, displayItem.EndTime);
                group.Items.Add(new LessonTimePointChoice
                {
                    Key = keys[0],
                    Keys = keys,
                    DisplayName = $"{group.Name} · {shortName} · {timeText}",
                    ShortName = shortName,
                    TimeText = timeText,
                    GroupName = group.Name,
                    IsSelected = keys.Any(isSelected),
                    SelectionChanged = (_, selected) => setSelected(keys, selected)
                });
            }
        }

        return groups.Where(group => group.Items.Count > 0).ToList();
    }

    private static string BaseName(string name)
    {
        var trimmed = (name ?? "").Trim();
        foreach (var suffix in ParitySuffixes)
        {
            if (trimmed.EndsWith(suffix, StringComparison.Ordinal))
            {
                return trimmed[..^suffix.Length].TrimEnd();
            }
        }

        return trimmed;
    }

    private static int ParityOrder(string name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.EndsWith("双周", StringComparison.Ordinal) || trimmed.EndsWith("双", StringComparison.Ordinal))
        {
            return 2;
        }

        if (trimmed.EndsWith("单周", StringComparison.Ordinal) || trimmed.EndsWith("单", StringComparison.Ordinal))
        {
            return 1;
        }

        return 0;
    }
}
