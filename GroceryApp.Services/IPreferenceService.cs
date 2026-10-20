using GroceryApp.Models;

namespace GroceryApp.Services;

/// <summary>
/// Reads and writes the visitor's interface preferences.
/// </summary>
/// <remarks>
/// Preferences are read through a cached copy rather than from browser storage on every render, and a
/// change is announced, so a control that changes how the catalogue looks does not have to know which
/// pages happen to be interested.
/// </remarks>
public interface IPreferenceService
{
    /// <summary>Gets the preferences in force, which are the stored ones or the defaults.</summary>
    UserPreferences Current { get; }

    /// <summary>True once the stored preferences have been read.</summary>
    bool IsLoaded { get; }

    /// <summary>Raised after the preferences have been changed or read for the first time.</summary>
    event Action? Changed;

    /// <summary>
    /// Reads the stored preferences into <see cref="Current"/>.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the preferences in force.</returns>
    /// <remarks>
    /// Safe to call more than once: the second call hands back the cached copy rather than reading again.
    /// </remarks>
    Task<UserPreferences> LoadAsync();

    /// <summary>
    /// Stores a set of preferences and makes them the ones in force.
    /// </summary>
    /// <param name="preferences">The preferences to store.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveAsync(UserPreferences preferences);

    /// <summary>
    /// Puts every preference back to its default, without writing the defaults away.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the restored defaults.</returns>
    Task<UserPreferences> ResetAsync();
}
