namespace GroceryApp.Tests;

/// <summary>
/// Tests for grouping the catalogue by supplier, which is a view over the items rather than a join against a
/// supplier table.
/// </summary>
public class SupplierAnalyticsServiceTests
{
    private readonly SupplierAnalyticsService _suppliers = new();

    private static GroceryItemDto Item(
        int id,
        string name,
        string supplier,
        decimal price = 2m,
        decimal cost = 1m,
        int stock = 10,
        int reorderPoint = 5,
        Category category = Category.Fruit,
        bool archived = false) => new()
    {
        Id = id,
        Name = name,
        Supplier = supplier,
        Category = category,
        Price = price,
        CostToProduce = cost,
        Stock = stock,
        ReorderPoint = reorderPoint,
        Unit = UnitOfMeasure.Each,
        IsArchived = archived
    };

    [Fact]
    public void Summarise_CountsAndValuesEachSuppliersItems()
    {
        var analysis = _suppliers.Summarise(new[]
        {
            Item(1, "Apple", "Wye"),
            Item(2, "Banana", "Wye"),
            Item(3, "Cheddar", "Dairy Co", price: 6m, cost: 3m)
        });

        Assert.Equal(2, analysis.Suppliers.Count);

        var wye = analysis.Suppliers.Single(supplier => supplier.Name == "Wye");
        Assert.Equal(2, wye.ItemCount);
        Assert.Equal(20, wye.UnitsHeld);
        Assert.Equal(40m, wye.ValueAtRetail);
        Assert.Equal(20m, wye.ValueAtCost);
        Assert.Equal(20m, wye.PotentialProfit);
    }

    [Fact]
    public void Summarise_OrdersTheLargestShareOfTheValueFirst()
    {
        var analysis = _suppliers.Summarise(new[]
        {
            Item(1, "Cheddar", "Dairy Co", price: 6m, cost: 3m, stock: 100),
            Item(2, "Apple", "Wye")
        });

        Assert.Equal("Dairy Co", analysis.Suppliers[0].Name);
        Assert.Equal(620m, analysis.TotalValueAtRetail);
    }

    [Fact]
    public void Summarise_GivesEachSupplierItsShareOfTheCatalogue()
    {
        var analysis = _suppliers.Summarise(new[]
        {
            Item(1, "Apple", "Wye"),
            Item(2, "Cheddar", "Dairy Co", price: 2m, cost: 1m)
        });

        Assert.All(analysis.Suppliers, supplier => Assert.Equal(50m, supplier.ShareOfCatalogueValue));
    }

    [Fact]
    public void Summarise_GroupsItemsThatNameNoSupplierTogether()
    {
        var analysis = _suppliers.Summarise(new[]
        {
            Item(1, "Apple", ""),
            Item(2, "Banana", "   "),
            Item(3, "Cheddar", "Dairy Co")
        });

        var unassigned = analysis.Suppliers.Single(supplier => supplier.IsUnassigned);
        Assert.Equal(2, unassigned.ItemCount);
        Assert.Equal(2, analysis.UnassignedItemCount);
    }

    [Fact]
    public void Summarise_CountsWhatNeedsOrdering()
    {
        var analysis = _suppliers.Summarise(new[]
        {
            Item(1, "Apple", "Wye", stock: 0),
            Item(2, "Banana", "Wye", stock: 4, reorderPoint: 5),
            Item(3, "Cheddar", "Wye", stock: 40)
        });

        var wye = Assert.Single(analysis.Suppliers);
        Assert.Equal(2, wye.NeedsOrderingCount);
        Assert.Equal(1, wye.OutOfStockCount);
    }

    [Fact]
    public void Summarise_LeavesArchivedItemsOutOfTheValueButStillCountsThem()
    {
        var analysis = _suppliers.Summarise(new[]
        {
            Item(1, "Apple", "Wye"),
            Item(2, "Old Pear", "Wye", stock: 500, archived: true)
        });

        var wye = Assert.Single(analysis.Suppliers);
        Assert.Equal(2, wye.ItemCount);
        Assert.Equal(1, wye.ArchivedCount);
        Assert.Equal(20m, wye.ValueAtRetail);
    }

    [Fact]
    public void Summarise_NeedingOrders_LeavesOutSuppliersWithNothingToOrder()
    {
        var analysis = _suppliers.Summarise(new[]
        {
            Item(1, "Apple", "Wye", stock: 0),
            Item(2, "Cheddar", "Dairy Co", stock: 40)
        });

        Assert.Equal("Wye", Assert.Single(analysis.NeedingOrders).Name);
    }

    [Fact]
    public void Summarise_ListsTheCategoriesASupplierCovers()
    {
        var analysis = _suppliers.Summarise(new[]
        {
            Item(1, "Apple", "Wye", category: Category.Fruit),
            Item(2, "Carrot", "Wye", category: Category.Vegetable),
            Item(3, "Apple", "Wye", category: Category.Fruit)
        });

        Assert.Equal(new[] { Category.Fruit, Category.Vegetable }, Assert.Single(analysis.Suppliers).Categories);
    }

    [Fact]
    public void Summarise_TrimsTheSupplierNameSoItGroupsWithItsNeighbours()
    {
        var analysis = _suppliers.Summarise(new[]
        {
            Item(1, "Apple", "Wye"),
            Item(2, "Banana", " Wye ")
        });

        Assert.Single(analysis.Suppliers);
        Assert.Equal("Wye", analysis.Suppliers[0].Name);
    }

    [Fact]
    public void Summarise_WithNoItems_ReturnsAnEmptyAnalysis()
    {
        var analysis = _suppliers.Summarise(Array.Empty<GroceryItemDto>());

        Assert.Empty(analysis.Suppliers);
        Assert.Equal(0m, analysis.TotalValueAtRetail);
    }

    [Fact]
    public void Summarise_WhenTheItemsAreNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _suppliers.Summarise(null!));
    }
}