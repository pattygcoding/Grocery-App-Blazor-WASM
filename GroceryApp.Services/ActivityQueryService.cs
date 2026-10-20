using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <inheritdoc />
public class ActivityQueryService : IActivityQueryService
{
    /// <inheritdoc />
    public ActivityQueryResult Query(
        IEnumerable<ActivityEntry> entries,
        ActivityFilter? filter = null,
        int maxEntries = 400)
    {
        ArgumentNullException.ThrowIfNull(entries);

        filter ??= new ActivityFilter();

        var log = entries.ToList();

        var matching = log
            .Where(entry => Matches(entry, filter))
            .OrderByDescending(entry => entry.TimestampUtc)
            .ToList();

        var result = new ActivityQueryResult
        {
            TotalEntries = log.Count,
            MatchingEntries = matching.Count,
            KindCounts = matching
                .GroupBy(entry => entry.Kind)
                .ToDictionary(group => group.Key, group => group.Count()),
            UnitsIn = matching.Where(entry => entry.StockDelta > 0).Sum(entry => entry.StockDelta),
            UnitsOut = matching.Where(entry => entry.StockDelta < 0).Sum(entry => -entry.StockDelta)
        };

        // The newest entries are the ones worth rendering, so the cap keeps the head of the list rather than
        // sampling it. What was cut is reported so the page can say so.
        var shown = maxEntries > 0 ? matching.Take(maxEntries).ToList() : matching;
        result.Omitted = matching.Count - shown.Count;

        result.Days = shown
            .GroupBy(entry => DateOnly.FromDateTime(entry.TimestampUtc))
            .OrderByDescending(day => day.Key)
            .Select(day => new ActivityDay
            {
                Date = day.Key,
                Entries = day.ToList(),
                StockDelta = day.Sum(entry => entry.StockDelta)
            })
            .ToList();

        return result;
    }

    /// <summary>
    /// Decides whether one entry survives the filter. Every clause has to pass, so narrowing one control can
    /// never widen the result in another.
    /// </summary>
    /// <param name="entry">The entry to test.</param>
    /// <param name="filter">The filter in force.</param>
    /// <returns>True when the entry should be shown.</returns>
    private static bool Matches(ActivityEntry entry, ActivityFilter filter)
    {
        if (!filter.IncludeGenerated && entry.IsSeeded)
        {
            return false;
        }

        if (filter.Kind.HasValue && entry.Kind != filter.Kind.Value)
        {
            return false;
        }

        if (filter.StockMovementsOnly && !entry.IsStockMovement)
        {
            return false;
        }

        return MatchesSearch(entry, filter.SearchText);
    }

    /// <summary>
    /// Matches free text against the fields the timeline shows.
    /// </summary>
    /// <remarks>
    /// Multiple words all have to match, but each may match a different field, the same way the catalogue
    /// search works: "apple restock" finds the restock of the apple rather than nothing.
    /// </remarks>
    private static bool MatchesSearch(ActivityEntry entry, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        var terms = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return terms.All(term =>
            entry.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            entry.Detail.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            entry.Kind.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}