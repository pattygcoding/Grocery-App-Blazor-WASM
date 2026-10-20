using GroceryApp.Models;

namespace GroceryApp.Services;

/// <summary>
/// Builds the demonstration history that gives a freshly seeded catalogue something to show on the
/// dashboard.
/// </summary>
/// <remarks>
/// <para>
/// A catalogue with no history has nothing to plot: no velocity, no days of cover, no movement to judge
/// dead stock by, and an activity feed with one line in it. Rather than present empty charts, the
/// application writes a plausible history once, and labels every entry it invents as generated so the
/// interface never passes it off as the visitor's own work.
/// </para>
/// <para>
/// The history is built by simulating each item day by day rather than by inventing numbers per chart.
/// Every item opens at zero and every arrival and departure is written to the log, which gives the whole
/// thing one property worth having: an item's stock on hand is exactly the sum of the movements recorded
/// against it.
/// </para>
/// <para>
/// Generation is deterministic. The random sequence for an item is seeded from its identifier and the
/// window length, so the same catalogue always produces the same history, which is what stops the charts
/// moving around when a page is reloaded and lets the behaviour be asserted in tests.
/// </para>
/// </remarks>
public static class DemoHistoryGenerator
{
    /// <summary>
    /// Length of the demonstration window in days.
    /// </summary>
    /// <remarks>
    /// Long enough to show a trend and a monthly comparison, short enough that the log stays a few
    /// hundred kilobytes rather than filling the visitor's storage.
    /// </remarks>
    public const int DefaultWindowDays = 60;

    /// <summary>Average units taken per movement, used only to size an item's opening stock.</summary>
    private const double AverageUnitsPerMovement = 2.5;

    /// <summary>
    /// Simulates a history for a catalogue, writing the resulting stock levels back onto the items.
    /// </summary>
    /// <param name="items">The items to simulate. Their stock, reorder point and last restock are updated in place.</param>
    /// <param name="nowUtc">The moment the window ends, supplied rather than read from the clock.</param>
    /// <param name="windowDays">Length of the window in days, clamped to between a week and a year.</param>
    /// <returns>The generated entries, oldest first, every one marked as generated.</returns>
    public static IReadOnlyList<ActivityEntry> Generate(
        IList<GroceryItem> items,
        DateTime nowUtc,
        int windowDays = DefaultWindowDays)
    {
        ArgumentNullException.ThrowIfNull(items);

        var window = Math.Clamp(windowDays, 7, 365);
        var startUtc = nowUtc.AddDays(-window);
        var entries = new List<ActivityEntry>();

        for (var index = 0; index < items.Count; index++)
        {
            SimulateItem(items[index], index, startUtc, window, entries);
        }

        entries.Sort((left, right) => left.TimestampUtc.CompareTo(right.TimestampUtc));

        return entries;
    }

    /// <summary>
    /// Works out how fast an item sells, before the per item variation is applied.
    /// </summary>
    /// <remarks>
    /// Cheap staples move fastest, which is both true of a grocery shop and what makes the demonstration
    /// figures differ between items rather than clustering around one number.
    /// </remarks>
    private static double BaseDailyDemand(decimal price) => price switch
    {
        < 1.5m => 1.1,
        < 3m => 0.8,
        < 6m => 0.5,
        _ => 0.3
    };

    /// <summary>
    /// What kind of stock story an item tells over the window.
    /// </summary>
    /// <remarks>
    /// The mix is deliberate. A catalogue where everything restocks on time produces a dashboard with
    /// nothing to report, so a minority of items are simulated as starved of restocking and another
    /// minority as carrying far more than they sell.
    /// </remarks>
    private enum StockStory
    {
        /// <summary>Tops up whenever it reaches its reorder point.</summary>
        Steady,

        /// <summary>Never gets restocked, so it empties and stays empty.</summary>
        Starved,

        /// <summary>Sells slowly against a large opening stock.</summary>
        Overstocked
    }

    /// <summary>
    /// Decides which stock story an item tells, spread across the catalogue by position rather than at
    /// random so a catalogue of any size gets a mix.
    /// </summary>
    private static StockStory StoryFor(int index) =>
        index % 7 == 3
            ? StockStory.Starved
            : index % 5 == 4
                ? StockStory.Overstocked
                : StockStory.Steady;

    /// <summary>
    /// Simulates one item across the window and writes the outcome back onto it.
    /// </summary>
    /// <param name="item">The item to simulate.</param>
    /// <param name="index">The item's position in the catalogue, which picks the stock story.</param>
    /// <param name="startUtc">Start of the window.</param>
    /// <param name="windowDays">Length of the window in days.</param>
    /// <param name="entries">The log being built, appended to in chronological order per item.</param>
    private static void SimulateItem(
        GroceryItem item,
        int index,
        DateTime startUtc,
        int windowDays,
        List<ActivityEntry> entries)
    {
        var story = StoryFor(index);

        // Seeded from the identifier so each item has its own sequence, and the same sequence every time.
        var random = new Random((item.Id * 7919) + windowDays);

        var dailyDemand = BaseDailyDemand(item.Price) * (0.7 + (random.NextDouble() * 0.6));
        var expectedSales = dailyDemand * windowDays * AverageUnitsPerMovement;
        var reorderPoint = Math.Max(2, (int)Math.Ceiling(dailyDemand * 5));

        var openingStock = story switch
        {
            // Never restocked, so it opens well below what it will sell.
            StockStory.Starved => (int)Math.Ceiling(expectedSales * 0.35),

            // Never restocked, so it opens well above what it will sell.
            StockStory.Overstocked => (int)Math.Ceiling(expectedSales * 1.9),

            // Restocked as it goes, so it opens below its top up level.
            _ => (int)Math.Ceiling(expectedSales * 0.5)
        };

        var openingStockAt = startUtc.AddHours(6);
        entries.Add(RestockEntry(item, openingStock, openingStockAt, "Opening stock"));

        var stock = openingStock;
        var lastRestock = openingStockAt;

        for (var day = 0; day < windowDays; day++)
        {
            var moment = startUtc
                .AddDays(day)
                .AddHours(random.Next(8, 19))
                .AddMinutes(random.Next(0, 60));

            if (stock <= reorderPoint && story == StockStory.Steady)
            {
                var topUp = (reorderPoint * 3) - stock + random.Next(0, 6);

                if (topUp > 0)
                {
                    stock += topUp;
                    entries.Add(RestockEntry(item, topUp, moment, "Restocked"));
                    lastRestock = moment;
                }

                continue;
            }

            if (random.NextDouble() >= dailyDemand)
            {
                continue;
            }

            // Never more than there is, so an item cannot sell stock it does not have.
            var sold = Math.Min(stock, 1 + random.Next(0, 4));

            if (sold <= 0)
            {
                continue;
            }

            stock -= sold;

            entries.Add(new ActivityEntry
            {
                TimestampUtc = moment,
                Kind = ActivityKind.StockAdjusted,
                ItemId = item.Id,
                ItemName = item.Name,
                StockDelta = -sold,
                Detail = "Sold",
                IsSeeded = true
            });
        }

        item.Stock = stock;
        item.ReorderPoint = reorderPoint;
        item.LastRestockedUtc = lastRestock;
    }

    /// <summary>
    /// Builds a restock entry.
    /// </summary>
    private static ActivityEntry RestockEntry(GroceryItem item, int quantity, DateTime atUtc, string detail) => new()
    {
        TimestampUtc = atUtc,
        Kind = ActivityKind.BulkRestock,
        ItemId = item.Id,
        ItemName = item.Name,
        StockDelta = quantity,
        Detail = detail,
        IsSeeded = true
    };
}
