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

    /// <summary>
    /// Supplier name given to an item that has not been assigned one, so the supplier grouping on
    /// the restock worksheet and the supplier filters always have a value to work with.
    /// </summary>
    public const string UnassignedSupplier = "Unassigned";

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

        // Detached copies: the catalogue is cached in memory for the session, so a caller must never
        // be able to reach in and change it without going through a write.
        return items.OrderBy(g => g.Category).ThenBy(g => g.Name).Select(Clone).ToList();
    }

    /// <inheritdoc />
    public async Task<GroceryItem?> GetByIdAsync(int id)
    {
        var items = await GetItemsAsync();
        var item = items.FirstOrDefault(g => g.Id == id);
        return item is null ? null : Clone(item);
    }

    /// <inheritdoc />
    public async Task<GroceryItem> CreateAsync(GroceryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var items = await GetItemsAsync();

        item.Id = _nextId++;
        item.CreatedUtc = DateTime.UtcNow;
        ApplyDefaults(item);
        items.Add(item);

        await _store.SaveAsync(items);
        return Clone(item);
    }

    /// <inheritdoc />
    public async Task<GroceryItem?> UpdateAsync(int id, GroceryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

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
        existingItem.Supplier = item.Supplier;
        existingItem.Unit = item.Unit;
        existingItem.ReorderPoint = item.ReorderPoint;
        existingItem.TargetMarginPercent = item.TargetMarginPercent;
        existingItem.Notes = item.Notes;
        existingItem.Tags = CopyTags(item.Tags);

        // A blank SKU means "leave the code alone", not "remove it": the editor offers the code as an
        // optional field, so an ordinary edit must not wipe it.
        if (!string.IsNullOrWhiteSpace(item.Sku))
        {
            existingItem.Sku = item.Sku.Trim();
        }

        // Archiving, restocking and the creation timestamp are changed by their own actions rather
        // than by the edit form, so an ordinary update leaves them untouched.
        ApplyDefaults(existingItem);

        await _store.SaveAsync(items);
        return Clone(existingItem);
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
    public async Task<IReadOnlyList<GroceryItem>> ReplaceAllAsync(IEnumerable<GroceryItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var replacement = items.Select(Clone).ToList();
        foreach (var item in replacement)
        {
            ApplyDefaults(item);
        }

        await _gate.WaitAsync();
        try
        {
            _items = replacement;
            _nextId = replacement.Count == 0 ? 1 : replacement.Max(g => g.Id) + 1;
            await _store.SaveAsync(_items);

            return _items.Select(Clone).ToList();
        }
        finally
        {
            _gate.Release();
        }
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

                // A payload written by an earlier build predates the SKU and supplier fields, and a
                // restored backup can arrive without them. Fill the gaps in and repair the payload
                // once rather than on every read.
                var repaired = false;
                foreach (var item in _items)
                {
                    repaired |= ApplyDefaults(item);
                }

                if (repaired)
                {
                    await _store.SaveAsync(_items);
                }
            }
            else
            {
                _items = LoadSeedItems();

                foreach (var item in _items)
                {
                    ApplyDefaults(item);
                }

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
                Supplier = SeedSupplier(category),
                Price = seed.Price,
                CostToProduce = seed.CostToProduce,
                Stock = seed.Stock,
                CreatedUtc = DateTime.UtcNow
            });
        }

        return items;
    }

    /// <summary>
    /// The supplier the seeded demo catalogue buys each category from.
    /// </summary>
    /// <param name="category">The category of the seeded item.</param>
    /// <returns>The supplier name to record against it.</returns>
    /// <remarks>
    /// The seed file predates suppliers, so every seeded item would otherwise be filed under one placeholder
    /// name, and the supplier grouping would look broken rather than simply empty. Naming one supplier per
    /// category gives the demo something realistic to group, filter and order by without rewriting the seed
    /// data itself.
    /// </remarks>
    private static string SeedSupplier(Category category) => category switch
    {
        Category.Fruit => "Wye Valley Produce",
        Category.Vegetable => "Fenland Growers",
        Category.Meat => "Bramble Farm Butchers",
        Category.Cheese => "Cheddar Gorge Creamery",
        Category.Bread => "Mill Street Bakery",
        _ => UnassignedSupplier
    };

    /// <summary>
    /// Fills in the fields that are derived rather than entered: the stock keeping unit code and the
    /// supplier name.
    /// </summary>
    /// <param name="item">The item to complete; updated in place.</param>
    /// <returns>True when the item was changed, so the caller knows the stored payload needs rewriting.</returns>
    private static bool ApplyDefaults(GroceryItem item)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(item.Sku))
        {
            item.Sku = GenerateSku(item.Category, item.Id);
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(item.Supplier))
        {
            item.Supplier = UnassignedSupplier;
            changed = true;
        }

        // A hand-edited or restored payload can carry an explicit null where the model expects a
        // collection, so the guard is on the value rather than on the annotation.
        if (item.Tags is null)
        {
            item.Tags = new List<string>();
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Builds the stock keeping unit code for an item: three letters taken from the category, a dash,
    /// then the identifier padded to four digits - for example "FRU-0007", "VEG-0011".
    /// </summary>
    /// <param name="category">The item's category, which supplies the prefix.</param>
    /// <param name="id">The item's identifier, which makes the code unique.</param>
    /// <returns>The generated code.</returns>
    private static string GenerateSku(Category category, int id)
        => $"{category.ToString()[..3].ToUpperInvariant()}-{id:D4}";

    /// <summary>
    /// Creates a detached copy of an item.
    /// </summary>
    /// <remarks>
    /// The catalogue is cached in memory for the life of the browser session, so everything handed
    /// out of the repository is a copy. Without that a page could edit an item by holding on to it
    /// and the change would never be written, and the undo stack would hold references to objects
    /// that later edits mutate underneath it.
    /// </remarks>
    /// <param name="item">The item to copy.</param>
    /// <returns>A new item instance carrying the same values.</returns>
    private static GroceryItem Clone(GroceryItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Sku = item.Sku,
        Category = item.Category,
        Supplier = item.Supplier,
        Unit = item.Unit,
        Price = item.Price,
        CostToProduce = item.CostToProduce,
        Stock = item.Stock,
        ReorderPoint = item.ReorderPoint,
        TargetMarginPercent = item.TargetMarginPercent,
        Tags = CopyTags(item.Tags),
        Notes = item.Notes,
        IsArchived = item.IsArchived,
        LastRestockedUtc = item.LastRestockedUtc,
        CreatedUtc = item.CreatedUtc
    };

    /// <summary>
    /// Copies a tag collection, so two items never end up sharing one list.
    /// </summary>
    /// <param name="tags">The tags to copy.</param>
    /// <returns>A new list holding the same tags.</returns>
    private static List<string> CopyTags(List<string>? tags)
        => tags is null ? new List<string>() : new List<string>(tags);
}
