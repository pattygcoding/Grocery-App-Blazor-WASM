using GroceryApp.Data.State;
using GroceryApp.Models;

namespace GroceryApp.Services;

/// <inheritdoc />
public class PreferenceService : IPreferenceService
{
    private readonly UserPreferencesStore _store;

    /// <summary>
    /// Initializes a new instance of the PreferenceService class.
    /// </summary>
    /// <param name="store">The store the preferences are kept in.</param>
    public PreferenceService(UserPreferencesStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public UserPreferences Current { get; private set; } = new();

    /// <inheritdoc />
    public bool IsLoaded { get; private set; }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public async Task<UserPreferences> LoadAsync()
    {
        if (IsLoaded)
        {
            return Current;
        }

        // A store that has never been written, or one holding something unreadable, reports nothing rather
        // than throwing, so the defaults stand.
        Current = await _store.LoadAsync() ?? new UserPreferences();
        IsLoaded = true;
        Changed?.Invoke();

        return Current;
    }

    /// <inheritdoc />
    public async Task SaveAsync(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        Current = preferences;
        IsLoaded = true;

        await _store.SaveAsync(Current);
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public async Task<UserPreferences> ResetAsync()
    {
        // The stored payload is removed rather than a set of defaults being written into its place: nothing
        // stored means the defaults apply, which is the same state as a first visit.
        await _store.ClearAsync();

        Current = new UserPreferences();
        IsLoaded = true;
        Changed?.Invoke();

        return Current;
    }
}
