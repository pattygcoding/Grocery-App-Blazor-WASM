namespace GroceryApp.Tests;

/// <summary>
/// Tests for reading a catalogue out of a file, and for judging every row before any of it is written. The
/// parser is the part of the application most exposed to input it did not produce, so most of these are
/// about text that is nearly right.
/// </summary>
public class InventoryImportServiceTests
{
    private readonly IGroceryService _groceries = Substitute.For<IGroceryService>();
    private readonly ICatalogueHistoryService _history = Substitute.For<ICatalogueHistoryService>();
    private readonly IActivityService _activity = Substitute.For<IActivityService>();
    private readonly InventoryImportService _import;

    public InventoryImportServiceTests()
    {
        _import = new InventoryImportService(_groceries, _history, _activity);
    }

    private const string Header = "name,category,price,costToProduce,stock,supplier\n";

    private static GroceryItemDto Existing(
        int id,
        string name,
        string sku = "",
        decimal price = 1m,
        decimal cost = 0.5m,
        int stock = 5,
        string supplier = "") => new()
    {
        Id = id,
        Name = name,
        Sku = sku,
        Category = Category.Fruit,
        Price = price,
        CostToProduce = cost,
        Stock = stock,
        Supplier = supplier,
        Unit = UnitOfMeasure.Each
    };

    #region Reading CSV

    [Fact]
    public void Preview_ReadsAHeaderAndOneRow()
    {
        var preview = _import.Preview(
            Header + "Conference Pear,Fruit,1.35,0.62,24,Wye Valley Produce",
            ImportFormat.Csv,
            null);

        Assert.True(preview.IsReadable);
        var row = Assert.Single(preview.Rows);
        Assert.Equal(ImportStatus.New, row.Status);
        Assert.Equal("Conference Pear", row.Name);
        Assert.Equal(1.35m, row.Values!.Price);
        Assert.Equal(24, row.Values.Stock);
        Assert.Equal("Wye Valley Produce", row.Values.Supplier);
    }

    [Fact]
    public void Preview_WithQuotedFields_KeepsTheDelimiterInsideThem()
    {
        // A supplier with a comma in its name is the reason the parser cannot be a Split call.
        var preview = _import.Preview(
            Header + "\"Conference Pear, large\",Fruit,1.35,0.62,24,\"Wye Valley, Ltd\"",
            ImportFormat.Csv,
            null);

        var row = Assert.Single(preview.Rows);
        Assert.Equal("Conference Pear, large", row.Name);
        Assert.Equal("Wye Valley, Ltd", row.Values!.Supplier);
    }

    [Fact]
    public void Preview_WithDoubledQuotes_ReadsOneQuote()
    {
        var preview = _import.Preview(
            Header + "\"Pear \"\"Special\"\"\",Fruit,1.35,0.62,24,Wye",
            ImportFormat.Csv,
            null);

        Assert.Equal("Pear \"Special\"", Assert.Single(preview.Rows).Name);
    }

    [Fact]
    public void Preview_ReadsColumnsInWhateverOrderTheFileUsesThem()
    {
        var preview = _import.Preview(
            "stock,price,name,costToProduce,category\n24,1.35,Conference Pear,0.62,Fruit",
            ImportFormat.Csv,
            null);

        var row = Assert.Single(preview.Rows);
        Assert.Equal("Conference Pear", row.Name);
        Assert.Equal(1.35m, row.Values!.Price);
        Assert.Equal(24, row.Values.Stock);
    }

    [Fact]
    public void Preview_IgnoresColumnsTheApplicationDoesNotKnow()
    {
        var preview = _import.Preview(
            "name,category,price,costToProduce,stock,barcode,aisle\nPear,Fruit,1.35,0.62,24,5012345,A3",
            ImportFormat.Csv,
            null);

        Assert.Equal(ImportStatus.New, Assert.Single(preview.Rows).Status);
    }

    [Fact]
    public void Preview_WithNoRowsAfterTheHeader_SaysSoRatherThanReportingNothing()
    {
        var preview = _import.Preview(Header, ImportFormat.Csv, null);

        Assert.False(preview.IsReadable);
        Assert.NotNull(preview.Error);
    }

    [Fact]
    public void Preview_WithNoText_SaysThereIsNothingToRead()
    {
        Assert.False(_import.Preview("   ", ImportFormat.Csv, null).IsReadable);
        Assert.False(_import.Preview(null, ImportFormat.Csv, null).IsReadable);
    }

    #endregion

    #region Reading JSON

    [Fact]
    public void Preview_ReadsAJsonArray()
    {
        var preview = _import.Preview(
            """[{"name":"Conference Pear","category":"Fruit","price":1.35,"costToProduce":0.62,"stock":24,"supplier":"Wye"}]""",
            ImportFormat.Json,
            null);

        var row = Assert.Single(preview.Rows);
        Assert.Equal(ImportStatus.New, row.Status);
        Assert.Equal(1.35m, row.Values!.Price);
        Assert.Equal("Wye", row.Values.Supplier);
    }

    [Fact]
    public void Preview_ReadsJsonNumbersAndStringsAlike()
    {
        var preview = _import.Preview(
            """[{"name":"Pear","category":"Fruit","price":"1.35","costToProduce":"0.62","stock":"24"}]""",
            ImportFormat.Json,
            null);

        var row = Assert.Single(preview.Rows);
        Assert.Equal(1.35m, row.Values!.Price);
        Assert.Equal(24, row.Values.Stock);
    }

    [Fact]
    public void Preview_WhenJsonIsNotAnArray_ReportsThatItCouldNotBeRead()
    {
        var preview = _import.Preview("""{"name":"Pear"}""", ImportFormat.Json, null);

        Assert.False(preview.IsReadable);
        Assert.Contains("array", preview.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Preview_WhenTheTextIsNotJson_ReportsThatItCouldNotBeRead()
    {
        var preview = _import.Preview("this is not json at all", ImportFormat.Json, null);

        Assert.False(preview.IsReadable);
        Assert.NotNull(preview.Error);
    }

    #endregion

    #region Judging rows

    [Fact]
    public void Preview_WhenTheCatalogueHasNoSuchItem_ReportsItAsNew()
    {
        var preview = _import.Preview(
            Header + "Conference Pear,Fruit,1.35,0.62,24,Wye",
            ImportFormat.Csv,
            new[] { Existing(1, "Apple") });

        Assert.Equal(ImportStatus.New, Assert.Single(preview.Rows).Status);
        Assert.Equal(1, preview.NewCount);
    }

    [Fact]
    public void Preview_MatchesByCodeBeforeName()
    {
        // The code identifies the line, so a row carrying one is matched on it even when the name has been
        // typed differently. This is what makes a file with renamed items usable.
        var preview = _import.Preview(
            "name,sku,category,price,costToProduce,stock\nComice Pear,FRU-0007,Fruit,1.35,0.62,24",
            ImportFormat.Csv,
            new[] { Existing(7, "Conference Pear", sku: "FRU-0007", price: 2m, cost: 0.62m, stock: 24) });

        var row = Assert.Single(preview.Rows);
        Assert.Equal(7, row.MatchedItemId);
        Assert.Equal(ImportStatus.Update, row.Status);
    }

    [Fact]
    public void Preview_WhenTheRowHasNoName_RefusesItRatherThanMatchingOnTheCodeAlone()
    {
        // An item has to be called something: a code identifies a line, but a catalogue with nameless rows in
        // it is worse than one that refused the file.
        var preview = _import.Preview(
            "sku,name,category,price,costToProduce,stock\nFRU-0007,,Fruit,2,1,9",
            ImportFormat.Csv,
            new[] { Existing(7, "Conference Pear", sku: "FRU-0007") });

        var row = Assert.Single(preview.Rows);
        Assert.Equal(ImportStatus.Invalid, row.Status);
        Assert.Equal("No name", row.Reason);
    }

    [Fact]
    public void Preview_WhenTheRowWouldChangeTheItem_ReportsWhatWouldChange()
    {
        var preview = _import.Preview(
            Header + "Conference Pear,Fruit,1.75,0.62,24,Wye",
            ImportFormat.Csv,
            new[] { Existing(1, "Conference Pear", price: 1.35m, cost: 0.62m, stock: 24, supplier: "Wye") });

        var row = Assert.Single(preview.Rows);
        Assert.Equal(ImportStatus.Update, row.Status);
        Assert.Contains("price from", row.Reason);
    }

    [Fact]
    public void Preview_WhenTheRowMatchesExactly_ReportsNoChange()
    {
        var preview = _import.Preview(
            Header + "Conference Pear,Fruit,1.35,0.62,24,Wye",
            ImportFormat.Csv,
            new[] { Existing(1, "Conference Pear", price: 1.35m, cost: 0.62m, stock: 24, supplier: "Wye") });

        var row = Assert.Single(preview.Rows);
        Assert.Equal(ImportStatus.Unchanged, row.Status);
        Assert.Equal(1, preview.UnchangedCount);
    }

    [Fact]
    public void Preview_WhenTheFileLeavesTheCodeOut_DoesNotTreatTheGeneratedCodeAsAChange()
    {
        // The catalogue generates a code when one is not given, so a file with no code column has nothing to
        // say about it and must not read as a change on that account.
        var preview = _import.Preview(
            "name,category,price,costToProduce,stock\nConference Pear,Fruit,1.35,0.62,24",
            ImportFormat.Csv,
            new[] { Existing(1, "Conference Pear", sku: "FRU-0001", price: 1.35m, cost: 0.62m, stock: 24) });

        Assert.Equal(ImportStatus.Unchanged, Assert.Single(preview.Rows).Status);
    }

    [Theory]
    [InlineData("Conference Pear,Herbs,1.35,0.62,24,Wye", "not one of the five categories")]
    [InlineData("Conference Pear,Fruit,,0.62,24,Wye", "No price")]
    [InlineData("Conference Pear,Fruit,1.35,,24,Wye", "No cost")]
    [InlineData("Conference Pear,Fruit,0,0.62,24,Wye", "greater than zero")]
    [InlineData("Conference Pear,Fruit,1.35,0.62,-4,Wye", "whole number")]
    [InlineData(",Fruit,1.35,0.62,24,Wye", "No name")]
    public void Preview_WhenARowCannotBeRead_SaysWhy(string line, string expectedReason)
    {
        var preview = _import.Preview(Header + line, ImportFormat.Csv, null);

        var row = Assert.Single(preview.Rows);
        Assert.Equal(ImportStatus.Invalid, row.Status);
        Assert.Contains(expectedReason, row.Reason);
        Assert.False(row.WouldWrite);
        Assert.Null(row.Values);
    }

    [Fact]
    public void Preview_CountsEveryKindOfRow()
    {
        var catalogue = new[]
        {
            Existing(1, "Apple", price: 1m, cost: 0.5m, stock: 5),
            Existing(2, "Banana", price: 1m, cost: 0.5m, stock: 5)
        };

        var preview = _import.Preview(
            Header +
            "Apple,Fruit,1,0.5,5,\n" +          // unchanged
            "Banana,Fruit,2,0.5,5,\n" +         // update
            "Conference Pear,Fruit,1.35,0.62,24,Wye\n" +  // new
            "Bad row,Herbs,1,1,1,\n",           // invalid
            ImportFormat.Csv,
            catalogue);

        Assert.Equal(1, preview.NewCount);
        Assert.Equal(1, preview.UpdateCount);
        Assert.Equal(1, preview.UnchangedCount);
        Assert.Equal(1, preview.InvalidCount);
        Assert.Equal(4, preview.Rows.Count);
        Assert.True(preview.HasWrites);
    }

    [Fact]
    public void Summary_SaysWhatTheFileWouldDo()
    {
        var preview = _import.Preview(
            Header + "Conference Pear,Fruit,1.35,0.62,24,Wye\nBad row,Herbs,1,1,1,",
            ImportFormat.Csv,
            null);

        Assert.Contains("1 row would be added", preview.Summary);
        Assert.Contains("1 cannot be read", preview.Summary);
    }

    [Fact]
    public void Preview_WritableCount_IgnoresUpdatesUnlessTheyWereAskedFor()
    {
        var preview = _import.Preview(
            Header + "Banana,Fruit,2,0.5,5,",
            ImportFormat.Csv,
            new[] { Existing(2, "Banana", price: 1m, cost: 0.5m, stock: 5) });

        Assert.Equal(0, preview.WritableCount(updateExisting: false));
        Assert.Equal(1, preview.WritableCount(updateExisting: true));
    }

    #endregion
}