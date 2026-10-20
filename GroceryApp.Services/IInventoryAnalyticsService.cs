using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Works out what the catalogue is worth, what needs reordering, how fast things move and how
/// healthy the whole thing is.
/// </summary>
/// <remarks>
/// Every method is a pure calculation over the data it is handed: nothing here reads storage, and
/// nothing here depends on the order the items arrive in. That keeps the analysis testable without a
/// browser, and means the same figures can be produced for the dashboard, the restock worksheet and
/// the exported workbook from one implementation.
/// </remarks>
public interface IInventoryAnalyticsService
{
    /// <summary>
    /// Classifies an item's stock position against its own reorder point.
    /// </summary>
    /// <param name="item">The item to classify.</param>
    /// <returns>The item's stock status.</returns>
    StockStatus Classify(GroceryItemDto item);

    /// <summary>
    /// Summarises what the catalogue is worth and how much profit is tied up in it.
    /// </summary>
    /// <param name="items">The catalogue to summarise.</param>
    /// <returns>The valuation summary. Monetary figures are returned at full precision and rounded only for display.</returns>
    ValuationSummary Summarise(IEnumerable<GroceryItemDto> items);

    /// <summary>
    /// Breaks the catalogue down by category, most valuable first.
    /// </summary>
    /// <param name="items">The catalogue to break down.</param>
    /// <returns>One entry per category present in the catalogue.</returns>
    IReadOnlyList<CategoryPerformance> ByCategory(IEnumerable<GroceryItemDto> items);

    /// <summary>
    /// Splits the catalogue into ABC bands by the value of the stock on hand.
    /// </summary>
    /// <param name="items">The catalogue to analyse.</param>
    /// <returns>The three bands with the items in each, or three empty bands when there is nothing to analyse.</returns>
    AbcAnalysis AnalyseAbc(IEnumerable<GroceryItemDto> items);

    /// <summary>
    /// Measures how fast each item moved, from the stock movements recorded in the activity log.
    /// </summary>
    /// <param name="items">The catalogue to measure.</param>
    /// <param name="activity">The activity log.</param>
    /// <param name="nowUtc">The moment the window ends, supplied rather than read from the clock so the result is reproducible.</param>
    /// <param name="windowDays">Length of the measurement window in days.</param>
    /// <returns>One entry per item, fastest moving first.</returns>
    IReadOnlyList<VelocityStat> CalculateVelocity(
        IEnumerable<GroceryItemDto> items,
        IEnumerable<ActivityEntry> activity,
        DateTime nowUtc,
        int windowDays = 30);

    /// <summary>
    /// Builds the restock worksheet: which items to top up, by how much, and why.
    /// </summary>
    /// <param name="items">The catalogue to work from.</param>
    /// <param name="activity">The activity log, used to measure demand.</param>
    /// <param name="nowUtc">The moment the measurement window ends.</param>
    /// <param name="leadTimeDays">Supplies the lead time for a supplier name. Null uses <see cref="InventoryAnalyticsService.DefaultLeadTimeDays"/> for everything.</param>
    /// <param name="windowDays">Length of the demand measurement window in days.</param>
    /// <returns>The items that need topping up, most urgent first.</returns>
    IReadOnlyList<ReorderSuggestion> SuggestReorders(
        IEnumerable<GroceryItemDto> items,
        IEnumerable<ActivityEntry> activity,
        DateTime nowUtc,
        Func<string, int>? leadTimeDays = null,
        int windowDays = 30);

    /// <summary>
    /// Scores how well the catalogue is being run, and explains the score.
    /// </summary>
    /// <param name="items">The catalogue to assess.</param>
    /// <param name="activity">The activity log, used to spot stock that never moves.</param>
    /// <param name="nowUtc">The moment the measurement window ends.</param>
    /// <param name="windowDays">Length of the movement window in days.</param>
    /// <returns>The score, its grade and the findings behind it.</returns>
    StockHealth AssessHealth(
        IEnumerable<GroceryItemDto> items,
        IEnumerable<ActivityEntry> activity,
        DateTime nowUtc,
        int windowDays = 30);

    /// <summary>
    /// Classic economic order quantity: the order size that balances the cost of ordering against the
    /// cost of holding stock.
    /// </summary>
    /// <remarks>
    /// The catalogue holds no data about what ordering costs, so the caller has to supply both costs.
    /// Nothing in the application shows a figure from this method without labelling the costs it
    /// assumed, rather than inventing them silently.
    /// </remarks>
    /// <param name="annualDemandUnits">Units expected to be sold in a year.</param>
    /// <param name="costPerOrder">Cost of placing and receiving one order.</param>
    /// <param name="annualHoldingCostPerUnit">Cost of keeping one unit for a year.</param>
    /// <returns>The order quantity that minimises total cost, or 0 when any input is not positive.</returns>
    decimal CalculateEconomicOrderQuantity(
        decimal annualDemandUnits,
        decimal costPerOrder,
        decimal annualHoldingCostPerUnit);
}
