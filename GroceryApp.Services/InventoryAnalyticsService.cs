using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// The inventory analytics used by the dashboard, the restock worksheet and the exported workbook.
/// </summary>
/// <remarks>
/// <para>
/// The rules the analysis applies are stated here rather than buried in the pages, and every constant
/// that has to be assumed - the buffer held against demand, how many lead times one order covers, the
/// point at which stock counts as overstocked - is a named constant with the reasoning next to it, so
/// a reader can disagree with a number and change it in one place.
/// </para>
/// <para>
/// Nothing in this class touches storage or the clock. Dates are passed in, which is what makes the
/// results reproducible in tests instead of drifting with the current time.
/// </para>
/// </remarks>
public partial class InventoryAnalyticsService : IInventoryAnalyticsService
{
    /// <summary>
    /// Days of measured demand held back as a buffer on top of the supplier's lead time.
    /// </summary>
    /// <remarks>
    /// Three days covers a normal weekend gap in deliveries without inflating every order. A real
    /// operation would set this per supplier from the spread of its actual delivery times, which the
    /// catalogue has no data for.
    /// </remarks>
    public const int SafetyStockDays = 3;

    /// <summary>
    /// How many lead times of stock one order should aim to cover.
    /// </summary>
    /// <remarks>
    /// Ordering to two cover periods rather than one is what stops an item that is reordered the day
    /// after it is restocked from sitting just above its threshold forever.
    /// </remarks>
    public const int CoverPeriodsPerOrder = 2;

    /// <summary>
    /// Stock above this multiple of an item's reorder point is treated as overstocked.
    /// </summary>
    public const int OverstockMultiple = 4;

    /// <summary>Lead time assumed for an item whose supplier is unknown.</summary>
    public const int DefaultLeadTimeDays = 3;

    /// <summary>Share of the catalogue's retail value the A band accounts for.</summary>
    public const decimal ClassAValueBoundary = 80m;

    /// <summary>Share of the catalogue's retail value the B band adds on top of the A band.</summary>
    public const decimal ClassBValueBoundary = 15m;

    /// <summary>Concentration above this share counts against the health score.</summary>
    public const decimal ConcentrationWarningThreshold = 40m;

    /// <inheritdoc />
    public StockStatus Classify(GroceryItemDto item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ClassifyAgainst(item.Stock, item.ReorderPoint);
    }

    /// <summary>
    /// Classifies a stock level against a threshold.
    /// </summary>
    /// <remarks>
    /// Kept separate from <see cref="Classify(GroceryItemDto)"/> because the restock worksheet judges
    /// items against the threshold their measured demand implies rather than the one they were
    /// configured with, and both paths have to band stock the same way.
    /// </remarks>
    /// <param name="stock">Units on hand.</param>
    /// <param name="reorderPoint">Threshold to judge them against.</param>
    /// <returns>The stock status.</returns>
    private static StockStatus ClassifyAgainst(int stock, int reorderPoint)
    {
        if (stock <= 0)
        {
            return StockStatus.OutOfStock;
        }

        // A threshold of zero would make every stocked item look healthy, which would hide the fact
        // that nothing has been configured for the item.
        var threshold = Math.Max(1, reorderPoint);

        if (stock <= threshold / 4)
        {
            return StockStatus.Critical;
        }

        if (stock <= threshold)
        {
            return StockStatus.Low;
        }

        return stock > threshold * OverstockMultiple
            ? StockStatus.Overstock
            : StockStatus.Healthy;
    }

    /// <inheritdoc />
    public ValuationSummary Summarise(IEnumerable<GroceryItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var catalogue = items.ToList();
        var summary = new ValuationSummary { ItemCount = catalogue.Count };

        if (catalogue.Count == 0)
        {
            return summary;
        }

        summary.TotalUnits = catalogue.Sum(item => item.Stock);
        summary.CostValue = catalogue.Sum(item => item.StockCostValue);
        summary.RetailValue = catalogue.Sum(item => item.StockRetailValue);
        summary.PotentialProfit = catalogue.Sum(item => item.StockPotentialProfit);
        summary.AverageMarginPercent = catalogue.Average(item => item.MarginPercent);
        summary.AverageUnitPrice = catalogue.Average(item => item.Price);
        summary.OutOfStockCount = catalogue.Count(item => item.Stock <= 0);
        summary.BelowTargetMarginCount = catalogue.Count(item => item.IsBelowTargetMargin);

        summary.LargestItemShareOfValue = summary.RetailValue == 0m
            ? 0m
            : catalogue.Max(item => item.StockRetailValue) / summary.RetailValue * 100m;

        return summary;
    }

    /// <inheritdoc />
    public IReadOnlyList<CategoryPerformance> ByCategory(IEnumerable<GroceryItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var catalogue = items.ToList();
        var totalRetailValue = catalogue.Sum(item => item.StockRetailValue);

        return catalogue
            .GroupBy(item => item.Category)
            .Select(group => new CategoryPerformance
            {
                Category = group.Key,
                ItemCount = group.Count(),
                TotalStock = group.Sum(item => item.Stock),
                StockCostValue = group.Sum(item => item.StockCostValue),
                StockRetailValue = group.Sum(item => item.StockRetailValue),
                PotentialProfit = group.Sum(item => item.StockPotentialProfit),
                AverageMargin = group.Average(item => item.Margin),
                AverageMarginPercent = group.Average(item => item.MarginPercent),
                ShareOfRetailValue = totalRetailValue == 0m
                    ? 0m
                    : group.Sum(item => item.StockRetailValue) / totalRetailValue * 100m
            })
            .OrderByDescending(category => category.StockRetailValue)
            .ThenBy(category => category.Category)
            .ToList();
    }

    /// <inheritdoc />
    public AbcAnalysis AnalyseAbc(IEnumerable<GroceryItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var catalogue = items.ToList();
        var analysis = new AbcAnalysis();

        if (catalogue.Count == 0)
        {
            return analysis;
        }

        var totalRetailValue = catalogue.Sum(item => item.StockRetailValue);

        var bands = new Dictionary<AbcClass, AbcClassSummary>
        {
            [AbcClass.A] = new() { Class = AbcClass.A },
            [AbcClass.B] = new() { Class = AbcClass.B },
            [AbcClass.C] = new() { Class = AbcClass.C }
        };

        // Most valuable first, so the running total crosses the boundaries in the intended order.
        // Items of equal value are ordered by name to keep the bands stable between loads.
        var ordered = catalogue
            .OrderByDescending(item => item.StockRetailValue)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var cumulative = 0m;

        foreach (var item in ordered)
        {
            var share = totalRetailValue == 0m ? 0m : item.StockRetailValue / totalRetailValue * 100m;

            // The band is decided by the running total *before* this item, so the item that carries
            // the total across a boundary stays in the more valuable band rather than being demoted
            // for having crossed it.
            var band = totalRetailValue == 0m
                ? AbcClass.C
                : cumulative < ClassAValueBoundary
                    ? AbcClass.A
                    : cumulative < ClassAValueBoundary + ClassBValueBoundary
                        ? AbcClass.B
                        : AbcClass.C;

            bands[band].Items.Add(item);
            cumulative += share;
        }

        var running = 0m;

        foreach (var band in new[] { AbcClass.A, AbcClass.B, AbcClass.C })
        {
            var summary = bands[band];

            summary.ItemCount = summary.Items.Count;
            summary.TotalStock = summary.Items.Sum(item => item.Stock);
            summary.StockCostValue = summary.Items.Sum(item => item.StockCostValue);
            summary.StockRetailValue = summary.Items.Sum(item => item.StockRetailValue);
            summary.PotentialProfit = summary.Items.Sum(item => item.StockPotentialProfit);
            summary.ShareOfValue = totalRetailValue == 0m
                ? 0m
                : summary.StockRetailValue / totalRetailValue * 100m;

            running += summary.ShareOfValue;
            summary.CumulativeShare = running;

            analysis.Classes.Add(summary);
        }

        analysis.Concentration = bands[AbcClass.A].ShareOfValue;
        analysis.ItemCountInClassA = bands[AbcClass.A].ItemCount;

        return analysis;
    }

    /// <inheritdoc />
    public IReadOnlyList<VelocityStat> CalculateVelocity(
        IEnumerable<GroceryItemDto> items,
        IEnumerable<ActivityEntry> activity,
        DateTime nowUtc,
        int windowDays = 30)
    {
        ArgumentNullException.ThrowIfNull(items);

        var catalogue = items.ToList();
        var window = Math.Max(1, windowDays);
        var unitsMoved = UnitsMovedByItem(activity, nowUtc.AddDays(-window), nowUtc);

        return catalogue
            .Select(item =>
            {
                unitsMoved.TryGetValue(item.Id, out var moved);
                var perDay = (double)moved / window;

                return new VelocityStat
                {
                    ItemId = item.Id,
                    Name = item.Name,
                    UnitsMoved = moved,
                    WindowDays = window,
                    UnitsPerDay = perDay,

                    // Days of cover is only reported when something actually moved: dividing by a
                    // rate of zero would produce an infinite figure dressed up as a measurement.
                    DaysOfCover = moved <= 0 ? null : item.Stock / perDay
                };
            })
            .OrderByDescending(stat => stat.UnitsPerDay)
            .ThenBy(stat => stat.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ReorderSuggestion> SuggestReorders(
        IEnumerable<GroceryItemDto> items,
        IEnumerable<ActivityEntry> activity,
        DateTime nowUtc,
        Func<string, int>? leadTimeDays = null,
        int windowDays = 30)
    {
        ArgumentNullException.ThrowIfNull(items);

        var catalogue = items.ToList();
        var window = Math.Max(1, windowDays);
        var unitsMoved = UnitsMovedByItem(activity, nowUtc.AddDays(-window), nowUtc);
        var suggestions = new List<ReorderSuggestion>();

        foreach (var item in catalogue)
        {
            unitsMoved.TryGetValue(item.Id, out var moved);
            var perDay = (double)moved / window;
            var usesMeasuredDemand = moved > 0;
            var configuredStatus = Classify(item);

            // With no demand history the item's own settings are the only signal available, so an item
            // that is comfortable against them needs nothing bought for it.
            if (!usesMeasuredDemand && configuredStatus is StockStatus.Healthy or StockStatus.Overstock)
            {
                continue;
            }

            var lead = Math.Max(0, leadTimeDays?.Invoke(item.Supplier) ?? DefaultLeadTimeDays);

            int reorderPoint;
            int targetStock;

            if (usesMeasuredDemand)
            {
                var safetyStock = (int)Math.Ceiling(perDay * SafetyStockDays);
                reorderPoint = (int)Math.Ceiling(perDay * lead) + safetyStock;
                targetStock = Math.Max(
                    reorderPoint,
                    (int)Math.Ceiling(perDay * lead * CoverPeriodsPerOrder) + safetyStock);
            }
            else
            {
                // With no demand history the only defensible threshold is the one the visitor set.
                reorderPoint = Math.Max(1, item.ReorderPoint);
                targetStock = reorderPoint * CoverPeriodsPerOrder;
            }

            // An item belongs on the worksheet when it is at or below either threshold: the one its
            // own settings define, or the one its measured demand implies.
            if (item.Stock > reorderPoint && item.Stock > item.ReorderPoint)
            {
                continue;
            }

            var quantity = targetStock - item.Stock;

            if (quantity <= 0)
            {
                continue;
            }

            // The status reported alongside the suggestion is measured against the threshold that
            // suggestion actually uses. An item whose configured threshold calls it overstocked while
            // its measured demand calls it nearly out is nearly out, and the worksheet says so rather
            // than presenting two contradictory readings of the same item.
            var status = usesMeasuredDemand
                ? ClassifyAgainst(item.Stock, reorderPoint)
                : configuredStatus;

            suggestions.Add(new ReorderSuggestion
            {
                Item = item,
                Status = status,
                ReorderPoint = reorderPoint,
                TargetStock = targetStock,
                SuggestedQuantity = quantity,
                EstimatedUnitCost = item.CostToProduce,
                EstimatedCost = quantity * item.CostToProduce,
                Supplier = item.Supplier,
                LeadTimeDays = lead,
                UnitsPerDay = perDay,
                UsesMeasuredDemand = usesMeasuredDemand,
                Reason = DescribeReorder(status, item, reorderPoint, lead, perDay, usesMeasuredDemand),
                Urgency = ScoreUrgency(status, item.Stock, reorderPoint, perDay, usesMeasuredDemand)
            });
        }

        return suggestions
            .OrderByDescending(suggestion => suggestion.Urgency)
            .ThenBy(suggestion => suggestion.Item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public StockHealth AssessHealth(
        IEnumerable<GroceryItemDto> items,
        IEnumerable<ActivityEntry> activity,
        DateTime nowUtc,
        int windowDays = 30)
    {
        ArgumentNullException.ThrowIfNull(items);

        var catalogue = items.ToList();
        var health = new StockHealth();

        if (catalogue.Count == 0)
        {
            health.Grade = GradeFor(0);
            health.Findings.Add(new HealthFinding
            {
                Severity = FindingSeverity.Information,
                Title = "Nothing to score yet",
                Detail = "The catalogue is empty. Add an item, or restore the demo data, and there will be something to assess."
            });

            return health;
        }

        var moved = UnitsMovedByItem(activity, nowUtc.AddDays(-Math.Max(1, windowDays)), nowUtc);

        foreach (var item in catalogue)
        {
            var status = Classify(item);

            switch (status)
            {
                case StockStatus.OutOfStock:
                    health.OutOfStockCount++;
                    break;
                case StockStatus.Critical:
                    health.CriticalCount++;
                    break;
                case StockStatus.Low:
                    health.LowCount++;
                    break;
                case StockStatus.Overstock:
                    health.OverstockCount++;
                    break;
                default:
                    health.HealthyCount++;
                    break;
            }

            // Stock sitting well above its threshold that has not moved at all inside the window is
            // cash on a shelf, and counts against the score in its own right rather than passing as
            // healthy stock simply because there is plenty of it.
            if (status != StockStatus.OutOfStock &&
                item.Stock > item.ReorderPoint * 2 &&
                !moved.ContainsKey(item.Id))
            {
                health.DeadStockCount++;
            }
        }

        var itemCount = Math.Max(1, catalogue.Count);

        // Penalties are shares of the catalogue rather than raw counts. Seven items out of stock means
        // something very different in a catalogue of ten than in one of five hundred, and a score that
        // counted raw items would declare any large catalogue unhealthy no matter how well it was run.
        static int ShareOfCatalogue(int count, int total, int weight) =>
            (int)Math.Round((double)count / total * weight, MidpointRounding.AwayFromZero);

        var deduction =
            ShareOfCatalogue(health.OutOfStockCount, itemCount, 50) +
            ShareOfCatalogue(health.CriticalCount, itemCount, 20) +
            ShareOfCatalogue(health.LowCount, itemCount, 12) +
            ShareOfCatalogue(health.OverstockCount, itemCount, 18) +
            ShareOfCatalogue(health.DeadStockCount, itemCount, 15);

        var valuation = Summarise(catalogue);

        if (valuation.LargestItemShareOfValue >= ConcentrationWarningThreshold)
        {
            deduction += 5;
        }

        health.Score = Math.Clamp(100 - deduction, 0, 100);
        health.Grade = GradeFor(health.Score);
        health.Findings.AddRange(BuildFindings(health, valuation));

        return health;
    }

    /// <inheritdoc />
    public decimal CalculateEconomicOrderQuantity(
        decimal annualDemandUnits,
        decimal costPerOrder,
        decimal annualHoldingCostPerUnit)
    {
        if (annualDemandUnits <= 0m || costPerOrder <= 0m || annualHoldingCostPerUnit <= 0m)
        {
            return 0m;
        }

        // EOQ = sqrt(2 * annual demand * cost per order / annual holding cost per unit). Decimal has no
        // square root, so the calculation goes through double and comes back out as a quantity.
        return (decimal)Math.Sqrt(
            (double)(2m * annualDemandUnits * costPerOrder / annualHoldingCostPerUnit));
    }
}
