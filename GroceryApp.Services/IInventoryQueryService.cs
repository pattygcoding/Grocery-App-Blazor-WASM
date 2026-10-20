using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Narrows, orders and summarises the catalogue list.
/// </summary>
/// <remarks>
/// Kept apart from the storage layer so a filter can be run over the catalogue held in the browser, over
/// an imported file or over a saved view without any of them knowing about the others.
/// </remarks>
public interface IInventoryQueryService
{
    /// <summary>
    /// Runs a filter over a catalogue.
    /// </summary>
    /// <param name="items">The catalogue to filter.</param>
    /// <param name="filter">What to keep and how to order it. An empty filter matches everything.</param>
    /// <param name="favouriteIds">Items the visitor has starred, used only when the filter asks for favourites.</param>
    /// <returns>The matching items plus the facets the filter bar offers and the totals behind them.</returns>
    InventoryQueryResult Query(
        IEnumerable<GroceryItemDto> items,
        InventoryFilter filter,
        IReadOnlyCollection<int>? favouriteIds = null);
}
