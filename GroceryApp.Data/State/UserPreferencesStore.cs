using GroceryApp.Models;

namespace GroceryApp.Data.State;

/// <summary>
/// Stores the visitor's interface preferences.
/// </summary>
/// <remarks>
/// Held under its own key, separate from the catalogue, so restoring the demo data never resets how
/// the application looks or behaves.
/// </remarks>
public sealed class UserPreferencesStore : JsonAppStateStore<UserPreferences>
{
    /// <summary>
    /// Initializes a new instance of the UserPreferencesStore class.
    /// </summary>
    /// <param name="store">The raw keyed store to read and write through.</param>
    public UserPreferencesStore(IAppStateStore store)
        : base(store, AppStateKeys.Preferences, AppStateJsonContext.Default.UserPreferences)
    {
    }
}
