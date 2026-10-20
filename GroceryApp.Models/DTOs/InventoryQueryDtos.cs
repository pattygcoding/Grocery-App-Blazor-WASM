namespace GroceryApp.Models.DTOs;

/// <summary>Column the catalogue list is ordered by.</summary>
public enum InventorySortField
{
    Name,
    Sku,
    Category,
    Supplier,
    Price,
    Cost,
    Margin,
    MarginPercent,
    Stock,
    StockValue,
    ReorderPoint,
    AbcClass,
    Created
}

/// <summary>Direction of the catalogue ordering.</summary>
public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>Which stock positions the catalogue list is limited to.</summary>
public enum StockFilter
{
    /// <summary>No stock filtering.</summary>
    Any,

    OutOfStock,

    /// <summary>Out of stock or down to the last quarter of the threshold.</summary>
    CriticalOrWorse,

    /// <summary>Anything at or below its reorder point, and anything worse.</summary>
    AtOrBelowReorderPoint,

    Healthy,

    Overstock
}

/// <summary>
/// Everything the catalogue list can be narrowed and ordered by.
/// </summary>
/// <remarks>
/// One object rather than a set of parameters, because the same filter is built from the query string,
/// from a saved view and from the filter bar, and it is passed to the analysis and the export as-is.
/// An empty filter matches everything.
/// </remarks>
public class InventoryFilter
{
    /// <summary>Free text matched against the name, SKU, supplier, notes and tags.</summary>
    public string? SearchText { get; set; }

    public List<Category> Categories { get; set; } = new();

    public List<string> Suppliers { get; set; } = new();

    public List<string> Tags { get; set; } = new();

    public decimal? MinimumPrice { get; set; }

    public decimal? MaximumPrice { get; set; }

    /// <summary>Only items whose margin is below this percentage.</summary>
    public decimal? MaximumMarginPercent { get; set; }

    public StockFilter Stock { get; set; } = StockFilter.Any;

    /// <summary>Only items in this ABC band.</summary>
    public AbcClass? AbcClass { get; set; }

    /// <summary>Whether archived items are included.</summary>
    public bool IncludeArchived { get; set; }

    /// <summary>Only items the visitor has starred.</summary>
    public bool FavouritesOnly { get; set; }

    public InventorySortField SortBy { get; set; } = InventorySortField.Name;

    public SortDirection Direction { get; set; } = SortDirection.Ascending;

    /// <summary>
    /// True when the filter narrows the catalogue rather than just ordering it. The list page uses this
    /// to decide whether to offer a reset.
    /// </summary>
    public bool IsNarrowing =>
        !string.IsNullOrWhiteSpace(SearchText) ||
        Categories.Count > 0 ||
        Suppliers.Count > 0 ||
        Tags.Count > 0 ||
        MinimumPrice.HasValue ||
        MaximumPrice.HasValue ||
        MaximumMarginPercent.HasValue ||
        Stock != StockFilter.Any ||
        AbcClass.HasValue ||
        IncludeArchived ||
        FavouritesOnly;
}

/// <summary>
/// The outcome of running a filter over the catalogue, including the facets the filter bar offers.
/// </summary>
public class InventoryQueryResult
{
    /// <summary>The items that survived the filter, in the requested order.</summary>
    public List<GroceryItemDto> Items { get; set; } = new();

    /// <summary>How many items existed before filtering, including archived ones.</summary>
    public int TotalBeforeFiltering { get; set; }

    /// <summary>How many archived items the filter left out.</summary>
    public int ArchivedExcluded { get; set; }

    /// <summary>Total retail value of the items that survived the filter.</summary>
    public decimal FilteredRetailValue { get; set; }

    /// <summary>Suppliers present in the unfiltered catalogue, so the filter bar can offer them.</summary>
    public List<string> Suppliers { get; set; } = new();

    /// <summary>Tags present in the unfiltered catalogue.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Price the cheapest item in the unfiltered catalogue sells for, for the price range control.</summary>
    public decimal LowestPrice { get; set; }

    /// <summary>Price the dearest item in the unfiltered catalogue sells for.</summary>
    public decimal HighestPrice { get; set; }

    /// <summary>
    /// The ABC band each item falls into, so a row can badge itself without re-running the analysis.
    /// </summary>
    public Dictionary<int, AbcClass> AbcBands { get; set; } = new();
}