namespace GroceryApp.Data.State;

/// <summary>
/// Stores the identifiers of the items the visitor opened most recently, newest first.
/// </summary>
public sealed class RecentlyViewedStore : JsonAppStateStore<List<int>>
{
    /// <summary>
    /// Initializes a new instance of the RecentlyViewedStore class.
    /// </summary>
    /// <param name="store">The raw keyed store to read and write through.</param>
    public RecentlyViewedStore(IAppStateStore store)
        : base(store, AppStateKeys.RecentlyViewed, AppStateJsonContext.Default.ListInt32)
    {
    }
}
