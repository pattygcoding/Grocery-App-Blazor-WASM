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
}
