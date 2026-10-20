namespace GroceryApp.Data.State;

/// <summary>
/// Stores the identifiers of the items the visitor starred.
/// </summary>
/// <remarks>
/// Identifiers rather than items: the catalogue remains the single source of truth for item data, so
/// a starred item never goes stale or duplicates the catalogue payload.
/// </remarks>
public sealed class FavouriteItemStore : JsonAppStateStore<List<int>>
{
    /// <summary>
    /// Initializes a new instance of the FavouriteItemStore class.
    /// </summary>
    /// <param name="store">The raw keyed store to read and write through.</param>
    public FavouriteItemStore(IAppStateStore store)
        : base(store, AppStateKeys.Favourites, AppStateJsonContext.Default.ListInt32)
    {
    }
}
