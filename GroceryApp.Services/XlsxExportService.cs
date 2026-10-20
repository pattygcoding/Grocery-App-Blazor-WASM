using GroceryApp.Models.DTOs;
using GroceryApp.Services.Xlsx;

namespace GroceryApp.Services;

/// <summary>
/// Builds the grocery analytics workbook in the browser.
/// </summary>
/// <remarks>
/// The worksheets, metrics and formatting match the original ClosedXML based implementation.
/// The file is written with <see cref="SimpleXlsxWriter"/>, a small Open XML writer without any
/// third party dependencies, so it can run inside a Blazor WebAssembly application.
/// </remarks>
public class XlsxExportService : IExcelExportService
{
    private readonly IInventoryAnalyticsService _analytics;

    /// <summary>
    /// Initializes a new instance of the XlsxExportService class.
    /// </summary>
    /// <param name="analytics">
    /// The analysis the workbook's later sheets are built from. Shared with the pages rather than
    /// reimplemented here, so a band or a stock position in the file matches the one on screen.
    /// </param>
    public XlsxExportService(IInventoryAnalyticsService analytics)
    {
        _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
    }

    /// <inheritdoc />
    public Task<byte[]> GenerateReportExcelAsync(IEnumerable<GroceryItemDto> groceries)
    {
        ArgumentNullException.ThrowIfNull(groceries);

        var groceryList = groceries.ToList();
        var workbook = new SimpleXlsxWriter();

        // Sheet 1: Key Metrics Summary
        CreateKeyMetricsSheet(workbook, groceryList);

        // Sheet 2: Profit by Category
        CreateProfitByCategorySheet(workbook, groceryList);

        // Sheet 3: Top Profitable Items
        CreateTopProfitableItemsSheet(workbook, groceryList);

        // Sheet 4: Stock Analysis
        CreateStockAnalysisSheet(workbook, groceryList);

        // Sheet 5: Price Range Analysis
        CreatePriceRangeAnalysisSheet(workbook, groceryList);

        // Sheet 6: ABC Analysis
        CreateAbcAnalysisSheet(workbook, groceryList);

        // Sheet 7: Supplier Summary
        CreateSupplierSummarySheet(workbook, groceryList);

        // Sheet 8: Items to Deal With
        CreateItemsToDealWithSheet(workbook, groceryList);

        return Task.FromResult(workbook.Build());
    }

    /// <summary>
    /// Creates the Key Metrics worksheet containing summary statistics and key performance indicators.
    /// </summary>
    /// <param name="workbook">The workbook to add the worksheet to.</param>
    /// <param name="groceries">The collection of grocery items to analyze.</param>
    private static void CreateKeyMetricsSheet(SimpleXlsxWriter workbook, List<GroceryItemDto> groceries)
    {
        var worksheet = workbook.AddSheet("Key Metrics");

        // Title
        worksheet.SetText(1, 1, "Grocery Reports - Key Metrics Summary", XlsxStyle.Title);
        worksheet.Merge("A1", "C1");

        // Key metrics (same calculations as the Reports page)
        var totalProfitPotential = groceries.Sum(g => (g.Price - g.CostToProduce) * g.Stock);
        var averageProfitPerItem = groceries.Count == 0 ? 0m : groceries.Average(g => g.Price - g.CostToProduce);
        var totalItemsInStock = groceries.Sum(g => g.Stock);
        var totalCategories = groceries.Select(g => g.Category).Distinct().Count();

        worksheet.SetText(3, 1, "Total Profit Potential:", XlsxStyle.BoldLabel);
        worksheet.SetNumber(3, 2, totalProfitPotential, XlsxStyle.GreenFillCurrency);

        worksheet.SetText(4, 1, "Average Profit per Item:", XlsxStyle.BoldLabel);
        worksheet.SetNumber(4, 2, averageProfitPerItem, XlsxStyle.BlueFillCurrency);

        worksheet.SetText(5, 1, "Total Items in Stock:", XlsxStyle.BoldLabel);
        worksheet.SetNumber(5, 2, totalItemsInStock, XlsxStyle.YellowFill);

        worksheet.SetText(6, 1, "Product Categories:", XlsxStyle.BoldLabel);
        worksheet.SetNumber(6, 2, totalCategories, XlsxStyle.CyanFill);

        // Note about the data source
        worksheet.SetText(8, 1, "Note: These values are calculated from the detailed data in the other sheets.", XlsxStyle.Italic);
        worksheet.Merge("A8", "C8");
    }

    /// <summary>
    /// Creates the Profit by Category worksheet showing profit analysis grouped by product categories.
    /// </summary>
    /// <param name="workbook">The workbook to add the worksheet to.</param>
    /// <param name="groceries">The collection of grocery items to analyze.</param>
    private static void CreateProfitByCategorySheet(SimpleXlsxWriter workbook, List<GroceryItemDto> groceries)
    {
        var worksheet = workbook.AddSheet("Profit by Category");

        worksheet.SetText(1, 1, "Category", XlsxStyle.HeaderBlue);
        worksheet.SetText(1, 2, "Items", XlsxStyle.HeaderBlue);
        worksheet.SetText(1, 3, "Total Stock", XlsxStyle.HeaderBlue);
        worksheet.SetText(1, 4, "Avg Profit", XlsxStyle.HeaderBlue);
        worksheet.SetText(1, 5, "Total Potential", XlsxStyle.HeaderBlue);

        var categoryStats = groceries
            .GroupBy(g => g.Category)
            .Select(g => new
            {
                Category = g.Key,
                ItemCount = g.Count(),
                TotalStock = g.Sum(i => i.Stock),
                AverageProfit = g.Average(i => i.Price - i.CostToProduce),
                TotalPotentialProfit = g.Sum(i => (i.Price - i.CostToProduce) * i.Stock)
            })
            .OrderByDescending(c => c.TotalPotentialProfit)
            .ToList();

        for (var index = 0; index < categoryStats.Count; index++)
        {
            var category = categoryStats[index];
            var row = index + 2;

            worksheet.SetText(row, 1, category.Category.ToString());
            worksheet.SetNumber(row, 2, category.ItemCount);
            worksheet.SetNumber(row, 3, category.TotalStock);
            worksheet.SetNumber(row, 4, category.AverageProfit, XlsxStyle.Currency);
            worksheet.SetNumber(row, 5, category.TotalPotentialProfit, XlsxStyle.Currency);
        }
    }

    /// <summary>
    /// Creates the Top Profitable Items worksheet listing the ten most profitable items.
    /// </summary>
    /// <param name="workbook">The workbook to add the worksheet to.</param>
    /// <param name="groceries">The collection of grocery items to analyze.</param>
    private static void CreateTopProfitableItemsSheet(SimpleXlsxWriter workbook, List<GroceryItemDto> groceries)
    {
        var worksheet = workbook.AddSheet("Top Profitable Items");

        worksheet.SetText(1, 1, "Item", XlsxStyle.HeaderGreen);
        worksheet.SetText(1, 2, "Category", XlsxStyle.HeaderGreen);
        worksheet.SetText(1, 3, "Profit", XlsxStyle.HeaderGreen);
        worksheet.SetText(1, 4, "Margin %", XlsxStyle.HeaderGreen);

        var topProfitableItems = groceries
            .OrderByDescending(g => g.Price - g.CostToProduce)
            .Take(10)
            .ToList();

        for (var index = 0; index < topProfitableItems.Count; index++)
        {
            var item = topProfitableItems[index];
            var row = index + 2;
            var profit = item.Price - item.CostToProduce;
            var marginPercent = item.Price == 0m ? 0m : profit / item.Price;

            worksheet.SetText(row, 1, item.Name);
            worksheet.SetText(row, 2, item.Category.ToString());
            worksheet.SetNumber(row, 3, profit, XlsxStyle.Currency);
            worksheet.SetNumber(row, 4, marginPercent, XlsxStyle.Percent);
        }
    }

    /// <summary>
    /// Creates the Stock Analysis worksheet showing inventory levels categorized by stock availability.
    /// </summary>
    /// <param name="workbook">The workbook to add the worksheet to.</param>
    /// <param name="groceries">The collection of grocery items to analyze.</param>
    private static void CreateStockAnalysisSheet(SimpleXlsxWriter workbook, List<GroceryItemDto> groceries)
    {
        var worksheet = workbook.AddSheet("Stock Analysis");

        worksheet.SetText(1, 1, "Status", XlsxStyle.HeaderCoral);
        worksheet.SetText(1, 2, "Items", XlsxStyle.HeaderCoral);
        worksheet.SetText(1, 3, "Total Stock", XlsxStyle.HeaderCoral);
        worksheet.SetText(1, 4, "Value", XlsxStyle.HeaderCoral);

        var highStockItems = groceries.Where(g => g.Stock > 5).ToList();
        var lowStockItems = groceries.Where(g => g.Stock >= 1 && g.Stock <= 5).ToList();
        var outOfStockItems = groceries.Where(g => g.Stock == 0).ToList();

        var stockLevels = new[]
        {
            (Status: "High Stock (>5)", Items: highStockItems, FillStyle: XlsxStyle.GreenFill, ValueStyle: XlsxStyle.GreenFillCurrency),
            (Status: "Low Stock (1-5)", Items: lowStockItems, FillStyle: XlsxStyle.YellowFill, ValueStyle: XlsxStyle.YellowFillCurrency),
            (Status: "Out of Stock (0)", Items: outOfStockItems, FillStyle: XlsxStyle.PinkFill, ValueStyle: XlsxStyle.PinkFillCurrency)
        };

        for (var index = 0; index < stockLevels.Length; index++)
        {
            var level = stockLevels[index];
            var row = index + 2;

            worksheet.SetText(row, 1, level.Status, level.FillStyle);
            worksheet.SetNumber(row, 2, level.Items.Count, level.FillStyle);
            worksheet.SetNumber(row, 3, level.Items.Sum(g => g.Stock), level.FillStyle);
            worksheet.SetNumber(row, 4, level.Items.Sum(g => g.Stock * g.Price), level.ValueStyle);
        }
    }

    /// <summary>
    /// Creates the Price Range Analysis worksheet showing analytics grouped by price ranges.
    /// </summary>
    /// <param name="workbook">The workbook to add the worksheet to.</param>
    /// <param name="groceries">The collection of grocery items to analyze.</param>
    private static void CreatePriceRangeAnalysisSheet(SimpleXlsxWriter workbook, List<GroceryItemDto> groceries)
    {
        var worksheet = workbook.AddSheet("Price Range Analysis");

        worksheet.SetText(1, 1, "Price Range", XlsxStyle.HeaderYellow);
        worksheet.SetText(1, 2, "Items", XlsxStyle.HeaderYellow);
        worksheet.SetText(1, 3, "Avg Cost", XlsxStyle.HeaderYellow);
        worksheet.SetText(1, 4, "Avg Profit", XlsxStyle.HeaderYellow);
        worksheet.SetText(1, 5, "Profit Margin", XlsxStyle.HeaderYellow);

        var priceRanges = new[]
        {
            (Range: "$0-$1", Items: groceries.Where(g => g.Price < 1m).ToList()),
            (Range: "$1-$3", Items: groceries.Where(g => g.Price >= 1m && g.Price < 3m).ToList()),
            (Range: "$3-$5", Items: groceries.Where(g => g.Price >= 3m && g.Price < 5m).ToList()),
            (Range: "$5-$10", Items: groceries.Where(g => g.Price >= 5m && g.Price < 10m).ToList()),
            (Range: "$10+", Items: groceries.Where(g => g.Price >= 10m).ToList())
        }.Where(range => range.Items.Count > 0).ToList();

        for (var index = 0; index < priceRanges.Count; index++)
        {
            var range = priceRanges[index];
            var row = index + 2;

            worksheet.SetText(row, 1, range.Range);
            worksheet.SetNumber(row, 2, range.Items.Count);
            worksheet.SetNumber(row, 3, range.Items.Average(g => g.CostToProduce), XlsxStyle.Currency);
            worksheet.SetNumber(row, 4, range.Items.Average(g => g.Price - g.CostToProduce), XlsxStyle.Currency);
            worksheet.SetNumber(row, 5, range.Items.Average(g => (g.Price - g.CostToProduce) / g.Price), XlsxStyle.Percent);
        }
    }

    /// <summary>
    /// Creates the Supplier Summary worksheet: what is bought from whom, and what it is worth.
    /// </summary>
    /// <param name="workbook">The workbook to add the worksheet to.</param>
    /// <param name="groceries">The catalogue to summarise.</param>
    private static void CreateSupplierSummarySheet(SimpleXlsxWriter workbook, List<GroceryItemDto> groceries)
    {
        var worksheet = workbook.AddSheet("Supplier Summary");

        worksheet.SetText(1, 1, "Supplier", XlsxStyle.HeaderBlue);
        worksheet.SetText(1, 2, "Items", XlsxStyle.HeaderBlue);
        worksheet.SetText(1, 3, "Units Held", XlsxStyle.HeaderBlue);
        worksheet.SetText(1, 4, "Value at Retail", XlsxStyle.HeaderBlue);
        worksheet.SetText(1, 5, "Value at Cost", XlsxStyle.HeaderBlue);
        worksheet.SetText(1, 6, "Average Margin", XlsxStyle.HeaderBlue);
        worksheet.SetText(1, 7, "Needs Ordering", XlsxStyle.HeaderBlue);

        // Grouped by name with the most valuable supplier first, because that is the order a buyer would
        // work down: the lines worth most are the ones worth the phone call.
        var suppliers = groceries
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Supplier) ? "(no supplier)" : item.Supplier)
            .Select(group => new { Name = group.Key, Items = group.ToList() })
            .OrderByDescending(group => group.Items.Sum(item => item.StockRetailValue))
            .ToList();

        for (var index = 0; index < suppliers.Count; index++)
        {
            var supplier = suppliers[index];
            var row = index + 2;
            var retail = supplier.Items.Sum(item => item.StockRetailValue);
            var cost = supplier.Items.Sum(item => item.CostToProduce * item.Stock);

            worksheet.SetText(row, 1, supplier.Name);
            worksheet.SetNumber(row, 2, supplier.Items.Count);
            worksheet.SetNumber(row, 3, supplier.Items.Sum(item => item.Stock));
            worksheet.SetNumber(row, 4, retail, XlsxStyle.Currency);
            worksheet.SetNumber(row, 5, cost, XlsxStyle.Currency);
            worksheet.SetNumber(row, 6, retail == 0m ? 0m : (retail - cost) / retail, XlsxStyle.Percent);
            worksheet.SetNumber(
                row,
                7,
                supplier.Items.Count(item => item.Stock <= item.ReorderPoint),
                XlsxStyle.YellowFill);
        }
    }

    /// <summary>
    /// Creates the ABC Analysis worksheet: how the value held is concentrated across the catalogue.
    /// </summary>
    /// <param name="workbook">The workbook to add the worksheet to.</param>
    /// <param name="groceries">The catalogue to analyse.</param>
    private void CreateAbcAnalysisSheet(SimpleXlsxWriter workbook, List<GroceryItemDto> groceries)
    {
        var worksheet = workbook.AddSheet("ABC Analysis");
        var analysis = _analytics.AnalyseAbc(groceries);

        worksheet.SetText(1, 1, "Band", XlsxStyle.HeaderGreen);
        worksheet.SetText(1, 2, "Items", XlsxStyle.HeaderGreen);
        worksheet.SetText(1, 3, "Units Held", XlsxStyle.HeaderGreen);
        worksheet.SetText(1, 4, "Value at Retail", XlsxStyle.HeaderGreen);
        worksheet.SetText(1, 5, "Potential Profit", XlsxStyle.HeaderGreen);
        worksheet.SetText(1, 6, "Share of Value", XlsxStyle.HeaderGreen);
        worksheet.SetText(1, 7, "Running Share", XlsxStyle.HeaderGreen);

        for (var index = 0; index < analysis.Classes.Count; index++)
        {
            var band = analysis.Classes[index];
            var row = index + 2;

            worksheet.SetText(row, 1, $"Band {band.Class}");
            worksheet.SetNumber(row, 2, band.ItemCount);
            worksheet.SetNumber(row, 3, band.TotalStock);
            worksheet.SetNumber(row, 4, band.StockRetailValue, XlsxStyle.Currency);
            worksheet.SetNumber(row, 5, band.PotentialProfit, XlsxStyle.Currency);
            worksheet.SetNumber(row, 6, band.ShareOfValue / 100m, XlsxStyle.Percent);
            worksheet.SetNumber(row, 7, band.CumulativeShare / 100m, XlsxStyle.Percent);
        }

        var noteRow = analysis.Classes.Count + 3;

        worksheet.SetText(noteRow, 1, $"{analysis.ItemCountInClassA} of {groceries.Count} items carry {analysis.Concentration:0.#}% of the value held.", XlsxStyle.BoldLabel);
        worksheet.SetText(noteRow + 1, 1, "Bands are worked out from the value of the stock on hand, not from what has sold.", XlsxStyle.Italic);
    }

    /// <summary>
    /// Creates the Items to Deal With worksheet: the lines that need a decision rather than a summary.
    /// </summary>
    /// <param name="workbook">The workbook to add the worksheet to.</param>
    /// <param name="groceries">The catalogue to read the problems out of.</param>
    /// <remarks>
    /// Every other sheet describes the catalogue. This one lists what somebody would have to do something
    /// about, with the reason written next to each line, which is the sheet a shopkeeper would actually use.
    /// </remarks>
    private void CreateItemsToDealWithSheet(SimpleXlsxWriter workbook, List<GroceryItemDto> groceries)
    {
        var worksheet = workbook.AddSheet("Items to Deal With");

        worksheet.SetText(1, 1, "Item", XlsxStyle.HeaderCoral);
        worksheet.SetText(1, 2, "Code", XlsxStyle.HeaderCoral);
        worksheet.SetText(1, 3, "What to do about it", XlsxStyle.HeaderCoral);
        worksheet.SetText(1, 4, "Stock", XlsxStyle.HeaderCoral);
        worksheet.SetText(1, 5, "Reorder Point", XlsxStyle.HeaderCoral);

        var flagged = groceries
            .Select(item => new { Item = item, Reason = Reason(item) })
            .Where(entry => entry.Reason is not null)
            .OrderBy(entry => entry.Item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (flagged.Count == 0)
        {
            worksheet.SetText(2, 1, "Nothing needs attention in this catalogue.", XlsxStyle.Italic);
            return;
        }

        for (var index = 0; index < flagged.Count; index++)
        {
            var entry = flagged[index];
            var row = index + 2;

            worksheet.SetText(row, 1, entry.Item.Name);
            worksheet.SetText(row, 2, entry.Item.Sku);
            worksheet.SetText(row, 3, entry.Reason ?? string.Empty);
            worksheet.SetNumber(row, 4, entry.Item.Stock);
            worksheet.SetNumber(row, 5, entry.Item.ReorderPoint);
        }
    }

    /// <summary>
    /// Decides what, if anything, is wrong with one item.
    /// </summary>
    /// <param name="item">The item to judge.</param>
    /// <returns>The reason to act on it, or null when there is nothing to say.</returns>
    /// <remarks>
    /// One reason per item and the order is deliberate: the first thing a person would do about the line is
    /// the one worth writing down, and an item with three problems does not need three rows.
    /// </remarks>
    private string? Reason(GroceryItemDto item)
    {
        // An archived item is out of the catalogue on purpose, so nothing about it is a problem.
        if (item.IsArchived)
        {
            return null;
        }

        if (_analytics.Classify(item) == StockStatus.OutOfStock)
        {
            return "Out of stock: order it, or take it off the list";
        }

        if (item.Stock <= item.ReorderPoint)
        {
            return "At or below its reorder point";
        }

        if (item.IsBelowTargetMargin)
        {
            return "Margin is below the target set for this item";
        }

        if (string.IsNullOrWhiteSpace(item.Supplier))
        {
            return "No supplier recorded, so it cannot be reordered";
        }

        if (_analytics.Classify(item) == StockStatus.Overstock && item.StockRetailValue > 250m)
        {
            return "More held than it is worth holding";
        }

        return null;
    }
}
