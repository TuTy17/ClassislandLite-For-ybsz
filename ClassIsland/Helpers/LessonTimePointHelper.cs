using System;
using System.Globalization;
using ClassIsland.Shared.Models.Profile;

namespace ClassIsland.Helpers;

public static class LessonTimePointHelper
{
    public static string CreateKey(Guid timeLayoutId, TimeLayoutItem item)
    {
        return $"{timeLayoutId:N}|{item.StartTime.Ticks}-{item.EndTime.Ticks}";
    }

    public static string CreateKey(
        Guid timeLayoutId,
        TimeSpan startTime,
        TimeSpan endTime)
    {
        return $"{timeLayoutId:N}|{startTime.Ticks}-{endTime.Ticks}";
    }

    public static bool TryGetTimes(string key, out TimeSpan startTime, out TimeSpan endTime)
    {
        startTime = default;
        endTime = default;
        var separator = key.IndexOf('|');
        if (separator < 0)
        {
            return false;
        }

        var range = key[(separator + 1)..];
        var rangeSeparator = range.IndexOf('-');
        if (rangeSeparator < 0 ||
            !long.TryParse(range[..rangeSeparator], NumberStyles.Integer, CultureInfo.InvariantCulture, out var startTicks) ||
            !long.TryParse(range[(rangeSeparator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var endTicks))
        {
            return false;
        }

        startTime = TimeSpan.FromTicks(startTicks);
        endTime = TimeSpan.FromTicks(endTicks);
        return true;
    }

    public static string FormatRange(TimeSpan startTime, TimeSpan endTime)
    {
        return $"{startTime:hh\\:mm}-{endTime:hh\\:mm}";
    }
}
