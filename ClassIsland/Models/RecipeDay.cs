using System.Collections.Generic;
using System.Linq;

namespace ClassIsland.Models;

public sealed class RecipeSection
{
    public string Name { get; init; } = "";
    public IReadOnlyList<string> Items { get; init; } = [];
    public string ItemsText => string.Join("  ·  ", Items);
}

public sealed class RecipeDay
{
    public int Month { get; init; }
    public int Day { get; init; }
    public string Lunch { get; init; } = "";
    public string Dinner { get; init; } = "";
    public IReadOnlyList<RecipeSection> LunchSections { get; init; } = [];
    public IReadOnlyList<RecipeSection> DinnerSections { get; init; } = [];

    public bool MatchesDate(System.DateTime date) => date.Month == Month && date.Day == Day;

    public IReadOnlyList<RecipeSection> GetSections(bool lunch) => lunch ? LunchSections : DinnerSections;
}
