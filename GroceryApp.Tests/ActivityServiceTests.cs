using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Tests;

/// <summary>
/// Tests for the activity log: what gets recorded, how it is ordered and pruned, and that it survives a
/// reload.
/// </summary>
public class ActivityServiceTests
{
    private static readonly DateTime Now = new(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryAppStateStore _store = new();
    private readonly ActivityService _activity;

    public ActivityServiceTests()
    {
        _activity = new ActivityService(new ActivityLogStore(_store));
    }

    private static GroceryItemDto Item(int id, string name) => new()
    {
        Id = id,
        Name = name,
        Price = 1m,
        CostToProduce = 0.5m,
        Stock = 5
    };

    private static ActivityEntry Entry(int itemId, double daysAgo, int delta = -1) => new()
    {
        ItemId = itemId,
        ItemName = $"Item {itemId}",
        StockDelta = delta,
        Kind = ActivityKind.StockAdjusted,
        TimestampUtc = Now.AddDays(-daysAgo)
    };

    [Fact]
    public async Task GetAllAsync_WhenNothingIsRecorded_IsEmpty()
    {
        Assert.Empty(await _activity.GetAllAsync());
        Assert.Equal(0, await _activity.CountAsync());
    }

    [Fact]
    public async Task RecordItemAsync_RecordsTheItemTheStockChangeAndTheDetail()
    {
        await _activity.RecordItemAsync(ActivityKind.StockAdjusted, Item(7, "Conference Pear"), -3, "Sold at market");

        var entry = Assert.Single(await _activity.GetAllAsync());

        Assert.Equal(ActivityKind.StockAdjusted, entry.Kind);
        Assert.Equal(7, entry.ItemId);
        Assert.Equal("Conference Pear", entry.ItemName);
        Assert.Equal(-3, entry.StockDelta);
        Assert.Equal("Sold at market", entry.Detail);
        Assert.False(entry.IsSeeded);
    }

    [Fact]
    public async Task RecordCatalogueAsync_RecordsAnEntryThatBelongsToNoItem()
    {
        await _activity.RecordCatalogueAsync(ActivityKind.Reset, "Demo data restored");

        var entry = Assert.Single(await _activity.GetAllAsync());

        Assert.Equal(0, entry.ItemId);
        Assert.Equal(string.Empty, entry.ItemName);
        Assert.Equal("Demo data restored", entry.Detail);
    }

    [Fact]
    public async Task RecordItemAsync_WhenTheItemIsNull_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _activity.RecordItemAsync(ActivityKind.Updated, null!));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsNewestFirst()
    {
        await _activity.RecordManyAsync(new[] { Entry(1, 5), Entry(2, 1), Entry(3, 3) });

        var entries = await _activity.GetAllAsync();

        Assert.Equal(new[] { 2, 3, 1 }, entries.Select(entry => entry.ItemId));
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsAtMostTheRequestedNumber()
    {
        await _activity.RecordManyAsync(new[] { Entry(1, 5), Entry(2, 3), Entry(3, 1) });

        Assert.Equal(new[] { 3, 2 }, (await _activity.GetRecentAsync(2)).Select(entry => entry.ItemId));
    }

    [Fact]
    public async Task GetRecentAsync_WhenTheCountIsNotPositive_ReturnsNothing()
    {
        await _activity.RecordItemAsync(ActivityKind.Created, Item(1, "Apple"));

        Assert.Empty(await _activity.GetRecentAsync(0));
        Assert.Empty(await _activity.GetRecentAsync(-5));
    }

    [Fact]
    public async Task GetBetweenAsync_ReturnsOnlyTheEntriesInTheWindowNewestFirst()
    {
        await _activity.RecordManyAsync(new[] { Entry(1, 10), Entry(2, 3), Entry(3, 1) });

        var window = await _activity.GetBetweenAsync(Now.AddDays(-4), Now.AddDays(-0.5));

        Assert.Equal(new[] { 3, 2 }, window.Select(entry => entry.ItemId));
    }

    [Fact]
    public async Task RecordManyAsync_MergesABatchIntoChronologicalOrderWhateverOrderItArrivesIn()
    {
        await _activity.RecordManyAsync(new[] { Entry(1, 1), Entry(2, 9), Entry(3, 5) });

        Assert.Equal(new[] { 1, 3, 2 }, (await _activity.GetAllAsync()).Select(entry => entry.ItemId));
    }

    [Fact]
    public async Task RecordManyAsync_WhenTheBatchSharesOneTimestamp_KeepsEveryEntry()
    {
        var batch = Enumerable.Range(1, 20).Select(id => Entry(id, 2)).ToList();

        await _activity.RecordManyAsync(batch);

        Assert.Equal(20, await _activity.CountAsync());
    }

    [Fact]
    public async Task RecordManyAsync_PrunesTheOldestEntriesOnceTheLogIsFull()
    {
        var batch = Enumerable
            .Range(0, ActivityService.MaxEntries + 5)
            .Select(index => new ActivityEntry
            {
                ItemId = index,
                Kind = ActivityKind.StockAdjusted,
                StockDelta = -1,
                TimestampUtc = Now.AddMinutes(-index)
            })
            .ToList();

        await _activity.RecordManyAsync(batch);

        var stored = await _activity.GetAllAsync();

        // The newest MaxEntries survive: indices 0 to MaxEntries - 1, since 0 is the most recent minute.
        Assert.Equal(ActivityService.MaxEntries, stored.Count);
        Assert.Equal(0, stored[0].ItemId);
        Assert.Equal(ActivityService.MaxEntries - 1, stored[^1].ItemId);
    }

    [Fact]
    public async Task ClearAsync_RemovesEveryEntry()
    {
        await _activity.RecordItemAsync(ActivityKind.Created, Item(1, "Apple"));

        await _activity.ClearAsync();

        Assert.Equal(0, await _activity.CountAsync());
        Assert.Empty(await _activity.GetAllAsync());
    }

    [Fact]
    public async Task TheLogSurvivesANewServiceOverTheSameStore()
    {
        await _activity.RecordItemAsync(ActivityKind.Created, Item(4, "Brie"), 10, "Added");

        // A second service over the same storage is what the next page load looks like.
        var reloaded = new ActivityService(new ActivityLogStore(_store));
        var entry = Assert.Single(await reloaded.GetAllAsync());

        Assert.Equal(4, entry.ItemId);
        Assert.Equal(10, entry.StockDelta);
    }
}
