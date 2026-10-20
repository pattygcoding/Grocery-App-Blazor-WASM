namespace GroceryApp.Tests;

/// <summary>
/// Tests for the shared JSON store base class, including recovery from a corrupted payload.
/// </summary>
public class JsonGroceryStoreTests
{
    /// <summary>
    /// Store double that lets a test write an arbitrary (including invalid) raw payload.
    /// </summary>
    private sealed class RawStore : JsonGroceryStore
    {
        public string? Raw { get; set; }

        protected override Task<string?> ReadRawAsync() => Task.FromResult(Raw);

        protected override Task WriteRawAsync(string value)
        {
            Raw = value;
            return Task.CompletedTask;
        }

        protected override Task RemoveRawAsync()
        {
            Raw = null;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LoadAsync_WhenNothingIsStored_ReturnsEmpty()
    {
        var store = new RawStore();

        Assert.Empty(await store.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_WhenPayloadIsCorrupt_ReturnsEmptyInsteadOfThrowing()
    {
        // Arrange: truncated JSON, and valid JSON of the wrong shape.
        var truncated = new RawStore { Raw = "{\"not\":\"an array\"" };
        var wrongShape = new RawStore { Raw = "{\"not\":\"an array\"}" };

        // Act & Assert
        Assert.Empty(await truncated.LoadAsync());
        Assert.Empty(await wrongShape.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_AfterSave_RoundTripsItemsIncludingEnums()
    {
        // Arrange
        var store = new RawStore();
        var items = new List<GroceryItem>
        {
            new() { Id = 3, Name = "Brie", Category = Category.Cheese, Price = 5.99m, CostToProduce = 3.50m, Stock = 4 }
        };

        // Act
        await store.SaveAsync(items);
        var loaded = await store.LoadAsync();

        // Assert
        var item = Assert.Single(loaded);
        Assert.Equal("Brie", item.Name);
        Assert.Equal(Category.Cheese, item.Category);
        Assert.Equal(5.99m, item.Price);
        Assert.Equal(3.50m, item.CostToProduce);
        Assert.Equal(4, item.Stock);
        Assert.Contains("\"category\":\"Cheese\"", store.Raw);
    }

    [Fact]
    public async Task Repository_WhenStoredPayloadIsCorrupt_ReseedsTheCatalogue()
    {
        // Arrange
        var store = new RawStore { Raw = "definitely not json" };
        var repository = new GroceryRepository(store);

        // Act
        var items = (await repository.GetAllAsync()).ToList();

        // Assert: the app recovers with the seeded catalogue and repairs the payload.
        Assert.Equal(50, items.Count);
        Assert.NotEqual("definitely not json", store.Raw);
        Assert.Equal(50, (await new GroceryRepository(store).GetAllAsync()).Count());
    }
}
