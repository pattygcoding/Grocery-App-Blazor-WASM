using GroceryApp.Models;

namespace GroceryApp.Data;

/// <summary>
/// Defines the contract for persisting the grocery catalogue inside the browser.
/// </summary>
/// <remarks>
/// Implementations of this interface write the whole catalogue to browser storage (localStorage)
/// or, as a fallback, to memory.
/// </remarks>
public interface IGroceryStore
{
    /// <summary>
    /// Reads the persisted catalogue.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the persisted items, or an empty collection when nothing has been stored yet.</returns>
    Task<IReadOnlyList<GroceryItem>> LoadAsync();

    /// <summary>
    /// Persists the complete catalogue, replacing any previously stored payload.
    /// </summary>
    /// <param name="items">The items to persist.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveAsync(IEnumerable<GroceryItem> items);

    /// <summary>
    /// Removes the persisted catalogue so the next read returns an empty collection.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ClearAsync();

    /// <summary>
    /// Measures the persisted catalogue.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the payload length in characters, or 0 when nothing is stored.</returns>
    /// <remarks>
    /// The catalogue is a single document written whole, so its size is the size of that document. Diagnostics
    /// asks for this because the catalogue is usually the largest thing the application has stored, and a
    /// storage report that left out the biggest entry would be worse than no report.
    /// </remarks>
    Task<int> SizeAsync();
}
