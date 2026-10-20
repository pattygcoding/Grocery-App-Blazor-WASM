using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

public interface IGroceryService
{
    /// <summary>
    /// Retrieves all grocery items from the database ordered by category and name.
    /// </summary>
    /// <returns>A collection of all grocery items as DTOs.</returns>
    Task<IEnumerable<GroceryItemDto>> GetAllGroceriesAsync();

    /// <summary>
    /// Retrieves a specific grocery item by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the grocery item.</param>
    /// <returns>The grocery item DTO if found, null otherwise.</returns>
    Task<GroceryItemDto?> GetGroceryByIdAsync(int id);

    /// <summary>
    /// Creates a new grocery item in the database.
    /// </summary>
    /// <param name="createDto">The data transfer object containing the grocery item information to create.</param>
    /// <returns>The created grocery item as a DTO.</returns>
    Task<GroceryItemDto> CreateGroceryAsync(CreateGroceryItemDto createDto);

    /// <summary>
    /// Updates an existing grocery item with new information.
    /// </summary>
    /// <param name="id">The unique identifier of the grocery item to update.</param>
    /// <param name="updateDto">The data transfer object containing the updated grocery item information.</param>
    /// <returns>The updated grocery item DTO if successful, null if the item was not found.</returns>
    Task<GroceryItemDto?> UpdateGroceryAsync(int id, UpdateGroceryItemDto updateDto);

    /// <summary>
    /// Deletes a grocery item from the database.
    /// </summary>
    /// <param name="id">The unique identifier of the grocery item to delete.</param>
    /// <returns>True if the item was deleted successfully, false if the item was not found.</returns>
    Task<bool> DeleteGroceryAsync(int id);

    /// <summary>
    /// Checks if a grocery item exists in the database.
    /// </summary>
    /// <param name="id">The unique identifier of the grocery item to check.</param>
    /// <returns>True if the item exists, false otherwise.</returns>
    Task<bool> GroceryExistsAsync(int id);

    /// <summary>
    /// Adjusts the stock level of a grocery item by a specified amount.
    /// </summary>
    /// <param name="id">The unique identifier of the grocery item.</param>
    /// <param name="adjustment">The amount to adjust the stock by (can be positive or negative).</param>
    /// <returns>The updated grocery item DTO if successful, null if the item was not found or if the adjustment would result in negative stock.</returns>
    Task<GroceryItemDto?> AdjustStockAsync(int id, int adjustment);

    /// <summary>
    /// Adds stock to several items in one operation, recording each line in the activity log.
    /// </summary>
    /// <param name="quantities">Units to add, keyed by item identifier. Zero and negative quantities are ignored.</param>
    /// <returns>The items that were restocked.</returns>
    /// <remarks>
    /// A separate method rather than a loop over <see cref="AdjustStockAsync"/>, because a restock is one
    /// change to the catalogue: it is written once, logged as one batch, and can be undone in one step.
    /// </remarks>
    Task<IReadOnlyList<GroceryItemDto>> ApplyRestockAsync(IReadOnlyDictionary<int, int> quantities);

    /// <summary>
    /// Archives or restores one item.
    /// </summary>
    /// <param name="id">The unique identifier of the grocery item.</param>
    /// <param name="archived">True to archive the item, false to put it back in the catalogue.</param>
    /// <returns>The updated grocery item DTO, or null if the item was not found.</returns>
    /// <remarks>
    /// Archiving is a change to the catalogue like any other, so it is logged and can be undone. It is not
    /// a delete: the item keeps its history, its code and its position in the value held figures unless the
    /// catalogue is asked for archived items.
    /// </remarks>
    Task<GroceryItemDto?> SetArchivedAsync(int id, bool archived);

    /// <summary>
    /// Archives or restores several items as one change.
    /// </summary>
    /// <param name="ids">The identifiers to change. Unknown identifiers are ignored.</param>
    /// <param name="archived">True to archive, false to restore.</param>
    /// <returns>The items that actually changed.</returns>
    /// <remarks>
    /// One write and one history step for the batch, so archiving twenty rows from the list is one undo
    /// rather than twenty. Identifiers already in the requested state are left alone rather than logged
    /// again.
    /// </remarks>
    Task<IReadOnlyList<GroceryItemDto>> SetArchivedAsync(IReadOnlyCollection<int> ids, bool archived);

    /// <summary>
    /// Deletes several items as one change.
    /// </summary>
    /// <param name="ids">The identifiers to remove. Unknown identifiers are ignored.</param>
    /// <returns>A task whose result is how many items were removed.</returns>
    /// <remarks>
    /// One write and one history step, so removing a selection from the list is one undo rather than one per
    /// row. Names are read before the removal so the log can still say what went.
    /// </remarks>
    Task<int> DeleteManyAsync(IReadOnlyCollection<int> ids);

    /// <summary>
    /// Clears the browser stored catalogue and restores the demo data that ships with the app.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ResetDemoDataAsync();
}
