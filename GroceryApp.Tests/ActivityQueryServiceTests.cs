namespace GroceryApp.Tests;

/// <summary>
/// Tests for the activity timeline's query: what survives the filter, how it falls into days, and what it
/// reports about what it left out. The log is the one thing in the application that can grow without bound,
/// so the counts matter as much as the entries.
/// </summary>
public class ActivityQueryServiceTests
{
    private static readonly DateTime Day1 = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);

    private readonly ActivityQueryService _query = new();

    private static ActivityEntry Entry(
        ActivityKind kind,
        DateTime timestampUtc,
        string itemName = "Apple",
        int itemId = 1,
        int stockDelta = 0,
        string detail = "",
        bool isSeeded = false) => new()
    {
        Kind = kind,
        TimestampUtc = timestampUtc,
        ItemName = itemName,
        ItemId = itemId,
        StockDelta = stockDelta,
        Detail = detail,
        IsSeeded = isSeeded
    };

    #region Ordering and grouping

    [Fact]
    public void Query_OrdersTheEntriesNewestFirst()
    {
        var entries = new[]
        {
            Entry(ActivityKind.Created, Day1),
            Entry(ActivityKind.Updated, Day2),
            Entry(ActivityKind.Deleted, Day1.AddHours(1))
        };

        var result = _query.Query(entries);

        Assert.Equal(
            new[] { Day2, Day1.AddHours(1), Day1 },
            result.Days.SelectMany(day => day.Entries).Select(entry => entry.TimestampUtc));
    }

    [Fact]
    public void Query_GroupsTheEntriesIntoDaysNewestDayFirst()
    {
        var entries = new[]
        {
            Entry(ActivityKind.Created, Day1),
            Entry(ActivityKind.Updated, Day1.AddHours(3)),
            Entry(ActivityKind.Deleted, Day2)
        };

        var result = _query.Query(entries);

        Assert.Equal(2, result.Days.Count);
        Assert.Equal(new DateOnly(2026, 3, 2), result.Days[0].Date);
        Assert.Single(result.Days[0].Entries);
        Assert.Equal(new DateOnly(2026, 3, 1), result.Days[1].Date);
        Assert.Equal(2, result.Days[1].Entries.Count);
    }

    [Fact]
    public void Query_ReportsEachDaysNetChangeInUnits()
    {
        var entries = new[]
        {
            Entry(ActivityKind.BulkRestock, Day1, stockDelta: 12),
            Entry(ActivityKind.StockAdjusted, Day1.AddHours(2), stockDelta: -5),
            Entry(ActivityKind.Created, Day2)
        };

        var result = _query.Query(entries);

        Assert.Equal(7, result.Days.Single(day => day.Date == new DateOnly(2026, 3, 1)).StockDelta);
        Assert.Equal(0, result.Days.Single(day => day.Date == new DateOnly(2026, 3, 2)).StockDelta);
    }

    [Fact]
    public void Query_MarksADayThatIsEntirelyGenerated()
    {
        var entries = new[]
        {
            Entry(ActivityKind.Created, Day1, isSeeded: true),
            Entry(ActivityKind.StockAdjusted, Day1.AddHours(1), stockDelta: -2, isSeeded: true),
            Entry(ActivityKind.Updated, Day2)
        };

        var result = _query.Query(entries);

        Assert.True(result.Days.Single(day => day.Date == new DateOnly(2026, 3, 1)).IsEntirelyGenerated);
        Assert.False(result.Days.Single(day => day.Date == new DateOnly(2026, 3, 2)).IsEntirelyGenerated);
    }

    #endregion

    #region Filtering

    [Fact]
    public void Query_WithNoFilter_KeepsEveryEntry()
    {
        var entries = new[] { Entry(ActivityKind.Created, Day1), Entry(ActivityKind.Deleted, Day2) };

        var result = _query.Query(entries);

        Assert.Equal(2, result.TotalEntries);
        Assert.Equal(2, result.MatchingEntries);
        Assert.Equal(0, result.Omitted);
        Assert.False(new ActivityFilter().IsNarrowing);
    }

    [Fact]
    public void Query_WithNoFilterArgument_TreatsItAsEverything()
    {
        var result = _query.Query(new[] { Entry(ActivityKind.Created, Day1, isSeeded: true) });

        Assert.Equal(1, result.MatchingEntries);
    }

    [Fact]
    public void Query_OneKind_KeepsOnlyThatKind()
    {
        var entries = new[]
        {
            Entry(ActivityKind.Created, Day1),
            Entry(ActivityKind.StockAdjusted, Day1, stockDelta: -1),
            Entry(ActivityKind.BulkRestock, Day2, stockDelta: 4)
        };

        var result = _query.Query(entries, new ActivityFilter { Kind = ActivityKind.StockAdjusted });

        Assert.Equal(ActivityKind.StockAdjusted, Assert.Single(result.Days.Single().Entries).Kind);
        Assert.True(new ActivityFilter { Kind = ActivityKind.Created }.IsNarrowing);
    }

    [Fact]
    public void Query_StockMovementsOnly_DropsEntriesThatDidNotMoveStock()
    {
        var entries = new[]
        {
            Entry(ActivityKind.Updated, Day1),
            Entry(ActivityKind.StockAdjusted, Day1, stockDelta: -3),
            Entry(ActivityKind.BulkRestock, Day2, stockDelta: 9)
        };

        var result = _query.Query(entries, new ActivityFilter { StockMovementsOnly = true });

        Assert.Equal(2, result.MatchingEntries);
        Assert.All(result.Days.SelectMany(day => day.Entries), entry => Assert.True(entry.IsStockMovement));
    }

    [Fact]
    public void Query_ExcludingGenerated_LeavesOutTheSeededHistory()
    {
        var entries = new[]
        {
            Entry(ActivityKind.StockAdjusted, Day1, stockDelta: -2, isSeeded: true),
            Entry(ActivityKind.StockAdjusted, Day2, stockDelta: -1)
        };

        var result = _query.Query(entries, new ActivityFilter { IncludeGenerated = false });

        var entry = Assert.Single(result.Days.SelectMany(day => day.Entries));
        Assert.False(entry.IsSeeded);
        Assert.Equal(2, result.TotalEntries);
    }

    [Fact]
    public void Query_WhenTheEntriesAreNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _query.Query(null!));
    }

    #endregion

    #region Search

    [Fact]
    public void Query_SearchMatchesTheItemNameTheDetailAndTheKind()
    {
        var entries = new[]
        {
            Entry(ActivityKind.Updated, Day1, itemName: "Conference Pear", detail: "Now priced at $2.10"),
            Entry(ActivityKind.BulkRestock, Day2, itemName: "Banana", detail: "Restocked", stockDelta: 6)
        };

        Assert.Single(Matching(entries, "pear"));
        Assert.Single(Matching(entries, "2.10"));
        Assert.Single(Matching(entries, "bulk"));
    }

    [Fact]
    public void Query_WithTwoWords_RequiresBothToMatch()
    {
        var entries = new[]
        {
            Entry(ActivityKind.BulkRestock, Day1, itemName: "Banana", stockDelta: 6),
            Entry(ActivityKind.BulkRestock, Day2, itemName: "Conference Pear", stockDelta: 6)
        };

        Assert.Equal("Conference Pear", Assert.Single(Matching(entries, "pear restock")).ItemName);
    }

    [Fact]
    public void Query_SearchIsCaseInsensitive()
    {
        var entries = new[] { Entry(ActivityKind.Created, Day1, itemName: "Conference Pear") };

        Assert.Single(Matching(entries, "CONFERENCE"));
    }

    [Fact]
    public void Query_WhenNothingMatches_ReturnsNoDaysButStillReportsTheLog()
    {
        var entries = new[] { Entry(ActivityKind.Created, Day1) };

        var result = _query.Query(entries, new ActivityFilter { SearchText = "zzzz" });

        Assert.Empty(result.Days);
        Assert.Equal(1, result.TotalEntries);
        Assert.Equal(0, result.MatchingEntries);
    }

    #endregion

    #region Counts and the cap

    [Fact]
    public void Query_CountsEachKindAmongTheMatchingEntries()
    {
        var entries = new[]
        {
            Entry(ActivityKind.BulkRestock, Day1, stockDelta: 5),
            Entry(ActivityKind.BulkRestock, Day1, stockDelta: 2),
            Entry(ActivityKind.Deleted, Day2)
        };

        var result = _query.Query(entries);

        Assert.Equal(2, result.KindCounts[ActivityKind.BulkRestock]);
        Assert.Equal(1, result.KindCounts[ActivityKind.Deleted]);
        Assert.False(result.KindCounts.ContainsKey(ActivityKind.Reset));
    }

    [Fact]
    public void Query_TotalsUnitsInAndOutSeparately()
    {
        var entries = new[]
        {
            Entry(ActivityKind.BulkRestock, Day1, stockDelta: 20),
            Entry(ActivityKind.StockAdjusted, Day1, stockDelta: -3),
            Entry(ActivityKind.StockAdjusted, Day2, stockDelta: -4)
        };

        var result = _query.Query(entries);

        Assert.Equal(20, result.UnitsIn);
        Assert.Equal(7, result.UnitsOut);
    }

    [Fact]
    public void Query_CapsTheEntriesAndReportsHowManyWereLeftOut()
    {
        var entries = Enumerable.Range(0, 10)
            .Select(index => Entry(ActivityKind.Created, Day1.AddMinutes(index)))
            .ToList();

        var result = _query.Query(entries, new ActivityFilter(), maxEntries: 4);

        Assert.Equal(4, result.Days.SelectMany(day => day.Entries).Count());
        Assert.Equal(10, result.MatchingEntries);
        Assert.Equal(6, result.Omitted);
    }

    [Fact]
    public void Query_KeepsTheNewestEntriesWhenTheCapBites()
    {
        var entries = Enumerable.Range(0, 10)
            .Select(index => Entry(ActivityKind.Created, Day1.AddMinutes(index), itemName: $"Item {index}"))
            .ToList();

        var result = _query.Query(entries, new ActivityFilter(), maxEntries: 3);

        Assert.Equal(
            new[] { "Item 9", "Item 8", "Item 7" },
            result.Days.SelectMany(day => day.Entries).Select(entry => entry.ItemName));
    }

    [Fact]
    public void Query_WithTheCapDisabled_ReturnsEverything()
    {
        var entries = Enumerable.Range(0, 25)
            .Select(index => Entry(ActivityKind.Created, Day1.AddMinutes(index)))
            .ToList();

        var result = _query.Query(entries, new ActivityFilter(), maxEntries: 0);

        Assert.Equal(25, result.Days.SelectMany(day => day.Entries).Count());
        Assert.Equal(0, result.Omitted);
    }

    #endregion

    /// <summary>Runs a search and hands back what matched, which is the assertion most of these tests make.</summary>
    private List<ActivityEntry> Matching(IEnumerable<ActivityEntry> entries, string searchText) =>
        _query.Query(entries, new ActivityFilter { SearchText = searchText })
            .Days.SelectMany(day => day.Entries)
            .ToList();
}