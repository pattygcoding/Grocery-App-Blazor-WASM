using System.Reflection;
using System.Text.Json;
using GroceryApp.Models;

namespace GroceryApp.Data.Repositories;

/// <summary>
/// Repository implementation for the Blazor WebAssembly build of the app.
/// </summary>
/// <remarks>
/// The catalogue is cached in memory for the lifetime of the browser session and persisted
/// through <see cref="IGroceryStore"/> (localStorage in the browser). The first read seeds the
/// catalogue with the 50 demo items embedded in SeedData.json.
/// </remarks>
public class GroceryRepository : IGroceryRepository
{
    /// <summary>
    /// Name of the embedded resource holding the demo catalogue.
    /// </summary>
    public const string SeedResourceName = "GroceryApp.Data.SeedData.json";

    private readonly IGroceryStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<GroceryItem>? _items;
    private int _nextId = 1;

    /// <summary>
    /// Initializes a new instance of the GroceryRepository class.
    /// </summary>
    /// <param name="store">The browser storage used to persist the catalogue.</param>
    public GroceryRepository(IGroceryStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GroceryItem>> GetAllAsync()
    {
        var items = await GetItemsAsync();
        return items.OrderBy(g => g.Category).ThenBy(g => g.Name).ToList();
    }

    /// <inheritdoc />
    public async Task<GroceryItem?> GetByIdAsync(int id)
    {
        var items = await GetItemsAsync();
        return items.FirstOrDefault(g => g.Id == id);
    }

    /// <inheritdoc />
    public async Task<GroceryItem> CreateAsync(GroceryItem item)
    {
        var items = await GetItemsAsync();

        item.Id = _nextId++;
        item.CreatedUtc = DateTime.UtcNow;
        items.Add(item);

        await _store.SaveAsync(items);
        return item;
    }

    /// <inheritdoc />
    public async Task<GroceryItem?> UpdateAsync(int id, GroceryItem item)
    {
        var items = await GetItemsAsync();
        var existingItem = items.FirstOrDefault(g => g.Id == id);
        if (existingItem == null)
        {
            return null;
        }

        existingItem.Name = item.Name;
        existingItem.Category = item.Category;
        existingItem.Price = item.Price;
        existingItem.CostToProduce = item.CostToProduce;
        existingItem.Stock = item.Stock;

        await _store.SaveAsync(items);
        return existingItem;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id)
    {
        var items = await GetItemsAsync();
        var item = items.FirstOrDefault(g => g.Id == id);
        if (item == null)
        {
            return false;
        }

        items.Remove(item);
        await _store.SaveAsync(items);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(int id)
    {
        var items = await GetItemsAsync();
        return items.Any(g => g.Id == id);
    }

    /// <inheritdoc />
    public async Task ResetAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await _store.ClearAsync();
            _items = null;
        }
        finally
        {
            _gate.Release();
        }

        // Reload immediately so the seeded catalogue is available (and persisted) again.
        await GetItemsAsync();
    }

    /// <summary>
    /// Gets the catalogue, loading it from browser storage on first use and seeding the
    /// demo data when nothing has been stored yet.
    /// </summary>
    private async Task<List<GroceryItem>> GetItemsAsync()
    {
        if (_items != null)
        {
            return _items;
        }

        await _gate.WaitAsync();
        try
        {
            if (_items != null)
            {
                return _items;
            }

            var stored = await _store.LoadAsync();

            if (stored.Count > 0)
            {
                _items = stored.ToList();
            }
            else
            {
                _items = LoadSeedItems();
                await _store.SaveAsync(_items);
            }

            _nextId = _items.Count == 0 ? 1 : _items.Max(g => g.Id) + 1;
            return _items;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Loads the demo catalogue from the SeedData.json embedded resource.
    /// </summary>
    /// <returns>The seeded grocery items with sequential identifiers.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the embedded seed data is missing.</exception>
    private static List<GroceryItem> LoadSeedItems()
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(SeedResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"The embedded seed data resource '{SeedResourceName}' could not be found.");
        }

        var seeds = JsonSerializer.Deserialize(stream, GroceryJsonContext.Default.ListSeedGroceryItem)
                    ?? new List<SeedGroceryItem>();

        var items = new List<GroceryItem>(seeds.Count);
        var id = 1;

        foreach (var seed in seeds)
        {
            // Only process items with a valid category value to keep the catalogue consistent.
            if (!Enum.TryParse<Category>(seed.Category, ignoreCase: true, out var category))
            {
                continue;
            }

            items.Add(new GroceryItem
            {
                Id = id++,
                Name = seed.Name,
                Category = category,
                Price = seed.Price,
                CostToProduce = seed.CostToProduce,
                Stock = seed.Stock,
                CreatedUtc = DateTime.UtcNow
            });
        }

        return items;
    }
}
