namespace GroceryApp.Models.DTOs;

/// <summary>
/// Where an item sits in the ABC (Pareto) classification of the catalogue's value.
/// </summary>
/// <remarks>
/// ABC analysis splits a catalogue by how much of its value each item carries, so attention goes to
/// the items that actually move the numbers rather than to the longest list.
/// </remarks>
public enum AbcClass
{
    /// <summary>The items making up the first 80% of the catalogue's retail value.</summary>
    A,

    /// <summary>The next 15% of the value.</summary>
    B,

    /// <summary>The tail: many items, little value.</summary>
    C
}

/// <summary>How urgent an item's stock position is.</summary>
public enum StockStatus
{
    /// <summary>Nothing on hand.</summary>
    OutOfStock,

    /// <summary>On hand, but at or below a quarter of the item's reorder point.</summary>
    Critical,

    /// <summary>At or below the item's reorder point.</summary>
    Low,

    /// <summary>Between the reorder point and four times it.</summary>
    Healthy,

    /// <summary>More than four times the reorder point: cash tied up in shelf space.</summary>
    Overstock
}

/// <summary>How a finding on the inventory health report should read.</summary>
public enum FindingSeverity
{
    Positive,
    Information,
    Caution,
    Warning
}

/// <summary>
/// What the whole catalogue is worth, and how much profit is sitting on the shelf.
/// </summary>
public class ValuationSummary
{
    public int ItemCount { get; set; }

    public int TotalUnits { get; set; }

    /// <summary>What the units on hand cost to produce.</summary>
    public decimal CostValue { get; set; }

    /// <summary>What the units on hand would sell for.</summary>
    public decimal RetailValue { get; set; }

    /// <summary>Retail value minus cost value: the profit locked up in stock.</summary>
    public decimal PotentialProfit { get; set; }

    /// <summary>Mean margin across the catalogue, as a percentage of price.</summary>
    public decimal AverageMarginPercent { get; set; }

    public decimal AverageUnitPrice { get; set; }

    public int OutOfStockCount { get; set; }

    public int BelowTargetMarginCount { get; set; }

    /// <summary>
    /// Share of the retail value held by the single largest item, as a percentage. A high figure
    /// means the catalogue's worth depends on very few lines.
    /// </summary>
    public decimal LargestItemShareOfValue { get; set; }
}

/// <summary>How one category is performing.</summary>
public class CategoryPerformance
{
    public Category Category { get; set; }

    public int ItemCount { get; set; }

    public int TotalStock { get; set; }

    public decimal StockCostValue { get; set; }

    public decimal StockRetailValue { get; set; }

    public decimal PotentialProfit { get; set; }

    /// <summary>Mean profit per unit across the category.</summary>
    public decimal AverageMargin { get; set; }

    /// <summary>Mean margin as a percentage of price.</summary>
    public decimal AverageMarginPercent { get; set; }

    /// <summary>Share of the catalogue's total retail value, as a percentage.</summary>
    public decimal ShareOfRetailValue { get; set; }
}

/// <summary>One band of the ABC classification, with the items that fall into it.</summary>
public class AbcClassSummary
{
    public AbcClass Class { get; set; }

    public int ItemCount { get; set; }

    public int TotalStock { get; set; }

    public decimal StockCostValue { get; set; }

    public decimal StockRetailValue { get; set; }

    public decimal PotentialProfit { get; set; }

    /// <summary>Share of the catalogue's retail value held by this band, as a percentage.</summary>
    public decimal ShareOfValue { get; set; }

    /// <summary>Running share of value up to and including this band, as a percentage.</summary>
    public decimal CumulativeShare { get; set; }

    /// <summary>The items in this band, most valuable first.</summary>
    public List<GroceryItemDto> Items { get; set; } = new();
}

/// <summary>
/// The catalogue split into ABC bands by the value of the stock on hand.
/// </summary>
public class AbcAnalysis
{
    public List<AbcClassSummary> Classes { get; set; } = new();

    /// <summary>Share of the catalogue's retail value held by the A items, as a percentage.</summary>
    public decimal Concentration { get; set; }

    /// <summary>
    /// How many catalogue lines the A items account for. A small number carrying most of the value is
    /// the pattern ABC analysis exists to expose.
    /// </summary>
    public int ItemCountInClassA { get; set; }
}

/// <summary>One line of commentary on the state of the catalogue.</summary>
public class HealthFinding
{
    public FindingSeverity Severity { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;
}

/// <summary>
/// A single score for how well the catalogue is being run, with the reasoning behind it.
/// </summary>
public class StockHealth
{
    /// <summary>Score from 0 (needs attention) to 100 (healthy), before any grade banding.</summary>
    public int Score { get; set; }

    /// <summary>Letter grade derived from <see cref="Score"/>.</summary>
    public string Grade { get; set; } = string.Empty;

    public int OutOfStockCount { get; set; }

    public int CriticalCount { get; set; }

    public int LowCount { get; set; }

    public int HealthyCount { get; set; }

    public int OverstockCount { get; set; }

    /// <summary>Items with plenty of stock and no recorded movement: cash sitting on a shelf.</summary>
    public int DeadStockCount { get; set; }

    public List<HealthFinding> Findings { get; set; } = new();
}

/// <summary>
/// A suggested top-up for one item, and the reasoning that produced it.
/// </summary>
public class ReorderSuggestion
{
    public GroceryItemDto Item { get; set; } = new();

    public StockStatus Status { get; set; }

    /// <summary>
    /// Stock level at or below which a reorder should be placed. Derived from the item's measured
    /// demand and the supplier's lead time when there is enough history, and from the item's own
    /// reorder point when there is not.
    /// </summary>
    public int ReorderPoint { get; set; }

    /// <summary>Level to top up to, so one order covers more than a single lead time.</summary>
    public int TargetStock { get; set; }

    public int SuggestedQuantity { get; set; }

    public decimal EstimatedUnitCost { get; set; }

    /// <summary>Suggested quantity at cost.</summary>
    public decimal EstimatedCost { get; set; }

    public string Supplier { get; set; } = string.Empty;

    public int LeadTimeDays { get; set; }

    /// <summary>Units sold per day, measured from the activity log. Zero when there is no history.</summary>
    public double UnitsPerDay { get; set; }

    /// <summary>
    /// True when the suggestion is based on measured demand from the activity log. False when it is
    /// based only on the item's own reorder point, which the interface says so the visitor is never
    /// shown a forecast that is really a guess.
    /// </summary>
    public bool UsesMeasuredDemand { get; set; }

    /// <summary>Why the item is on the list, in one sentence.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>0-100, higher first. Orders the worksheet.</summary>
    public int Urgency { get; set; }
}

/// <summary>
/// How quickly one item moved over the measurement window.
/// </summary>
public class VelocityStat
{
    public int ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Units taken out of stock inside the window.</summary>
    public int UnitsMoved { get; set; }

    public int WindowDays { get; set; }

    public double UnitsPerDay { get; set; }

    /// <summary>
    /// How long the stock on hand lasts at the measured rate, or null when nothing moved inside the
    /// window, in which case any figure would be invented.
    /// </summary>
    public double? DaysOfCover { get; set; }
}
