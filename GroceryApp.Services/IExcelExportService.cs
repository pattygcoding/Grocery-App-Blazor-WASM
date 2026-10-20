using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Generates the downloadable report workbook for the application.
/// </summary>
/// <remarks>
/// The report is produced entirely in the browser, so the application keeps working on
/// static hosting (GitHub Pages) with no server side component.
/// </remarks>
public interface IExcelExportService
{
    /// <summary>
    /// Generates a comprehensive Excel report with 5 worksheets containing grocery analytics.
    /// </summary>
    /// <param name="groceries">The collection of grocery items to include in the report.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the Excel file contents.</returns>
    /// <remarks>
    /// The generated Excel file contains the following worksheets:
    /// 1. Key Metrics - Summary of key performance indicators
    /// 2. Profit by Category - Profit analysis grouped by product category
    /// 3. Top Profitable Items - Top 10 most profitable items
    /// 4. Stock Analysis - Analysis of stock levels (high, low, out of stock)
    /// 5. Price Range Analysis - Analysis by price ranges
    /// </remarks>
    Task<byte[]> GenerateReportExcelAsync(IEnumerable<GroceryItemDto> groceries);
}
