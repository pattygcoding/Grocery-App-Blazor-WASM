using System.IO.Compression;
using GroceryApp.Services.Xlsx;

namespace GroceryApp.Tests;

/// <summary>
/// Tests for the client side Excel report generator that replaced ClosedXML in the
/// Blazor WebAssembly build.
/// </summary>
public class XlsxExportServiceTests
{
    private readonly XlsxExportService _service = new(new InventoryAnalyticsService());

    private static List<GroceryItemDto> CreateItems() => new()
    {
        new GroceryItemDto { Id = 1, Name = "Apple", Category = Category.Fruit, Price = 1.00m, CostToProduce = 0.50m, Stock = 10 },
        new GroceryItemDto { Id = 2, Name = "Cheddar Cheese", Category = Category.Cheese, Price = 4.00m, CostToProduce = 2.00m, Stock = 4 },
        new GroceryItemDto { Id = 3, Name = "Lamb Chops", Category = Category.Meat, Price = 12.00m, CostToProduce = 6.00m, Stock = 0 }
    };

    private static ZipArchive Open(byte[] workbook) => new(new MemoryStream(workbook), ZipArchiveMode.Read);

    private static string ReadEntry(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        Assert.NotNull(entry);

        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task GenerateReportExcelAsync_ProducesAnOpenXmlPackageWithEightWorksheets()
    {
        // Act
        var workbook = await _service.GenerateReportExcelAsync(CreateItems());

        // Assert
        using var archive = Open(workbook);
        var entries = archive.Entries.Select(entry => entry.FullName).ToList();

        Assert.Contains("[Content_Types].xml", entries);
        Assert.Contains("_rels/.rels", entries);
        Assert.Contains("xl/workbook.xml", entries);
        Assert.Contains("xl/_rels/workbook.xml.rels", entries);
        Assert.Contains("xl/styles.xml", entries);
        Assert.Equal(8, entries.Count(name => name.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal)));

        var workbookXml = ReadEntry(archive, "xl/workbook.xml");

        foreach (var sheetName in new[]
                 {
                     "Key Metrics", "Profit by Category", "Top Profitable Items", "Stock Analysis",
                     "Price Range Analysis", "ABC Analysis", "Supplier Summary", "Items to Deal With"
                 })
        {
            Assert.Contains($"name=\"{sheetName}\"", workbookXml);
        }
    }

    [Fact]
    public async Task GenerateReportExcelAsync_AbcSheet_BandsTheValueHeld()
    {
        // Act
        var workbook = await _service.GenerateReportExcelAsync(CreateItems());

        // Assert
        using var archive = Open(workbook);
        var sheet = ReadEntry(archive, "xl/worksheets/sheet6.xml");

        Assert.Contains("Band", sheet);
        Assert.Contains("Running Share", sheet);
        Assert.Contains("of 3 items carry", sheet);
    }

    [Fact]
    public async Task GenerateReportExcelAsync_SupplierSheet_SaysWhenThereIsNoSupplier()
    {
        // Arrange: none of the sample items records a supplier.
        var items = CreateItems();

        // Act
        var workbook = await _service.GenerateReportExcelAsync(items);

        // Assert
        using var archive = Open(workbook);
        var sheet = ReadEntry(archive, "xl/worksheets/sheet7.xml");

        Assert.Contains("(no supplier)", sheet);
        Assert.Contains("Value at Retail", sheet);
    }

    [Fact]
    public async Task GenerateReportExcelAsync_ItemsToDealWithSheet_ExplainsEachLine()
    {
        // Act
        var workbook = await _service.GenerateReportExcelAsync(CreateItems());

        // Assert
        using var archive = Open(workbook);
        var sheet = ReadEntry(archive, "xl/worksheets/sheet8.xml");

        // The lamb has no stock, which is the first thing somebody would do about the line.
        Assert.Contains("Lamb Chops", sheet);
        Assert.Contains("Out of stock", sheet);
    }

    [Fact]
    public async Task GenerateReportExcelAsync_ForAnEmptyCatalogue_StillWritesEverySheet()
    {
        // Act
        var workbook = await _service.GenerateReportExcelAsync(new List<GroceryItemDto>());

        // Assert: the workbook opens rather than being a file with sheets missing from it.
        using var archive = Open(workbook);
        var sheet = ReadEntry(archive, "xl/worksheets/sheet8.xml");

        Assert.Contains("Nothing needs attention", sheet);
    }

    [Fact]
    public async Task GenerateReportExcelAsync_KeyMetricsSheet_ContainsTitleAndCalculatedTotals()
    {
        // Act
        var workbook = await _service.GenerateReportExcelAsync(CreateItems());

        // Assert
        using var archive = Open(workbook);
        var sheet = ReadEntry(archive, "xl/worksheets/sheet1.xml");

        Assert.Contains("Grocery Reports - Key Metrics Summary", sheet);
        Assert.Contains("<mergeCell ref=\"A1:C1\"/>", sheet);
        Assert.Contains("Total Profit Potential:", sheet);
        // (1.00 - 0.50) * 10 + (4.00 - 2.00) * 4 + (12.00 - 6.00) * 0 = 13
        Assert.Contains("<v>13.00</v>", sheet);
        Assert.Contains("Product Categories:", sheet);
        Assert.Contains("<v>3</v>", sheet);
    }

    [Fact]
    public async Task GenerateReportExcelAsync_ProfitByCategorySheet_OrdersByPotentialProfitDescending()
    {
        // Act
        var workbook = await _service.GenerateReportExcelAsync(CreateItems());

        // Assert
        using var archive = Open(workbook);
        var sheet = ReadEntry(archive, "xl/worksheets/sheet2.xml");

        var cheeseIndex = sheet.IndexOf("Cheese", StringComparison.Ordinal);
        var fruitIndex = sheet.IndexOf("Fruit", StringComparison.Ordinal);
        var meatIndex = sheet.IndexOf("Meat", StringComparison.Ordinal);

        Assert.True(cheeseIndex > 0 && fruitIndex > cheeseIndex && meatIndex > fruitIndex,
            "Categories should be written in descending order of total potential profit.");
    }

    [Fact]
    public async Task GenerateReportExcelAsync_TopProfitableSheet_ListsHighestProfitFirst()
    {
        // Act
        var workbook = await _service.GenerateReportExcelAsync(CreateItems());

        // Assert
        using var archive = Open(workbook);
        var sheet = ReadEntry(archive, "xl/worksheets/sheet3.xml");

        var lambChops = sheet.IndexOf("Lamb Chops", StringComparison.Ordinal);
        var cheddar = sheet.IndexOf("Cheddar Cheese", StringComparison.Ordinal);
        var apple = sheet.IndexOf("Apple", StringComparison.Ordinal);

        Assert.True(lambChops > 0 && cheddar > lambChops && apple > cheddar,
            "Items should be written in descending order of profit.");
        Assert.Contains("Margin %", sheet);
    }

    [Fact]
    public async Task GenerateReportExcelAsync_StockAnalysisSheet_ClassifiesStockLevels()
    {
        // Act
        var workbook = await _service.GenerateReportExcelAsync(CreateItems());

        // Assert
        using var archive = Open(workbook);
        var sheet = ReadEntry(archive, "xl/worksheets/sheet4.xml");

        Assert.Contains("High Stock (&gt;5)", sheet);
        Assert.Contains("Low Stock (1-5)", sheet);
        Assert.Contains("Out of Stock (0)", sheet);
        // One item per level in the test data.
        Assert.Equal(3, sheet.Split("<v>1</v>").Length - 1);
    }

    [Fact]
    public async Task GenerateReportExcelAsync_PriceRangeSheet_ContainsThePriceBands()
    {
        // Act
        var workbook = await _service.GenerateReportExcelAsync(CreateItems());

        // Assert
        using var archive = Open(workbook);
        var sheet = ReadEntry(archive, "xl/worksheets/sheet5.xml");

        Assert.Contains("$1-$3", sheet);
        Assert.Contains("$3-$5", sheet);
        Assert.Contains("$10+", sheet);
    }

    [Fact]
    public async Task GenerateReportExcelAsync_UsesInlineStringsSoNoSharedStringTableIsNeeded()
    {
        // Act
        var workbook = await _service.GenerateReportExcelAsync(CreateItems());

        // Assert
        using var archive = Open(workbook);

        Assert.Null(archive.GetEntry("xl/sharedStrings.xml"));
        Assert.Contains("t=\"inlineStr\"", ReadEntry(archive, "xl/worksheets/sheet1.xml"));
    }

    [Fact]
    public async Task GenerateReportExcelAsync_StylesPartDefinesCurrencyAndPercentageFormats()
    {
        // Act
        var workbook = await _service.GenerateReportExcelAsync(CreateItems());

        // Assert
        using var archive = Open(workbook);
        var styles = ReadEntry(archive, "xl/styles.xml");

        Assert.Contains("formatCode='&quot;$&quot;#,##0.00'", styles);
        Assert.Contains("formatCode='0.0%'", styles);
        Assert.Contains("<cellXfs count='18'>", styles);
    }

    [Fact]
    public async Task GenerateReportExcelAsync_EscapesItemNamesInSheetXml()
    {
        // Arrange
        var items = new List<GroceryItemDto>
        {
            new() { Id = 1, Name = "Salt & Pepper <best>", Category = Category.Vegetable, Price = 2.00m, CostToProduce = 1.00m, Stock = 5 }
        };

        // Act
        var workbook = await _service.GenerateReportExcelAsync(items);

        // Assert
        using var archive = Open(workbook);
        var sheet = ReadEntry(archive, "xl/worksheets/sheet3.xml");

        Assert.Contains("Salt &amp; Pepper &lt;best&gt;", sheet);
        Assert.DoesNotContain("Salt & Pepper <best>", sheet);
    }

    [Fact]
    public async Task GenerateReportExcelAsync_WithNoItems_StillProducesTheFullWorkbook()
    {
        // Act
        var workbook = await _service.GenerateReportExcelAsync(new List<GroceryItemDto>());

        // Assert
        using var archive = Open(workbook);
        Assert.Equal(8, archive.Entries.Count(entry => entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal)));
        Assert.Contains("<v>0</v>", ReadEntry(archive, "xl/worksheets/sheet1.xml"));
    }
}
