using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// The rules behind the analysis: how a movement window is totalled, how urgency is scored, how a
/// score becomes a grade, and how the findings are worded.
/// </summary>
/// <remarks>
/// Split from the analysis itself so the arithmetic that produces the numbers and the judgements about
/// what those numbers mean can be read and argued with separately. Numbers embedded in the finding text
/// are formatted invariantly, because that text is prose the analysis owns rather than part of the
/// interface's localised presentation.
/// </remarks>
public partial class InventoryAnalyticsService
{
    /// <summary>
    /// Totals the units that left stock per item inside a window.
    /// </summary>
    /// <param name="activity">The activity log. Null is treated as an empty log.</param>
    /// <param name="fromUtc">Start of the window, inclusive.</param>
    /// <param name="toUtc">End of the window, inclusive.</param>
    /// <returns>Units removed per item identifier. Items that did not move are absent from the result.</returns>
    private static Dictionary<int, int> UnitsMovedByItem(
        IEnumerable<ActivityEntry>? activity,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var totals = new Dictionary<int, int>();

        if (activity is null)
        {
            return totals;
        }

        foreach (var entry in activity)
        {
            // Only outward movements count as demand. A restock is supply, not something the item sold.
            if (entry.StockDelta >= 0 || entry.TimestampUtc < fromUtc || entry.TimestampUtc > toUtc)
            {
                continue;
            }

            totals[entry.ItemId] = totals.GetValueOrDefault(entry.ItemId) + Math.Abs(entry.StockDelta);
        }

        return totals;
    }

    /// <summary>
    /// Explains why an item is on the restock worksheet.
    /// </summary>
    private static string DescribeReorder(
        StockStatus status,
        GroceryItemDto item,
        int reorderPoint,
        int leadTimeDays,
        double unitsPerDay,
        bool usesMeasuredDemand)
    {
        if (!usesMeasuredDemand)
        {
            return status == StockStatus.OutOfStock
                ? FormattableString.Invariant($"Out of stock, at or below the reorder point of {reorderPoint} it is set to")
                : FormattableString.Invariant($"Down to {item.Stock}, at or below the reorder point of {reorderPoint} it is set to");
        }

        return status switch
        {
            StockStatus.OutOfStock => FormattableString.Invariant(
                $"Out of stock while selling about {unitsPerDay:0.##} per day"),

            StockStatus.Critical => FormattableString.Invariant(
                $"{item.Stock} left, under a quarter of the {reorderPoint} that covers its demand"),

            _ => FormattableString.Invariant(
                $"{item.Stock} left against a reorder point of {reorderPoint}, which covers {leadTimeDays} days of lead time at {unitsPerDay:0.##} per day")
        };
    }

    /// <summary>
    /// Scores how badly an item needs restocking, from 0 to 100.
    /// </summary>
    /// <remarks>
    /// Three things push an item up the list: how empty it is, how far below its own threshold it has
    /// fallen, and how fast it moves. The third only counts when the demand behind it was measured
    /// rather than assumed, so a missing history cannot inflate an order.
    /// </remarks>
    private static int ScoreUrgency(
        StockStatus status,
        int stock,
        int reorderPoint,
        double unitsPerDay,
        bool usesMeasuredDemand)
    {
        var baseScore = status switch
        {
            StockStatus.OutOfStock => 55,
            StockStatus.Critical => 40,
            StockStatus.Low => 20,
            _ => 5
        };

        // How far below the threshold the item has fallen, worth up to 25 points.
        var deficit = Math.Clamp(1d - ((double)stock / Math.Max(1, reorderPoint)), 0d, 1d);
        var deficitScore = (int)Math.Round(deficit * 25d, MidpointRounding.AwayFromZero);

        // A fast moving item matters more than a slow one, worth up to 20 points.
        var demandScore = usesMeasuredDemand
            ? Math.Min(20, (int)Math.Round(unitsPerDay * 4d, MidpointRounding.AwayFromZero))
            : 0;

        return Math.Clamp(baseScore + deficitScore + demandScore, 0, 100);
    }

    /// <summary>
    /// Turns a health score into a letter grade.
    /// </summary>
    private static string GradeFor(int score) => score switch
    {
        >= 90 => "A",
        >= 75 => "B",
        >= 60 => "C",
        >= 40 => "D",
        _ => "E"
    };

    /// <summary>
    /// Words the health score: one finding for every problem found, plus a summary of what is held.
    /// </summary>
    /// <remarks>
    /// A catalogue with nothing wrong with it gets a positive finding rather than an empty list, so no
    /// page has to decide what "no findings" is supposed to look like.
    /// </remarks>
    private static List<HealthFinding> BuildFindings(StockHealth health, ValuationSummary valuation)
    {
        static HealthFinding Finding(FindingSeverity severity, string title, string detail)
            => new() { Severity = severity, Title = title, Detail = detail };

        var findings = new List<HealthFinding>();

        if (health.OutOfStockCount > 0)
        {
            findings.Add(Finding(
                FindingSeverity.Warning,
                FormattableString.Invariant($"{health.OutOfStockCount} item{Plural(health.OutOfStockCount)} out of stock"),
                "These cannot be sold at all. The restock worksheet shows what to order and what it costs."));
        }

        if (health.CriticalCount > 0)
        {
            findings.Add(Finding(
                FindingSeverity.Caution,
                FormattableString.Invariant($"{health.CriticalCount} item{Plural(health.CriticalCount)} nearly out"),
                "Under a quarter of the threshold that covers their demand is left, so the next order needs placing now."));
        }

        if (health.LowCount > 0)
        {
            findings.Add(Finding(
                FindingSeverity.Information,
                FormattableString.Invariant($"{health.LowCount} item{Plural(health.LowCount)} at or below the reorder point"),
                "There is still cover, but these belong on the next order."));
        }

        if (health.OverstockCount > 0)
        {
            findings.Add(Finding(
                FindingSeverity.Caution,
                FormattableString.Invariant($"{health.OverstockCount} item{Plural(health.OverstockCount)} overstocked"),
                FormattableString.Invariant(
                    $"More than {OverstockMultiple} times their reorder point, which is cash spent on shelf space.")));
        }

        if (health.DeadStockCount > 0)
        {
            findings.Add(Finding(
                FindingSeverity.Caution,
                FormattableString.Invariant($"{health.DeadStockCount} item{Plural(health.DeadStockCount)} did not move at all"),
                "Stocked well above the threshold with no recorded movement inside the measurement window."));
        }

        if (valuation.LargestItemShareOfValue >= ConcentrationWarningThreshold)
        {
            findings.Add(Finding(
                FindingSeverity.Caution,
                "The value is concentrated in one item",
                FormattableString.Invariant(
                    $"A single item holds {valuation.LargestItemShareOfValue:0.#}% of the retail value held, so one price change or one supply problem moves the whole picture.")));
        }

        if (valuation.BelowTargetMarginCount > 0)
        {
            findings.Add(Finding(
                FindingSeverity.Information,
                FormattableString.Invariant($"{valuation.BelowTargetMarginCount} item{Plural(valuation.BelowTargetMarginCount)} under the margin target"),
                "Each item page shows the price that would reach the target set for it."));
        }

        if (findings.Count == 0)
        {
            findings.Add(Finding(
                FindingSeverity.Positive,
                "Every item is within its thresholds",
                "Nothing is out of stock, nothing is overstocked and nothing is sitting still."));
        }

        findings.Add(Finding(
            FindingSeverity.Positive,
            FormattableString.Invariant($"{valuation.TotalUnits:N0} units held across {valuation.ItemCount} lines"),
            FormattableString.Invariant(
                $"Average margin {valuation.AverageMarginPercent:0.#}% and average unit price {valuation.AverageUnitPrice:0.00}.")));

        return findings;
    }

    /// <summary>
    /// Picks the verb endings for a counted finding: "1 item is", "3 items are".
    /// </summary>
    private static string Plural(int count) => count == 1 ? " is" : "s are";
}
