using System.Text.Json;
using GroceryApp.Models;

namespace GroceryApp.Data;

/// <summary>
/// Base class for browser storage implementations that persist the catalogue as JSON.
/// Derived stores only have to deal with raw string input/output (for example
/// localStorage through JS interop, or an in-memory variable in unit tests).
/// </summary>
public abstract class JsonGroceryStore : IGroceryStore
{
    /// <summary>
    /// Reads the persisted catalogue.
    /// </summary>
    public async Task<IReadOnlyList<GroceryItem>> LoadAsync()
    {
        var json = await ReadRawAsync();

        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<GroceryItem>();
        }

        try
        {
            return JsonSerializer.Deserialize(json, GroceryJsonContext.Default.ListGroceryItem)
                   ?? new List<GroceryItem>();
        }
        catch (JsonException)
        {
            // Browser storage can hold a payload written by an older build, or simply be corrupted.
            // Treat it as "nothing stored" so the repository seeds a fresh catalogue and rewrites
            // the payload, instead of failing every page that touches the data.
            return Array.Empty<GroceryItem>();
        }
    }

    /// <summary>
    /// Persists the complete catalogue as JSON.
    /// </summary>
    public Task SaveAsync(IEnumerable<GroceryItem> items)
    {
        var json = JsonSerializer.Serialize(items.ToList(), GroceryJsonContext.Default.ListGroceryItem);
        return WriteRawAsync(json);
    }

    /// <summary>
    /// Removes the persisted catalogue.
    /// </summary>
    public Task ClearAsync() => RemoveRawAsync();

    /// <summary>
    /// Reads the raw JSON payload from the underlying storage.
    /// </summary>
    /// <returns>The stored payload, or null when nothing has been stored.</returns>
    protected abstract Task<string?> ReadRawAsync();

    /// <summary>
    /// Writes the raw JSON payload to the underlying storage.
    /// </summary>
    /// <param name="value">The JSON payload to store.</param>
    protected abstract Task WriteRawAsync(string value);

    /// <summary>
    /// Removes the payload from the underlying storage.
    /// </summary>
    protected abstract Task RemoveRawAsync();
}
