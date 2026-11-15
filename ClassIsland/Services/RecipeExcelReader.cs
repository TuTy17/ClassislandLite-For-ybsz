using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClassIsland.Models;

namespace ClassIsland.Services;

public static partial class RecipeExcelReader
{
    public static IReadOnlyList<RecipeDay> Read(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetEntry = archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName, "xl/worksheets/sheet1.xml", StringComparison.OrdinalIgnoreCase));
        if (sheetEntry == null)
        {
            return [];
        }

        using var sheetStream = sheetEntry.Open();
        var document = XDocument.Load(sheetStream);
        var rows = document.Descendants()
            .Where(element => element.Name.LocalName == "row")
            .Select(row => new ParsedRow(
                int.TryParse((string?)row.Attribute("r"), out var rowNumber) ? rowNumber : 0,
                row.Descendants()
                    .Where(element => element.Name.LocalName == "c")
                    .Select(cell => new ParsedCell(
                        GetColumnIndex((string?)cell.Attribute("r")),
                        ReadCellValue(cell, sharedStrings)))
                    .Where(cell => cell.Column >= 0)
                    .GroupBy(cell => cell.Column)
                    .ToDictionary(group => group.Key, group => group.Last().Value)))
            .OrderBy(row => row.Index)
            .ToList();

        var header = rows.FirstOrDefault(row =>
            row.Values.Values.Any(value => NormalizeHeader(value).Contains("午餐", StringComparison.Ordinal)) ||
            row.Values.Values.Any(value => NormalizeHeader(value).Contains("晚餐", StringComparison.Ordinal)));
        if (header == null)
        {
            return [];
        }

        var dateColumn = header.Values.FirstOrDefault(pair => NormalizeHeader(pair.Value).Contains("日期", StringComparison.Ordinal)).Key;
        if (dateColumn == 0 && !header.Values.ContainsKey(0))
        {
            dateColumn = 0;
        }

        var lunchColumn = FindMealColumn(header.Values, "午餐");
        var dinnerColumn = FindMealColumn(header.Values, "晚餐");
        var nightSnackColumn = FindMealColumn(header.Values, "夜宵");
        var specialColumn = FindMealColumn(header.Values, "特色菜");
        var friedRiceColumn = FindMealColumn(header.Values, "炒饭");
        var lastColumn = rows.SelectMany(row => row.Values.Keys).DefaultIfEmpty(15).Max();

        lunchColumn = lunchColumn >= 0 ? lunchColumn : 5;
        dinnerColumn = dinnerColumn > lunchColumn ? dinnerColumn : Math.Max(lunchColumn + 1, 10);
        nightSnackColumn = nightSnackColumn > dinnerColumn ? nightSnackColumn : lastColumn + 1;
        var lunchMainEnd = new[] { specialColumn, friedRiceColumn, dinnerColumn }
            .Where(column => column > lunchColumn)
            .DefaultIfEmpty(dinnerColumn)
            .Min();

        var result = new List<RecipeDay>();
        RecipeBuilder? builder = null;
        foreach (var row in rows.Where(row => row.Index > header.Index))
        {
            if (TryParseMonthDay(row.Values.GetValueOrDefault(dateColumn), out var month, out var day))
            {
                if (builder != null)
                {
                    result.Add(builder.Build());
                }
                builder = new RecipeBuilder(month, day);
            }

            if (builder != null)
            {
                builder.AddSection("营养套餐", EnumerateMealValues(row.Values, lunchColumn, lunchMainEnd));
                if (specialColumn > lunchColumn && specialColumn < dinnerColumn)
                {
                    builder.AddSection("特色菜", EnumerateMealValues(row.Values, specialColumn, specialColumn + 1));
                }
                if (friedRiceColumn > lunchColumn && friedRiceColumn < dinnerColumn)
                {
                    builder.AddSection("炒饭 / 减脂餐", EnumerateMealValues(row.Values, friedRiceColumn, friedRiceColumn + 1));
                }
                builder.AddSection("营养套餐", EnumerateMealValues(row.Values, dinnerColumn, nightSnackColumn), dinner: true);
            }
        }

        if (builder != null)
        {
            result.Add(builder.Build());
        }
        return result;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.Entries.FirstOrDefault(item =>
            string.Equals(item.FullName, "xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "si")
            .Select(item => string.Concat(item.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value)))
            .ToList();
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(cell.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value));
        }

        var value = cell.Descendants().FirstOrDefault(element => element.Name.LocalName == "v")?.Value ?? "";
        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
            index >= 0 && index < sharedStrings.Count)
        {
            return sharedStrings[index];
        }

        return value;
    }

    private static int FindMealColumn(IReadOnlyDictionary<int, string> headerValues, string mealName)
    {
        return headerValues
            .Where(pair => NormalizeHeader(pair.Value).Contains(mealName, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .DefaultIfEmpty(-1)
            .Min();
    }

    private static string NormalizeHeader(string value)
    {
        return Regex.Replace(value, @"\s+", "");
    }

    private static IEnumerable<string> EnumerateMealValues(
        IReadOnlyDictionary<int, string> values,
        int startColumn,
        int endColumnExclusive)
    {
        for (var column = startColumn; column < endColumnExclusive; column++)
        {
            if (!values.TryGetValue(column, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
            if (normalized is "午餐" or "晚餐" or "夜宵" or "菜品")
            {
                continue;
            }
            yield return normalized;
        }
    }

    private static int GetColumnIndex(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return -1;
        }

        var index = 0;
        var foundLetter = false;
        foreach (var character in reference)
        {
            if (!char.IsLetter(character))
            {
                break;
            }
            foundLetter = true;
            index = index * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        }
        return foundLetter ? index - 1 : -1;
    }

    private static bool TryParseMonthDay(string? text, out int month, out int day)
    {
        month = 0;
        day = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = MonthDayRegex().Match(text);
        if (!match.Success ||
            !int.TryParse(match.Groups["month"].Value, out month) ||
            !int.TryParse(match.Groups["day"].Value, out day))
        {
            return false;
        }
        return month is >= 1 and <= 12 && day is >= 1 and <= 31;
    }

    [GeneratedRegex(@"(?:^|\D)(?<month>\d{1,2})[.\-/月](?<day>\d{1,2})(?:\D|$)")]
    private static partial Regex MonthDayRegex();

    private sealed record ParsedRow(int Index, IReadOnlyDictionary<int, string> Values);
    private sealed record ParsedCell(int Column, string Value);

    private sealed class RecipeBuilder(int month, int day)
    {
        private readonly List<SectionBuilder> _lunchSections = [];
        private readonly List<SectionBuilder> _dinnerSections = [];

        public void AddSection(string name, IEnumerable<string> values, bool dinner = false)
        {
            var sections = dinner ? _dinnerSections : _lunchSections;
            var section = sections.FirstOrDefault(item => item.Name == name);
            if (section == null)
            {
                section = new SectionBuilder(name);
                sections.Add(section);
            }
            section.Add(values);
        }

        public RecipeDay Build()
        {
            var lunchSections = _lunchSections.Select(item => item.Build()).ToArray();
            var dinnerSections = _dinnerSections.Select(item => item.Build()).ToArray();
            return new RecipeDay
            {
                Month = month,
                Day = day,
                LunchSections = lunchSections,
                DinnerSections = dinnerSections,
                Lunch = string.Join("  ·  ", lunchSections.SelectMany(item => item.Items)),
                Dinner = string.Join("  ·  ", dinnerSections.SelectMany(item => item.Items))
            };
        }

        private sealed class SectionBuilder(string name)
        {
            private readonly List<string> _items = [];
            private readonly HashSet<string> _itemSet = new(StringComparer.Ordinal);

            public string Name { get; } = name;

            public void Add(IEnumerable<string> items)
            {
                foreach (var item in items)
                {
                    if (_itemSet.Add(item))
                    {
                        _items.Add(item);
                    }
                }
            }

            public RecipeSection Build() => new() { Name = Name, Items = _items.ToArray() };
        }
    }
}
