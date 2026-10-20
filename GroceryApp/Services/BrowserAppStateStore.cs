using GroceryApp.Data;
using Microsoft.JSInterop;

namespace GroceryApp.Services;

/// <summary>
/// Keeps the application's auxiliary state - the activity log, preferences, saved views and the
/// pinned item lists - in the browser's <c>localStorage</c>.
/// </summary>
/// <remarks>
/// All access goes through the <c>groceryStorage</c> module in wwwroot/storage.js, which reports
/// failures instead of throwing and only ever enumerates keys carrying the application's own prefix.
/// When storage is unavailable (for example private browsing modes that block localStorage) every
/// call falls back to an in-memory copy, so the app keeps working for the session and only loses
/// persistence across a reload. That mirrors how <see cref="BrowserLocalStorageStore"/> behaves for
/// the catalogue itself.
/// </remarks>
public sealed class BrowserAppStateStore : IAppStateStore
{
    private readonly IJSRuntime _jsRuntime;
    private readonly Dictionary<string, string> _memoryFallback = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the BrowserAppStateStore class.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime used to reach localStorage.</param>
    public BrowserAppStateStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public async Task<string?> ReadAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("groceryStorage.get", key);

            if (value is not null)
            {
                return value;
            }
        }
        catch (JSException)
        {
            // storage.js is unavailable (blocked storage, or a cached index.html from before this
            // feature). Fall through to the in-memory copy.
        }

        return _memoryFallback.TryGetValue(key, out var fallback) ? fallback : null;
    }

    /// <inheritdoc />
    public async Task WriteAsync(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        _memoryFallback[key] = value;

        try
        {
            await _jsRuntime.InvokeVoidAsync("groceryStorage.set", key, value);
        }
        catch (JSException)
        {
            // Keep the in-memory copy only.
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        _memoryFallback.Remove(key);

        try
        {
            await _jsRuntime.InvokeVoidAsync("groceryStorage.remove", key);
        }
        catch (JSException)
        {
            // Nothing to remove.
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> KeysAsync()
    {
        try
        {
            var keys = await _jsRuntime.InvokeAsync<string[]>("groceryStorage.keys");

            if (keys is not null)
            {
                return keys.OrderBy(key => key, StringComparer.Ordinal).ToList();
            }
        }
        catch (JSException)
        {
            // Fall through to the in-memory keys.
        }

        return _memoryFallback.Keys
            .Where(key => key.StartsWith(AppStateKeys.Prefix, StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
    }
}
