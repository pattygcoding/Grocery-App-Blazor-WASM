namespace GroceryApp.Data;

/// <summary>
/// Defines the contract for persisting the small pieces of application state that sit next to the
/// catalogue: the activity log, interface preferences, saved views and the pinned item lists.
/// </summary>
/// <remarks>
/// The catalogue has its own contract (<see cref="IGroceryStore"/>) because it is a single document
/// that is always rewritten as a whole. Everything else is a small, separately versioned payload
/// addressed by key, which is what this store deals with. Implementations write to browser storage
/// or, when browser storage is unavailable, to memory.
/// </remarks>
public interface IAppStateStore
{
    /// <summary>
    /// Reads the raw payload stored under a key.
    /// </summary>
    /// <param name="key">The storage key, normally one of the constants on <see cref="AppStateKeys"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the stored payload, or null when the key is not set.</returns>
    Task<string?> ReadAsync(string key);

    /// <summary>
    /// Writes a raw payload under a key, replacing any previous value.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <param name="value">The payload to store.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task WriteAsync(string key, string value);

    /// <summary>
    /// Removes a key.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemoveAsync(string key);

    /// <summary>
    /// Lists the keys this application owns, so diagnostics can report what is stored and the
    /// demo reset can clear it.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the keys carrying the <see cref="AppStateKeys.Prefix"/> prefix, in ordinal order.</returns>
    Task<IReadOnlyList<string>> KeysAsync();
}
