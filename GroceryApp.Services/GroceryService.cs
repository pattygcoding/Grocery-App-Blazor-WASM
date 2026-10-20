using GroceryApp.Data.Repositories;
using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

public class GroceryService : IGroceryService
{
    private readonly IGroceryRepository _repository;
    private readonly IActivityService _activity;
    private readonly ICatalogueHistoryService _history;

    public GroceryService(
        IGroceryRepository repository,
        IActivityService activity,
        ICatalogueHistoryService history)
    {
        _repository = repository;
        _activity = activity;
        _history = history;
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

    public Task<GroceryItemDto> CreateGroceryAsync(CreateGroceryItemDto createDto) =>
        // Every change to the catalogue is one undoable step, so the undo control in the top bar puts back
        // whatever was done last, whoever did it.
        _history.RunAsync($"Adding {createDto.Name}", () => CreateAsync(createDto));

    private async Task<GroceryItemDto> CreateAsync(CreateGroceryItemDto createDto)
    {
        var grocery = new GroceryItem
        {
            Name = createDto.Name,
            Sku = createDto.Sku,
            Category = createDto.Category,
            Supplier = createDto.Supplier,
            Unit = createDto.Unit,
            Price = createDto.Price,
            CostToProduce = createDto.CostToProduce,
            Stock = createDto.Stock,
            ReorderPoint = createDto.ReorderPoint,
            TargetMarginPercent = createDto.TargetMarginPercent,
            Tags = NormalizeTags(createDto.Tags),
            Notes = createDto.Notes
        };

        var createdGrocery = await _repository.CreateAsync(grocery);
        var created = MapToDto(createdGrocery);

        // Recorded after the write succeeds, so the log only ever describes changes that happened.
        await _activity.RecordItemAsync(
            ActivityKind.Created,
            created,
            created.Stock,
            "Added to the catalogue");

        return created;
    }

    public Task<GroceryItemDto?> UpdateGroceryAsync(int id, UpdateGroceryItemDto updateDto) =>
        _history.RunAsync($"Editing {updateDto.Name}", () => UpdateAsync(id, updateDto));

    private async Task<GroceryItemDto?> UpdateAsync(int id, UpdateGroceryItemDto updateDto)
    {
        var grocery = new GroceryItem
        {
            Name = updateDto.Name,
            Sku = updateDto.Sku,
            Category = updateDto.Category,
            Supplier = updateDto.Supplier,
            Unit = updateDto.Unit,
            Price = updateDto.Price,
            CostToProduce = updateDto.CostToProduce,
            Stock = updateDto.Stock,
            ReorderPoint = updateDto.ReorderPoint,
            TargetMarginPercent = updateDto.TargetMarginPercent,
            Tags = NormalizeTags(updateDto.Tags),
            Notes = updateDto.Notes
        };

        var updatedGrocery = await _repository.UpdateAsync(id, grocery);

        if (updatedGrocery is null)
        {
            return null;
        }

        var updated = MapToDto(updatedGrocery);

        await _activity.RecordItemAsync(
            ActivityKind.Updated,
            updated,
            0,
            $"Now priced at {updated.Price:C2} with {updated.Stock} in stock");

        return updated;
    }

    public async Task<bool> DeleteGroceryAsync(int id)
    {
        // Read before the step is named: once the item is gone there is nothing left to name it from.
        var item = await _repository.GetByIdAsync(id);

        return await _history.RunAsync(
            $"Deleting {item?.Name ?? $"item {id}"}",
            () => DeleteAsync(id, item));
    }

    private async Task<bool> DeleteAsync(int id, GroceryItem? item)
    {
        var deleted = await _repository.DeleteAsync(id);

        if (deleted && item is not null)
        {
            await _activity.RecordItemAsync(
                ActivityKind.Deleted,
                MapToDto(item),
                0,
                "Removed from the catalogue");
        }

        return deleted;
    }

    public async Task<bool> GroceryExistsAsync(int id)
    {
        return await _repository.ExistsAsync(id);
    }

    public async Task<GroceryItemDto?> AdjustStockAsync(int id, int adjustment)
    {
        var grocery = await _repository.GetByIdAsync(id);

        if (grocery is null)
        {
            // Nothing to adjust and nothing to record: a missing item is not a step.
            return null;
        }

        if (adjustment == 0)
        {
            // An adjustment of zero would put a step in the history that does not change anything.
            return MapToDto(grocery);
        }

        return await _history.RunAsync($"Adjusting {grocery.Name}", () => AdjustAsync(grocery, adjustment));
    }

    private async Task<GroceryItemDto?> AdjustAsync(GroceryItem grocery, int adjustment)
    {
        var previousStock = grocery.Stock;
        grocery.Stock = Math.Max(0, previousStock + adjustment);

        var updatedGrocery = await _repository.UpdateAsync(grocery.Id, grocery);

        if (updatedGrocery is null)
        {
            return null;
        }

        var updated = MapToDto(updatedGrocery);

        // The change that was applied is logged rather than the one that was asked for: clamping at zero
        // means the two differ whenever an adjustment asks for more stock than there is.
        await _activity.RecordItemAsync(
            ActivityKind.StockAdjusted,
            updated,
            updated.Stock - previousStock,
            "Adjusted from the catalogue");

        return updated;
    }

    public Task<IReadOnlyList<GroceryItemDto>> ApplyRestockAsync(IReadOnlyDictionary<int, int> quantities)
    {
        ArgumentNullException.ThrowIfNull(quantities);

        // The label counts what will actually be ordered, which is not the same as the number of lines handed
        // in: zero and negative quantities are dropped below.
        var lines = quantities.Count(entry => entry.Value > 0);

        if (lines == 0)
        {
            // Nothing to order, so the catalogue is not read, not written, and no empty step is left in the
            // history for an undo to put back.
            return Task.FromResult<IReadOnlyList<GroceryItemDto>>(Array.Empty<GroceryItemDto>());
        }

        return _history.RunAsync($"Restocking {lines} lines", () => RestockAsync(quantities));
    }

    private async Task<IReadOnlyList<GroceryItemDto>> RestockAsync(IReadOnlyDictionary<int, int> quantities)
    {
        if (quantities.Count == 0)
        {
            return Array.Empty<GroceryItemDto>();
        }

        var items = (await _repository.GetAllAsync()).ToList();
        var restocked = new List<GroceryItemDto>();
        var now = DateTime.UtcNow;

        foreach (var (itemId, quantity) in quantities)
        {
            if (quantity <= 0)
            {
                continue;
            }

            var item = items.FirstOrDefault(candidate => candidate.Id == itemId);

            if (item is null)
            {
                continue;
            }

            item.Stock += quantity;

            // The restock timestamp is what the velocity figures measure cover from, and an ordinary edit
            // deliberately leaves it alone, so it is set here rather than in the repository.
            item.LastRestockedUtc = now;

            restocked.Add(MapToDto(item));
        }

        if (restocked.Count == 0)
        {
            return Array.Empty<GroceryItemDto>();
        }

        // One write for the whole batch and one batch for the log, which is what makes the restock a single
        // change to the catalogue rather than twenty.
        await _repository.ReplaceAllAsync(items);

        await _activity.RecordManyAsync(restocked.Select(item => new ActivityEntry
        {
            Kind = ActivityKind.BulkRestock,
            ItemId = item.Id,
            ItemName = item.Name,
            StockDelta = quantities[item.Id],
            Detail = "Restocked"
        }));

        return restocked;
    }

    public async Task<GroceryItemDto?> SetArchivedAsync(int id, bool archived)
    {
        var item = await _repository.GetByIdAsync(id);

        if (item is null)
        {
            return null;
        }

        if (item.IsArchived == archived)
        {
            // Already in the state that was asked for, so there is nothing to change and nothing to record.
            return MapToDto(item);
        }

        var changed = await _history.RunAsync(
            $"{Verb(archived)} {item.Name}",
            () => SetArchivedAllAsync(new[] { id }, archived));

        return changed.FirstOrDefault() ?? MapToDto(item);
    }

    /// <summary>
    /// The word a step that changes the archive state reads as, so "undo archiving brie" says what happened.
    /// </summary>
    /// <param name="archived">True when the change archives, false when it restores.</param>
    /// <returns>The verb, in the form the undo control uses.</returns>
    private static string Verb(bool archived) => archived ? "Archiving" : "Restoring";

    public async Task<IReadOnlyList<GroceryItemDto>> SetArchivedAsync(IReadOnlyCollection<int> ids, bool archived)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var targets = ids.Distinct().ToList();

        if (targets.Count == 0)
        {
            return Array.Empty<GroceryItemDto>();
        }

        // Read first, so the step is named by what will actually change, and so a selection that is already in
        // the state that was asked for is not recorded as a change at all.
        var catalogue = await _repository.GetAllAsync();
        var affected = catalogue.Count(item => targets.Contains(item.Id) && item.IsArchived != archived);

        if (affected == 0)
        {
            return Array.Empty<GroceryItemDto>();
        }

        var what = affected == 1 ? "1 item" : $"{affected} items";

        return await _history.RunAsync(
            $"{Verb(archived)} {what}",
            () => SetArchivedAllAsync(targets, archived));
    }

    private async Task<IReadOnlyList<GroceryItemDto>> SetArchivedAllAsync(
        IReadOnlyCollection<int> ids,
        bool archived)
    {
        var items = (await _repository.GetAllAsync()).ToList();
        var changed = new List<GroceryItemDto>();

        foreach (var item in items)
        {
            // Only rows that are not already in the state that was asked for, so archiving a selection that
            // happens to include an archived row does not log it twice.
            if (ids.Contains(item.Id) && item.IsArchived != archived)
            {
                item.IsArchived = archived;
                changed.Add(MapToDto(item));
            }
        }

        if (changed.Count == 0)
        {
            return Array.Empty<GroceryItemDto>();
        }

        await _repository.ReplaceAllAsync(items);

        await _activity.RecordManyAsync(changed.Select(item => new ActivityEntry
        {
            Kind = archived ? ActivityKind.Archived : ActivityKind.Restored,
            ItemId = item.Id,
            ItemName = item.Name,
            Detail = archived ? "Archived" : "Back in the catalogue"
        }));

        return changed;
    }

    public async Task<int> DeleteManyAsync(IReadOnlyCollection<int> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var targets = ids.Distinct().ToList();

        if (targets.Count == 0)
        {
            return 0;
        }

        // Read first, so the step is named by what will actually go, and so a selection that matches nothing
        // is not recorded as a change at all.
        var catalogue = await _repository.GetAllAsync();
        var found = catalogue.Count(item => targets.Contains(item.Id));

        if (found == 0)
        {
            return 0;
        }

        var what = found == 1 ? "1 item" : $"{found} items";

        return await _history.RunAsync($"Deleting {what}", () => DeleteAllAsync(targets));
    }

    private async Task<int> DeleteAllAsync(IReadOnlyCollection<int> ids)
    {
        var items = (await _repository.GetAllAsync()).ToList();
        var removed = items.Where(item => ids.Contains(item.Id)).ToList();

        if (removed.Count == 0)
        {
            return 0;
        }

        await _repository.ReplaceAllAsync(items.Where(item => !ids.Contains(item.Id)).ToList());

        await _activity.RecordManyAsync(removed.Select(item => new ActivityEntry
        {
            Kind = ActivityKind.Deleted,
            ItemId = item.Id,
            ItemName = item.Name,
            Detail = "Removed from the catalogue"
        }));

        return removed.Count;
    }

    public async Task ResetDemoDataAsync()
    {
        await _repository.ResetAsync();

        // The log described a catalogue that no longer exists, so it goes with it. Leaving it behind would
        // leave movements recorded against items that are not there any more.
        await _activity.ClearAsync();
    }

    private static GroceryItemDto MapToDto(GroceryItem grocery)
    {
        return new GroceryItemDto
        {
            Id = grocery.Id,
            Name = grocery.Name,
            Sku = grocery.Sku,
            Category = grocery.Category,
            Supplier = grocery.Supplier,
            Unit = grocery.Unit,
            Price = grocery.Price,
            CostToProduce = grocery.CostToProduce,
            Stock = grocery.Stock,
            ReorderPoint = grocery.ReorderPoint,
            TargetMarginPercent = grocery.TargetMarginPercent,
            Tags = NormalizeTags(grocery.Tags),
            Notes = grocery.Notes,
            IsArchived = grocery.IsArchived,
            LastRestockedUtc = grocery.LastRestockedUtc,
            CreatedUtc = grocery.CreatedUtc
        };
    }

    /// <summary>
    /// Copies a tag collection and drops the blanks, so a tag typed as " , , " does not become three
    /// empty labels in the filter bar.
    /// </summary>
    /// <param name="tags">The tags to normalise.</param>
    /// <returns>A new list of trimmed, non-empty tags with duplicates removed.</returns>
    private static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return new List<string>();
        }

        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}