using GroceryApp.Models;

namespace GroceryApp.Data.Repositories;

/// <summary>
/// Defines the contract for grocery item data access operations.
/// Provides methods for CRUD operations and existence checking of grocery items in the data store.
/// </summary>
/// <remarks>
/// In this front-end only Blazor WebAssembly build the "data store" is browser storage, so every
/// operation is asynchronous and runs inside the visitor's browser.
/// </remarks>
public interface IGroceryRepository
{
    /// <summary>
    /// Retrieves all grocery items from the data store.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of grocery items.</returns>
    /// <remarks>
    /// Returns all grocery items without any filtering. For large datasets, consider implementing pagination.
    /// </remarks>
    Task<IEnumerable<GroceryItem>> GetAllAsync();

    /// <summary>
    /// Retrieves a specific grocery item by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the grocery item to retrieve.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the grocery item if found, otherwise null.</returns>
    /// <remarks>
    /// Returns null if no grocery item with the specified ID exists in the data store.
    /// </remarks>
    Task<GroceryItem?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new grocery item in the data store.
    /// </summary>
    /// <param name="item">The grocery item to create. The ID property will be ignored as it will be auto-generated.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created grocery item with its assigned ID.</returns>
    /// <remarks>
    /// The created item will have its ID property populated with the auto-generated identifier from the data store.
    /// All required fields should be populated before calling this method.
    /// </remarks>
    Task<GroceryItem> CreateAsync(GroceryItem item);

    /// <summary>
    /// Updates an existing grocery item in the data store.
    /// </summary>
    /// <param name="id">The unique identifier of the grocery item to update.</param>
    /// <param name="item">The updated grocery item data. The ID property should match the id parameter.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the updated grocery item if successful, otherwise null.</returns>
    /// <remarks>
    /// Returns null if no grocery item with the specified ID exists in the data store.
    /// All fields of the item will be updated including CostToProduce, Price, and Stock.
    /// </remarks>
    Task<GroceryItem?> UpdateAsync(int id, GroceryItem item);

    /// <summary>
    /// Deletes a grocery item from the data store.
    /// </summary>
    /// <param name="id">The unique identifier of the grocery item to delete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the item was successfully deleted, otherwise false.</returns>
    /// <remarks>
    /// Returns false if no grocery item with the specified ID exists in the data store.
    /// This operation is permanent and cannot be undone.
    /// </remarks>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Checks whether a grocery item with the specified identifier exists in the data store.
    /// </summary>
    /// <param name="id">The unique identifier of the grocery item to check for existence.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the item exists, otherwise false.</returns>
    /// <remarks>
    /// This is a lightweight operation that only checks for existence without retrieving the full item data.
    /// Useful for validation scenarios before performing operations that require an existing item.
    /// </remarks>
    Task<bool> ExistsAsync(int id);

    /// <summary>
    /// Clears the persisted catalogue and restores the original seeded demo data.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// Used by the hosted demo so visitors can put the application back to its initial state
    /// after adding, editing, or deleting items.
    /// </remarks>
    Task ResetAsync();
}
