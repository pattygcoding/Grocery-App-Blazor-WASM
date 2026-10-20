using GroceryApp.Models;

namespace GroceryApp.Tests;

/// <summary>
/// Tests for the demonstration history: that it is reproducible, that it never invents stock it does not
/// account for, and that it produces a catalogue with something worth reporting.
/// </summary>
public class DemoHistoryGeneratorTests
{
    private static readonly DateTime Now = new(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// A catalogue with a spread of price bands, because the demand model differs by price.
    /// </summary>
    private static List<GroceryItem> Catalogue(int count)
    {
        var items = new List<GroceryItem>();

        for (var index = 1; index <= count; index++)
        {
            items.Add(new GroceryItem
            {
                Id = index,
                Name = $"Item {index}",
                Category = Category.Fruit,
                Price = 1m + ((index % 8) * 0.75m),
                CostToProduce = 0.5m,
                Stock = 10,
                ReorderPoint = 5
            });
        }

        return items;
    }

    private static int DeliveriesFor(IReadOnlyList<ActivityEntry> entries, int itemId) =>
        entries.Count(entry => entry.ItemId == itemId && entry.StockDelta > 0);

    private static IEnumerable<string> Describe(IReadOnlyList<ActivityEntry> entries) =>
        entries.Select(entry => $"{entry.ItemId}:{entry.TimestampUtc:O}:{entry.StockDelta}");

    [Fact]
    public void Generate_WhenTheCatalogueIsEmpty_ReturnsNoEntries()
    {
        Assert.Empty(DemoHistoryGenerator.Generate(new List<GroceryItem>(), Now));
    }

    [Fact]
    public void Generate_WhenTheCatalogueIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DemoHistoryGenerator.Generate(null!, Now));
    }

    [Fact]
    public void Generate_IsReproducibleForTheSameCatalogue()
    {
        var first = DemoHistoryGenerator.Generate(Catalogue(20), Now);
        var second = DemoHistoryGenerator.Generate(Catalogue(20), Now);

        Assert.Equal(Describe(first), Describe(second));
    }

    [Fact]
    public void Generate_WhenRunTwiceOverTheSameItems_DoesNotCompoundTheFirstRun()
    {
        // Each item's stock is worked out from scratch, so running the simulation again over items it has
        // already adjusted must not drain them a second time.
        var items = Catalogue(20);

        DemoHistoryGenerator.Generate(items, Now);
        var afterFirst = items.Select(item => item.Stock).ToList();

        DemoHistoryGenerator.Generate(items, Now);

        Assert.Equal(afterFirst, items.Select(item => item.Stock));
    }

    [Fact]
    public void Generate_LeavesEveryItemsStockEqualToTheSumOfItsRecordedMovements()
    {
        // The property the whole demonstration rests on: the catalogue and its history tell the same
        // story, so no figure derived from one can contradict the other.
        var items = Catalogue(30);
        var entries = DemoHistoryGenerator.Generate(items, Now);

        foreach (var item in items)
        {
            var recorded = entries.Where(entry => entry.ItemId == item.Id).Sum(entry => entry.StockDelta);

            Assert.Equal(recorded, item.Stock);
        }
    }

    [Fact]
    public void Generate_NeverTakesMoreStockOutThanAnItemHas()
    {
        var items = Catalogue(20);
        var entries = DemoHistoryGenerator.Generate(items, Now);

        foreach (var item in items)
        {
            var running = 0;

            foreach (var entry in entries
                .Where(entry => entry.ItemId == item.Id)
                .OrderBy(entry => entry.TimestampUtc))
            {
                running += entry.StockDelta;

                Assert.True(running >= 0, $"{item.Name} ran down to {running} at {entry.TimestampUtc:O}");
            }
        }
    }

    [Fact]
    public void Generate_LeavesNoItemWithNegativeStock()
    {
        var items = Catalogue(30);
        DemoHistoryGenerator.Generate(items, Now);

        Assert.All(items, item => Assert.True(item.Stock >= 0, $"{item.Name} ended at {item.Stock}"));
    }

    [Fact]
    public void Generate_RecordsEveryMovementInsideTheWindow()
    {
        var entries = DemoHistoryGenerator.Generate(Catalogue(20), Now, windowDays: 60);

        Assert.All(entries, entry => Assert.InRange(entry.TimestampUtc, Now.AddDays(-60), Now));
    }

    [Fact]
    public void Generate_ClampsTheWindowToBetweenAWeekAndAYear()
    {
        Assert.All(
            DemoHistoryGenerator.Generate(Catalogue(5), Now, windowDays: 1),
            entry => Assert.True(entry.TimestampUtc >= Now.AddDays(-7)));

        Assert.All(
            DemoHistoryGenerator.Generate(Catalogue(5), Now, windowDays: 9999),
            entry => Assert.True(entry.TimestampUtc >= Now.AddDays(-365)));
    }

    [Fact]
    public void Generate_ReturnsEntriesOldestFirst()
    {
        var entries = DemoHistoryGenerator.Generate(Catalogue(20), Now);

        var expected = entries.OrderBy(entry => entry.TimestampUtc).Select(entry => entry.TimestampUtc);

        Assert.Equal(expected, entries.Select(entry => entry.TimestampUtc));
    }

    [Fact]
    public void Generate_MarksEveryEntryAsGenerated()
    {
        // The interface says which entries it invented, so none of them may arrive unlabelled.
        var entries = DemoHistoryGenerator.Generate(Catalogue(20), Now);

        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.True(entry.IsSeeded));
    }

    [Fact]
    public void Generate_DeliversOpeningStockToEveryItem()
    {
        var items = Catalogue(20);
        var entries = DemoHistoryGenerator.Generate(items, Now);

        Assert.All(items, item =>
        {
            var opening = entries.SingleOrDefault(entry =>
                entry.ItemId == item.Id && entry.Detail == "Opening stock");

            Assert.NotNull(opening);
            Assert.True(opening!.StockDelta > 0);
        });
    }

    [Fact]
    public void Generate_LeavesSomeItemsWithoutASecondDeliverySoTheCatalogueHasASpread()
    {
        // Items that are never restocked inside the window are what produce empty and overstocked lines.
        // Without them the dashboard has nothing to report.
        var items = Catalogue(30);
        var entries = DemoHistoryGenerator.Generate(items, Now);

        var singleDelivery = items.Count(item => DeliveriesFor(entries, item.Id) == 1);

        Assert.True(singleDelivery > 0, "every item was restocked inside the window");
        Assert.True(singleDelivery < items.Count, "no item was restocked inside the window");
    }

    [Fact]
    public void Generate_GivesItemsAReorderPointDerivedFromTheirMeasuredDemand()
    {
        var items = Catalogue(20);
        DemoHistoryGenerator.Generate(items, Now);

        Assert.All(items, item => Assert.True(item.ReorderPoint >= 2, $"{item.Name} kept {item.ReorderPoint}"));
    }

    [Fact]
    public void Generate_TimestampsTheLastRestockOfEveryItemItAdjusted()
    {
        var items = Catalogue(20);
        DemoHistoryGenerator.Generate(items, Now);

        Assert.All(items, item =>
        {
            Assert.NotNull(item.LastRestockedUtc);
            Assert.InRange(item.LastRestockedUtc!.Value, Now.AddDays(-60), Now);
        });
    }
}
