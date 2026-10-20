namespace GroceryApp.Data;

/// <summary>
/// Browser storage keys this application owns.
/// </summary>
/// <remarks>
/// Every key is namespaced with <see cref="Prefix"/> so the app can enumerate and clear only its
/// own records: the published demo shares an origin with the rest of the house sites.
/// </remarks>
public static class AppStateKeys
{
    /// <summary>Prefix shared by every key the application writes.</summary>
    public const string Prefix = "groceryapp.";

    /// <summary>The catalogue itself, owned by <see cref="IGroceryStore"/>.</summary>
    public const string Catalogue = "groceryapp.catalogue.v1";

    /// <summary>Resolved theme preference, owned by the pre-paint script in index.html.</summary>
    public const string Theme = "groceryapp.theme";

    /// <summary>The activity log: an append-only history of catalogue mutations.</summary>
    public const string Activity = "groceryapp.activity.v1";

    /// <summary>Interface preferences that survive a demo data reset.</summary>
    public const string Preferences = "groceryapp.prefs.v1";

    /// <summary>Named sets of catalogue filters.</summary>
    public const string SavedViews = "groceryapp.views.v1";

    /// <summary>Identifiers of the items the visitor starred.</summary>
    public const string Favourites = "groceryapp.favourites.v1";

    /// <summary>Identifiers of the items the visitor opened most recently, newest first.</summary>
    public const string RecentlyViewed = "groceryapp.recent.v1";

    /// <summary>The managed list of suppliers the catalogue is bought from.</summary>
    public const string Suppliers = "groceryapp.suppliers.v1";
}
