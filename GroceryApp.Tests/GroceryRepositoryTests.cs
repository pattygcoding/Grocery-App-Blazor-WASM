namespace GroceryApp.Tests;

/// <summary>
/// Tests for the browser storage backed repository used by the Blazor WebAssembly build.
/// The in-memory store implements the same contract as the localStorage store, so these tests
/// cover seeding, identifier generation and persistence without needing a browser.
/// </summary>
public class GroceryRepositoryTests
{
    private readonly InMemoryGroceryStore _store = new();
    private readonly GroceryRepository _repository;

    public GroceryRepositoryTests()
    {
        _repository = new GroceryRepository(_store);
    }

    private static GroceryItem NewItem(string name = "Dragon Fruit", int stock = 5) => new()
    {
        Name = name,
        Category = Category.Fruit,
        Price = 5.50m,
        CostToProduce = 3.00m,
        Stock = stock
    };

    #region Seeding

    [Fact]
    public async Task GetAllAsync_OnFirstUse_SeedsFiftyItemsAcrossFiveCategories()
    {
        // Act
        var items = (await _repository.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(50, items.Count);
        Assert.Equal(
            Enum.GetValues<Category>().OrderBy(c => c),
            items.Select(i => i.Category).Distinct().OrderBy(c => c));
        Assert.All(Enum.GetValues<Category>(), category => Assert.Equal(10, items.Count(i => i.Category == category)));
        Assert.Equal(Enumerable.Range(1, 50), items.Select(i => i.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task GetAllAsync_OrdersItemsByCategoryThenName()
    {
        // Act
        var items = (await _repository.GetAllAsync()).ToList();

        // Assert
        var expectedOrder = items.OrderBy(i => i.Category).ThenBy(i => i.Name).Select(i => i.Name);
        Assert.Equal(expectedOrder, items.Select(i => i.Name));
    }

    [Fact]
    public async Task GetAllAsync_PersistsTheSeededCatalogueSoItCanBeReloaded()
    {
        // Act
        await _repository.GetAllAsync();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(_store.Payload));

        var reloadedRepository = new GroceryRepository(_store);
        Assert.Equal(50, (await reloadedRepository.GetAllAsync()).Count());
    }

    [Fact]
    public async Task GetAllAsync_WhenStoreAlreadyHasData_DoesNotReseed()
    {
        // Arrange
        var existing = new List<GroceryItem> { NewItem("Saved Item") };
        existing[0].Id = 7;
        var store = new InMemoryGroceryStore();
        await store.SaveAsync(existing);

        // Act
        var repository = new GroceryRepository(store);
        var items = (await repository.GetAllAsync()).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal("Saved Item", items[0].Name);
    }

    #endregion

    #region Create / Update / Delete

    [Fact]
    public async Task CreateAsync_AssignsNextIdentifierSetsTimestampAndPersists()
    {
        // Arrange
        await _repository.GetAllAsync();

        // Act
        var created = await _repository.CreateAsync(NewItem());

        // Assert
        Assert.Equal(51, created.Id);
        Assert.NotEqual(default, created.CreatedUtc);
        Assert.Equal(51, (await _repository.GetAllAsync()).Count());

        var reloadedRepository = new GroceryRepository(_store);
        Assert.True(await reloadedRepository.ExistsAsync(51));
    }

    [Fact]
    public async Task UpdateAsync_WithExistingItem_UpdatesAllFields()
    {
        // Arrange
        await _repository.GetAllAsync();
        var original = (await _repository.GetByIdAsync(1))!;

        var update = new GroceryItem
        {
            Name = "Updated Name",
            Category = Category.Bread,
            Price = 9.99m,
            CostToProduce = 4.44m,
            Stock = 3
        };

        // Act
        var updated = await _repository.UpdateAsync(original.Id, update);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal(Category.Bread, updated.Category);
        Assert.Equal(9.99m, updated.Price);
        Assert.Equal(4.44m, updated.CostToProduce);
        Assert.Equal(3, updated.Stock);

        var reloadedRepository = new GroceryRepository(_store);
        Assert.Equal("Updated Name", (await reloadedRepository.GetByIdAsync(original.Id))!.Name);
    }

    [Fact]
    public async Task UpdateAsync_WhenItemDoesNotExist_ReturnsNull()
    {
        Assert.Null(await _repository.UpdateAsync(4242, NewItem()));
    }

    [Fact]
    public async Task DeleteAsync_WithExistingItem_RemovesItFromTheStore()
    {
        // Arrange
        await _repository.GetAllAsync();

        // Act
        var deleted = await _repository.DeleteAsync(1);

        // Assert
        Assert.True(deleted);
        Assert.Equal(49, (await _repository.GetAllAsync()).Count());
        Assert.False(await _repository.ExistsAsync(1));

        var reloadedRepository = new GroceryRepository(_store);
        Assert.False(await reloadedRepository.ExistsAsync(1));
    }

    [Fact]
    public async Task DeleteAsync_WhenItemDoesNotExist_ReturnsFalse()
    {
        Assert.False(await _repository.DeleteAsync(9999));
    }

    [Fact]
    public async Task GetByIdAsync_WhenItemDoesNotExist_ReturnsNull()
    {
        Assert.Null(await _repository.GetByIdAsync(9999));
    }

    #endregion

    #region Reset

    [Fact]
    public async Task ResetAsync_RestoresTheSeededCatalogue()
    {
        // Arrange
        await _repository.GetAllAsync();
        await _repository.DeleteAsync(1);
        await _repository.CreateAsync(NewItem("Temporary Item"));

        // Act
        await _repository.ResetAsync();

        // Assert
        var items = (await _repository.GetAllAsync()).ToList();
        Assert.Equal(50, items.Count);
        Assert.Contains(items, i => i.Id == 1);
        Assert.DoesNotContain(items, i => i.Name == "Temporary Item");

        var reloadedRepository = new GroceryRepository(_store);
        Assert.Equal(50, (await reloadedRepository.GetAllAsync()).Count());
    }

    #endregion
}
