using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Groups the catalogue by who it is bought from.
/// </summary>
/// <remarks>
/// Items reference a supplier by name rather than by identifier, so this is a view over the catalogue rather
/// than a join against a table. That is the deliberate consequence of suppliers being renameable without
/// rewriting the items that mention them.
/// </remarks>
public interface ISupplierAnalyticsService
{
    /// <summary>
    /// Summarises the catalogue per supplier.
    /// </summary>
    /// <param name="items">The catalogue to group.</param>
    /// <returns>The suppliers, largest share of the value first.</returns>
    /// <remarks>
    /// Archived items are counted but left out of the value and ordering figures: an item taken out of the
    /// catalogue is not stock the visitor is holding, and counting it would make the page disagree with the
    /// dashboard.
    /// </remarks>
    SupplierAnalysis Summarise(IEnumerable<GroceryItemDto> items);
}