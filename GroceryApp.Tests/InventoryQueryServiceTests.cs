using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Tests;

/// <summary>
/// Tests for the catalogue filter: what each control keeps, how the parts combine, how ordering works
/// and what the filter bar is told about the catalogue behind the results.
/// </summary>
public class InventoryQueryServiceTests
{
    private readonly InventoryQueryService _query = new(new InventoryAnalyticsService());

    private static GroceryItemDto Item(
        int id,
        string name,
        Category category = Category.Fruit,
        string supplier = "Acme",
        decimal price = 4m,
        decimal cost = 2m,
        int stock = 10,
        int reorderPoint = 5,
        bool archived = false,
        params string[] tags) => new()
        {
            Id = id,
            Name = name,
            Sku = $"SKU-{id:D4}",
            Category = category,
            Supplier = supplier,
            Price = price,
            CostToProduce = cost,
            Stock = stock,
            ReorderPoint = reorderPoint,
            IsArchived = archived,
            TargetMarginPercent = 40m,
            Tags = tags.ToList()
        };

    private static InventoryQueryResult Run(IEnumerable<GroceryItemDto> items, InventoryFilter filter) =>
        new InventoryQueryService(new InventoryAnalyticsService()).Query(items, filter);

    #region What each control keeps

    [Fact]
    public void Query_WhenTheFilterIsEmpty_ReturnsEverythingInNameOrder()
    {
        var result = _query.Query(
            new[] { Item(1, "Cherry"), Item(2, "Apple"), Item(3, "Banana") },
            new InventoryFilter());

        Assert.Equal(3, result.TotalBeforeFiltering);
        Assert.Equal(new[] { "Apple", "Banana", "Cherry" }, result.Items.Select(item => item.Name));
    }

    [Fact]
    public void Query_ExcludesArchivedItemsUnlessTheyAreAskedFor()
    {
        var items = new[] { Item(1, "Apple"), Item(2, "Retired", archived: true) };

        Assert.Equal(new[] { "Apple" }, Run(items, new InventoryFilter()).Items.Select(item => item.Name));
        Assert.Equal(2, Run(items, new InventoryFilter { IncludeArchived = true }).Items.Count);
    }

    [Fact]
    public void Query_ReportsHowManyItemsWereLeftOutBecauseTheyWereArchived()
    {
        var result = _query.Query(
            new[]
            {
                Item(1, "Apple"),
                Item(2, "Retired", archived: true),
                Item(3, "Also retired", archived: true)
            },
            new InventoryFilter());

        Assert.Equal(2, result.ArchivedExcluded);
        Assert.Equal(3, result.TotalBeforeFiltering);
    }

    [Fact]
    public void Query_MatchesSearchTextAgainstNameSkuSupplierNotesAndTags()
    {
        var items = new[]
        {
            Item(1, "Conference Pear"),
            Item(2, "Cheddar", supplier: "Dairy Co"),
            Item(3, "Something else", tags: new[] { "chilled" })
        };

        Assert.Equal(
            new[] { "Conference Pear" },
            Run(items, new InventoryFilter { SearchText = "conference" }).Items.Select(item => item.Name));

        Assert.Equal(
            new[] { "Cheddar" },
            Run(items, new InventoryFilter { SearchText = "SKU-0002" }).Items.Select(item => item.Name));

        Assert.Equal(
            new[] { "Cheddar" },
            Run(items, new InventoryFilter { SearchText = "dairy" }).Items.Select(item => item.Name));

        Assert.Equal(
            new[] { "Something else" },
            Run(items, new InventoryFilter { SearchText = "chilled" }).Items.Select(item => item.Name));
    }

    [Fact]
    public void Query_RequiresEverySearchWordToMatchButNotAllInTheSameField()
    {
        var items = new[]
        {
            Item(1, "Bulk Cheddar", supplier: "Dairy Co"),
            Item(2, "Cheddar Wedge", supplier: "Other Co")
        };

        // "cheddar" matches both names; "dairy" narrows it to the one whose supplier says so.
        var result = Run(items, new InventoryFilter { SearchText = "cheddar dairy" });

        Assert.Equal(new[] { "Bulk Cheddar" }, result.Items.Select(item => item.Name));
    }

    [Fact]
    public void Query_WhenOneSearchWordMatchesNothing_ReturnsNothing()
    {
        var result = Run(new[] { Item(1, "Apple") }, new InventoryFilter { SearchText = "apple unicorn" });

        Assert.Empty(result.Items);
    }

    [Fact]
    public void Query_FiltersByAnyOfTheSelectedCategories()
    {
        var items = new[]
        {
            Item(1, "Apple", Category.Fruit),
            Item(2, "Brie", Category.Cheese),
            Item(3, "Carrot", Category.Vegetable)
        };

        var result = Run(items, new InventoryFilter
        {
            Categories = new List<Category> { Category.Fruit, Category.Cheese }
        });

        Assert.Equal(new[] { "Apple", "Brie" }, result.Items.Select(item => item.Name));
    }

    #endregion

    #region Suppliers, tags, price, margin and stock

    [Fact]
    public void Query_FiltersBySupplierIgnoringCase()
    {
        var items = new[] { Item(1, "Apple", supplier: "Acme"), Item(2, "Brie", supplier: "Dairy Co") };

        var result = Run(items, new InventoryFilter { Suppliers = new List<string> { "acme" } });

        Assert.Equal(new[] { "Apple" }, result.Items.Select(item => item.Name));
    }

    [Fact]
    public void Query_FiltersByTagsUsingAnyOfThem()
    {
        var items = new[]
        {
            Item(1, "Apple", tags: new[] { "chilled" }),
            Item(2, "Brie", tags: new[] { "bulk" }),
            Item(3, "Carrot")
        };

        var result = Run(items, new InventoryFilter { Tags = new List<string> { "chilled", "bulk" } });

        Assert.Equal(new[] { "Apple", "Brie" }, result.Items.Select(item => item.Name));
    }

    [Fact]
    public void Query_FiltersByPriceRangeInclusively()
    {
        var items = new[] { Item(1, "Cheap", price: 1m), Item(2, "Middle", price: 5m), Item(3, "Dear", price: 20m) };

        var result = Run(items, new InventoryFilter { MinimumPrice = 1m, MaximumPrice = 5m });

        Assert.Equal(new[] { "Cheap", "Middle" }, result.Items.Select(item => item.Name));
    }

    [Fact]
    public void Query_FiltersToItemsWhoseMarginIsUnderTheThreshold()
    {
        var items = new[]
        {
            Item(1, "Thin", price: 10m, cost: 9m),
            Item(2, "Comfortable", price: 10m, cost: 5m)
        };

        var result = Run(items, new InventoryFilter { MaximumMarginPercent = 30m });

        Assert.Equal(new[] { "Thin" }, result.Items.Select(item => item.Name));
    }

    [Fact]
    public void Query_FiltersByStockStatusIncludingTheOrWorseOptions()
    {
        var items = new[]
        {
            Item(1, "Empty", stock: 0, reorderPoint: 5),
            Item(2, "Critical", stock: 1, reorderPoint: 20),
            Item(3, "Low", stock: 4, reorderPoint: 5),
            Item(4, "Fine", stock: 10, reorderPoint: 5),
            Item(5, "Piled up", stock: 100, reorderPoint: 5)
        };

        Assert.Equal(
            new[] { "Empty" },
            Run(items, new InventoryFilter { Stock = StockFilter.OutOfStock }).Items.Select(i => i.Name));

        Assert.Equal(
            new[] { "Critical", "Empty" },
            Run(items, new InventoryFilter { Stock = StockFilter.CriticalOrWorse }).Items.Select(i => i.Name));

        Assert.Equal(
            new[] { "Critical", "Empty", "Low" },
            Run(items, new InventoryFilter { Stock = StockFilter.AtOrBelowReorderPoint }).Items.Select(i => i.Name));

        Assert.Equal(
            new[] { "Fine" },
            Run(items, new InventoryFilter { Stock = StockFilter.Healthy }).Items.Select(i => i.Name));

        Assert.Equal(
            new[] { "Piled up" },
            Run(items, new InventoryFilter { Stock = StockFilter.Overstock }).Items.Select(i => i.Name));
    }

    [Fact]
    public void Query_FiltersByAbcBand()
    {
        // One item holds almost all of the value, so it is the only item in band A.
        var items = new[]
        {
            Item(1, "Big", price: 10m, cost: 5m, stock: 100),
            Item(2, "Small one", price: 1m, cost: 0.5m, stock: 10),
            Item(3, "Small two", price: 1m, cost: 0.5m, stock: 10)
        };

        Assert.Equal(
            new[] { "Big" },
            Run(items, new InventoryFilter { AbcClass = AbcClass.A }).Items.Select(i => i.Name));

        Assert.Empty(Run(items, new InventoryFilter { AbcClass = AbcClass.B }).Items);

        Assert.Equal(
            new[] { "Small one", "Small two" },
            Run(items, new InventoryFilter { AbcClass = AbcClass.C }).Items.Select(i => i.Name));
    }

    [Fact]
    public void Query_WhenFavouritesAreAskedFor_KeepsOnlyTheStarredItems()
    {
        var items = new[] { Item(1, "Apple"), Item(2, "Banana"), Item(3, "Cherry") };

        var result = _query.Query(items, new InventoryFilter { FavouritesOnly = true }, new[] { 1, 3 });

        Assert.Equal(new[] { "Apple", "Cherry" }, result.Items.Select(item => item.Name));
    }

    [Fact]
    public void Query_WhenFavouritesAreAskedForAndNoneAreKnown_ReturnsNothing()
    {
        var result = _query.Query(new[] { Item(1, "Apple") }, new InventoryFilter { FavouritesOnly = true });

        Assert.Empty(result.Items);
    }

    [Fact]
    public void Query_ReportsWhenTheFilterNarrowsTheCatalogueRatherThanOnlyOrderingIt()
    {
        Assert.False(new InventoryFilter().IsNarrowing);
        Assert.True(new InventoryFilter { SearchText = "apple" }.IsNarrowing);
        Assert.True(new InventoryFilter { Stock = StockFilter.OutOfStock }.IsNarrowing);
        Assert.False(new InventoryFilter { SortBy = InventorySortField.Margin }.IsNarrowing);
    }

    #endregion

    #region Ordering, facets and totals

    [Fact]
    public void Query_OrdersDescendingByReversingTheAscendingOrder()
    {
        var items = new[] { Item(1, "Apple"), Item(2, "Banana"), Item(3, "Cherry") };

        var result = Run(items, new InventoryFilter
        {
            SortBy = InventorySortField.Name,
            Direction = SortDirection.Descending
        });

        Assert.Equal(new[] { "Cherry", "Banana", "Apple" }, result.Items.Select(item => item.Name));
    }

    [Fact]
    public void Query_OrdersByMargin()
    {
        var items = new[]
        {
            Item(1, "Thin", price: 10m, cost: 9m),
            Item(2, "Fat", price: 10m, cost: 2m)
        };

        Assert.Equal(
            new[] { "Thin", "Fat" },
            Run(items, new InventoryFilter { SortBy = InventorySortField.Margin }).Items.Select(i => i.Name));

        Assert.Equal(
            new[] { "Fat", "Thin" },
            Run(items, new InventoryFilter
            {
                SortBy = InventorySortField.Margin,
                Direction = SortDirection.Descending
            }).Items.Select(i => i.Name));
    }

    [Fact]
    public void Query_OrdersByTheValueOfTheStockRatherThanTheUnitPrice()
    {
        var items = new[]
        {
            Item(1, "Many cheap", price: 1m, stock: 100),
            Item(2, "One dear", price: 50m, stock: 1)
        };

        Assert.Equal(
            new[] { "One dear", "Many cheap" },
            Run(items, new InventoryFilter { SortBy = InventorySortField.StockValue }).Items.Select(i => i.Name));
    }

    [Fact]
    public void Query_OrdersByAbcBandSoTheMostValuableItemsComeFirst()
    {
        var items = new[]
        {
            Item(1, "Small one", price: 1m, cost: 0.5m, stock: 10),
            Item(2, "Big", price: 10m, cost: 5m, stock: 100),
            Item(3, "Small two", price: 1m, cost: 0.5m, stock: 10)
        };

        Assert.Equal(
            new[] { "Big", "Small one", "Small two" },
            Run(items, new InventoryFilter { SortBy = InventorySortField.AbcClass }).Items.Select(i => i.Name));
    }

    [Fact]
    public void Query_ReportsTheSuppliersAndTagsPresentInTheWholeCatalogueNotJustTheResults()
    {
        var items = new[]
        {
            Item(1, "Apple", supplier: "Acme", tags: new[] { "chilled" }),
            Item(2, "Brie", Category.Cheese, supplier: "Dairy Co", tags: new[] { "bulk" })
        };

        var result = Run(items, new InventoryFilter { Categories = new List<Category> { Category.Fruit } });

        // The filter bar has to keep offering the options that exist, otherwise a visitor can narrow
        // themselves into a corner with no way back.
        Assert.Equal(new[] { "Acme", "Dairy Co" }, result.Suppliers);
        Assert.Equal(new[] { "bulk", "chilled" }, result.Tags);
        Assert.Equal(new[] { "Apple" }, result.Items.Select(item => item.Name));
    }

    [Fact]
    public void Query_ReportsThePriceRangeOfTheCatalogueForTheRangeControl()
    {
        var result = Run(
            new[] { Item(1, "Cheap", price: 0.5m), Item(2, "Dear", price: 12.5m) },
            new InventoryFilter());

        Assert.Equal(0.5m, result.LowestPrice);
        Assert.Equal(12.5m, result.HighestPrice);
    }

    [Fact]
    public void Query_ReportsTheRetailValueOfTheItemsThatSurvivedTheFilter()
    {
        var items = new[] { Item(1, "Apple", price: 2m, stock: 10), Item(2, "Brie", Category.Cheese, price: 9m, stock: 10) };

        var result = Run(items, new InventoryFilter { Categories = new List<Category> { Category.Fruit } });

        Assert.Equal(20m, result.FilteredRetailValue);
    }

    [Fact]
    public void Query_WhenTheCatalogueIsEmpty_ReturnsNoItemsAndNoFacets()
    {
        var result = _query.Query(Array.Empty<GroceryItemDto>(), new InventoryFilter());

        Assert.Empty(result.Items);
        Assert.Empty(result.Suppliers);
        Assert.Empty(result.Tags);
        Assert.Equal(0m, result.LowestPrice);
        Assert.Equal(0m, result.HighestPrice);
        Assert.Equal(0m, result.FilteredRetailValue);
    }

    [Fact]
    public void Query_WhenTheCatalogueIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _query.Query(null!, new InventoryFilter()));
    }

    [Fact]
    public void Query_WhenTheFilterIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _query.Query(Array.Empty<GroceryItemDto>(), null!));
    }

    #endregion
}