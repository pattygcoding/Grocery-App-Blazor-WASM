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
    /// Clears the browser stored catalogue and restores the demo data that ships with the app.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ResetDemoDataAsync();
}
