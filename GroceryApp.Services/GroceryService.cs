using GroceryApp.Data.Repositories;
using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

public class GroceryService : IGroceryService
{
    private readonly IGroceryRepository _repository;

    public GroceryService(IGroceryRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<GroceryItemDto>> GetAllGroceriesAsync()
    {
        var groceries = await _repository.GetAllAsync();
        return groceries.Select(MapToDto);
    }

    public async Task<GroceryItemDto?> GetGroceryByIdAsync(int id)
    {
        var grocery = await _repository.GetByIdAsync(id);
        return grocery == null ? null : MapToDto(grocery);
    }

    public async Task<GroceryItemDto> CreateGroceryAsync(CreateGroceryItemDto createDto)
    {
        var grocery = new GroceryItem
        {
            Name = createDto.Name,
            Category = createDto.Category,
            Price = createDto.Price,
            CostToProduce = createDto.CostToProduce,
            Stock = createDto.Stock
        };

        var createdGrocery = await _repository.CreateAsync(grocery);
        return MapToDto(createdGrocery);
    }

    public async Task<GroceryItemDto?> UpdateGroceryAsync(int id, UpdateGroceryItemDto updateDto)
    {
        var grocery = new GroceryItem
        {
            Name = updateDto.Name,
            Category = updateDto.Category,
            Price = updateDto.Price,
            CostToProduce = updateDto.CostToProduce,
            Stock = updateDto.Stock
        };

        var updatedGrocery = await _repository.UpdateAsync(id, grocery);
        return updatedGrocery == null ? null : MapToDto(updatedGrocery);
    }

    public async Task<bool> DeleteGroceryAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }

    public async Task<bool> GroceryExistsAsync(int id)
    {
        return await _repository.ExistsAsync(id);
    }

    public async Task<GroceryItemDto?> AdjustStockAsync(int id, int adjustment)
    {
        var grocery = await _repository.GetByIdAsync(id);
        if (grocery == null) return null;

        var newStock = Math.Max(0, grocery.Stock + adjustment);
        grocery.Stock = newStock;

        var updatedGrocery = await _repository.UpdateAsync(id, grocery);
        return updatedGrocery == null ? null : MapToDto(updatedGrocery);
    }

    public async Task ResetDemoDataAsync()
    {
        await _repository.ResetAsync();
    }

    private static GroceryItemDto MapToDto(GroceryItem grocery)
    {
        return new GroceryItemDto
        {
            Id = grocery.Id,
            Name = grocery.Name,
            Category = grocery.Category,
            Price = grocery.Price,
            CostToProduce = grocery.CostToProduce,
            Stock = grocery.Stock,
            CreatedUtc = grocery.CreatedUtc
        };
    }
}