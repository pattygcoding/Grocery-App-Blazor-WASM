using GroceryApp.Models;

namespace GroceryApp.Data.State;

/// <summary>
/// Stores the catalogue filters the visitor saved as named views.
/// </summary>
public sealed class SavedViewStore : JsonAppStateStore<List<SavedView>>
{
    /// <summary>
    /// Initializes a new instance of the SavedViewStore class.
    /// </summary>
    /// <param name="store">The raw keyed store to read and write through.</param>
    public SavedViewStore(IAppStateStore store)
        : base(store, AppStateKeys.SavedViews, AppStateJsonContext.Default.ListSavedView)
    {
    }
}
