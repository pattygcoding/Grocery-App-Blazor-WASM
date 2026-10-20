using GroceryApp.Models;

namespace GroceryApp.Models.DTOs;

/// <summary>
/// What the activity timeline is being asked for.
/// </summary>
public class ActivityFilter
{
    /// <summary>Free text matched against the item name, the detail and the kind.</summary>
    public string? SearchText { get; set; }

    /// <summary>One kind to show, or null for every kind.</summary>
    public ActivityKind? Kind { get; set; }

    /// <summary>When true only entries that moved stock are shown.</summary>
    public bool StockMovementsOnly { get; set; }

    /// <summary>
    /// When false the generated demonstration history is left out, so the timeline shows only what the
    /// visitor did themselves. On by default, because the figures everywhere else read the generated
    /// history and a timeline that disagreed with them would look like a bug.
    /// </summary>
    public bool IncludeGenerated { get; set; } = true;

    /// <summary>True when any clause is narrowing the log.</summary>
    public bool IsNarrowing =>
        !string.IsNullOrWhiteSpace(SearchText) || Kind.HasValue || StockMovementsOnly || !IncludeGenerated;
}

/// <summary>
/// One day of the timeline.
/// </summary>
public class ActivityDay
{
    /// <summary>The day these entries were recorded on, in UTC.</summary>
    public DateOnly Date { get; set; }

    /// <summary>The entries recorded that day, newest first.</summary>
    public List<ActivityEntry> Entries { get; set; } = new();

    /// <summary>Net change in units on hand for the day, so a day of selling reads as a negative.</summary>
    public int StockDelta { get; set; }

    /// <summary>How many of the day's entries were generated rather than recorded.</summary>
    public int GeneratedCount => Entries.Count(entry => entry.IsSeeded);

    /// <summary>True when every entry that day was generated.</summary>
    public bool IsEntirelyGenerated => Entries.Count > 0 && Entries.All(entry => entry.IsSeeded);
}

/// <summary>
/// The timeline a query produced, together with the counts that describe what was left out.
/// </summary>
public class ActivityQueryResult
{
    /// <summary>The days, newest first.</summary>
    public List<ActivityDay> Days { get; set; } = new();

    /// <summary>Every entry in the log, before filtering.</summary>
    public int TotalEntries { get; set; }

    /// <summary>How many entries matched the filter.</summary>
    public int MatchingEntries { get; set; }

    /// <summary>
    /// How many matching entries were not returned because the page only renders so many. Reported rather
    /// than hidden: a timeline that silently stops is worse than one that says it stopped.
    /// </summary>
    public int Omitted { get; set; }

    /// <summary>How many matching entries of each kind, so the filter can show what is available.</summary>
    public Dictionary<ActivityKind, int> KindCounts { get; set; } = new();

    /// <summary>Total units added and removed across the matching entries.</summary>
    public int UnitsIn { get; set; }

    /// <summary>Total units taken out of stock across the matching entries.</summary>
    public int UnitsOut { get; set; }
}