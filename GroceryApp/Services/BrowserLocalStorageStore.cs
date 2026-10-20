using GroceryApp.Data;
using Microsoft.JSInterop;

namespace GroceryApp.Services;

/// <summary>
/// Persists the grocery catalogue in the browser's <c>localStorage</c>.
/// </summary>
/// <remarks>
/// The data lives in the visitor's browser and survives page reloads (but not a cleared browser
/// storage). When storage is unavailable (for example private browsing modes that block
/// localStorage) the store transparently falls back to an in-memory payload so the app keeps
/// working.
/// </remarks>
public sealed class BrowserLocalStorageStore : JsonGroceryStore
{
    /// <summary>
    /// localStorage key holding the JSON catalogue. Sourced from <see cref="AppStateKeys"/> so every
    /// key the application owns is declared in one place.
    /// </summary>
    public const string StorageKey = AppStateKeys.Catalogue;

    private readonly IJSRuntime _jsRuntime;
    private string? _memoryFallback;

    /// <summary>
    /// Initializes a new instance of the BrowserLocalStorageStore class.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime used to reach localStorage.</param>
    public BrowserLocalStorageStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    protected override async Task<string?> ReadRawAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        }
        catch (JSException)
        {
            // localStorage can be blocked by browser privacy settings.
            return _memoryFallback;
        }
    }

    /// <inheritdoc />
    protected override async Task WriteRawAsync(string value)
    {
        _memoryFallback = value;

        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, value);
        }
        catch (JSException)
        {
            // Keep the in-memory copy only.
        }
    }

    /// <inheritdoc />
    protected override async Task RemoveRawAsync()
    {
        _memoryFallback = null;

        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch (JSException)
        {
            // Nothing to remove.
        }
    }
}
