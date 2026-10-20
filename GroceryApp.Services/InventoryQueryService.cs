using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Filters, orders and summarises the catalogue for the list page, the restock worksheet and the
/// exported report.
/// </summary>
/// <remarks>
/// The ABC band an item falls into is taken from <see cref="IInventoryAnalyticsService"/> rather than
/// worked out again here, so an item cannot be in band A on the dashboard and band C in the filter.
/// </remarks>
public class InventoryQueryService : IInventoryQueryService
{
    private readonly IInventoryAnalyticsService _analytics;

    /// <summary>
    /// Initializes a new instance of the InventoryQueryService class.
    /// </summary>
    /// <param name="analytics">The analysis used to band items and judge their stock position.</param>
    public InventoryQueryService(IInventoryAnalyticsService analytics)
    {
        _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
    }

    /// <inheritdoc />
    public InventoryQueryResult Query(
        IEnumerable<GroceryItemDto> items,
        InventoryFilter filter,
        IReadOnlyCollection<int>? favouriteIds = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(filter);

        var catalogue = items.ToList();
        var abcBands = BuildAbcLookup(_analytics.AnalyseAbc(catalogue));

        var result = new InventoryQueryResult
        {
            TotalBeforeFiltering = catalogue.Count,
            ArchivedExcluded = catalogue.Count(item => item.IsArchived),
            Suppliers = catalogue
                .Select(item => item.Supplier)
                .Where(supplier => !string.IsNullOrWhiteSpace(supplier))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(supplier => supplier, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Tags = catalogue
                .SelectMany(item => item.Tags)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        if (catalogue.Count > 0)
        {
            result.LowestPrice = catalogue.Min(item => item.Price);
            result.HighestPrice = catalogue.Max(item => item.Price);
        }

        var matching = catalogue
            .Where(item => Matches(item, filter, abcBands, favouriteIds))
            .ToList();

        result.Items = Order(matching, filter, abcBands).ToList();
        result.FilteredRetailValue = result.Items.Sum(item => item.StockRetailValue);
        result.AbcBands = abcBands;

        return result;
    }

    /// <summary>
    /// Maps every item to the ABC band it belongs to, so the filter can ask for a band without
    /// re-running the analysis per item.
    /// </summary>
    private static Dictionary<int, AbcClass> BuildAbcLookup(AbcAnalysis analysis)
    {
        var lookup = new Dictionary<int, AbcClass>();

        foreach (var band in analysis.Classes)
        {
            foreach (var item in band.Items)
            {
                lookup[item.Id] = band.Class;
            }
        }

        return lookup;
    }

    /// <summary>
    /// Decides whether one item survives the filter. Every clause has to pass: the clauses combine with
    /// "and" so that narrowing one control can never widen the result in another.
    /// </summary>
    private bool Matches(
        GroceryItemDto item,
        InventoryFilter filter,
        IReadOnlyDictionary<int, AbcClass> abcBands,
        IReadOnlyCollection<int>? favouriteIds)
    {
        if (item.IsArchived && !filter.IncludeArchived)
        {
            return false;
        }

        if (filter.FavouritesOnly && (favouriteIds is null || !favouriteIds.Contains(item.Id)))
        {
            return false;
        }

        if (filter.Categories.Count > 0 && !filter.Categories.Contains(item.Category))
        {
            return false;
        }

        if (filter.Suppliers.Count > 0 &&
            !filter.Suppliers.Contains(item.Supplier, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // Tag matching is "any of", because tags are additive labels rather than mutually exclusive
        // buckets: asking for "chilled" and "bulk" should not require an item to carry both.
        if (filter.Tags.Count > 0 &&
            !filter.Tags.Any(tag => item.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (filter.MinimumPrice.HasValue && item.Price < filter.MinimumPrice.Value)
        {
            return false;
        }

        if (filter.MaximumPrice.HasValue && item.Price > filter.MaximumPrice.Value)
        {
            return false;
        }

        // The margin control is a "show me the weakest lines" control, so it filters from above.
        if (filter.MaximumMarginPercent.HasValue && item.MarginPercent > filter.MaximumMarginPercent.Value)
        {
            return false;
        }

        if (filter.AbcClass.HasValue &&
            abcBands.GetValueOrDefault(item.Id, AbcClass.C) != filter.AbcClass.Value)
        {
            return false;
        }

        return MatchesStock(item, filter.Stock) && MatchesSearch(item, filter.SearchText);
    }

    /// <summary>
    /// Maps the stock filter onto the item's stock status.
    /// </summary>
    /// <remarks>
    /// The "or worse" options exist so a visitor can ask for everything that needs attention in one go,
    /// instead of ticking three boxes and hoping they thought of all of them.
    /// </remarks>
    private bool MatchesStock(GroceryItemDto item, StockFilter filter)
    {
        var status = _analytics.Classify(item);

        return filter switch
        {
            StockFilter.Any => true,
            StockFilter.OutOfStock => status == StockStatus.OutOfStock,
            StockFilter.CriticalOrWorse => status is StockStatus.OutOfStock or StockStatus.Critical,
            StockFilter.AtOrBelowReorderPoint =>
                status is StockStatus.OutOfStock or StockStatus.Critical or StockStatus.Low,
            StockFilter.Healthy => status == StockStatus.Healthy,
            StockFilter.Overstock => status == StockStatus.Overstock,
            _ => true
        };
    }

    /// <summary>
    /// Matches free text against the fields a person would search by.
    /// </summary>
    /// <remarks>
    /// Multiple words all have to match, but each may match a different field. "cheese bulk" finds the
    /// bulk cheese rather than nothing, which is what makes the search box worth using.
    /// </remarks>
    private static bool MatchesSearch(GroceryItemDto item, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        var terms = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return terms.All(term => MatchesTerm(item, term));
    }

    private static bool MatchesTerm(GroceryItemDto item, string term) =>
        item.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        item.Sku.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        item.Supplier.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        item.Notes.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        item.Category.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
        item.Tags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Orders the matching items.
    /// </summary>
    /// <remarks>
    /// Descending is the reverse of the ascending order rather than a second set of comparators, so the
    /// two directions can never disagree about what a column means. That reverses the tie-breaker too,
    /// which is the price of having one definition of each ordering rather than two.
    /// </remarks>
    private static IEnumerable<GroceryItemDto> Order(
        IEnumerable<GroceryItemDto> items,
        InventoryFilter filter,
        IReadOnlyDictionary<int, AbcClass> abcBands)
    {
        var ordered = filter.SortBy switch
        {
            InventorySortField.Sku => items.OrderBy(item => item.Sku, StringComparer.OrdinalIgnoreCase),
            InventorySortField.Category => items
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            InventorySortField.Supplier => items
                .OrderBy(item => item.Supplier, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            InventorySortField.Price => items.OrderBy(item => item.Price),
            InventorySortField.Cost => items.OrderBy(item => item.CostToProduce),
            InventorySortField.Margin => items.OrderBy(item => item.Margin),
            InventorySortField.MarginPercent => items.OrderBy(item => item.MarginPercent),
            InventorySortField.Stock => items.OrderBy(item => item.Stock),
            InventorySortField.StockValue => items.OrderBy(item => item.StockRetailValue),
            InventorySortField.ReorderPoint => items.OrderBy(item => item.ReorderPoint),
            InventorySortField.AbcClass => items
                .OrderBy(item => abcBands.GetValueOrDefault(item.Id, AbcClass.C))
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            InventorySortField.Created => items.OrderBy(item => item.CreatedUtc),
            _ => items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        };

        return filter.Direction == SortDirection.Descending
            ? ordered.Reverse()
            : ordered;
    }
}