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

    #region Generated fields and payload migration

    [Fact]
    public async Task CreateAsync_GeneratesASkuFromTheCategoryAndTheIdentifier()
    {
        await _repository.GetAllAsync();

        var created = await _repository.CreateAsync(NewItem("Dragon Fruit"));

        Assert.Equal("FRU-0051", created.Sku);
    }

    [Fact]
    public async Task CreateAsync_KeepsASuppliedSku()
    {
        await _repository.GetAllAsync();
        var item = NewItem();
        item.Sku = "CUSTOM-1";

        Assert.Equal("CUSTOM-1", (await _repository.CreateAsync(item)).Sku);
    }

    [Fact]
    public async Task CreateAsync_WhenNoSupplierIsGiven_AssignsTheUnassignedSupplier()
    {
        await _repository.GetAllAsync();

        var created = await _repository.CreateAsync(NewItem());

        Assert.Equal(GroceryRepository.UnassignedSupplier, created.Supplier);
    }

    [Fact]
    public async Task GetAllAsync_WhenTheStoredPayloadPredatesTheNewFields_RepairsItAndWritesItBack()
    {
        // A payload as an earlier build wrote it: only the original six properties, no SKU, no
        // supplier, no unit, no reorder point and no target margin.
        var store = new RawCatalogueStore
        {
            Raw = """[{"id":7,"name":"Legacy Brie","category":"Cheese","price":5.99,"costToProduce":3.50,"stock":4,"createdUtc":"2024-01-01T00:00:00Z"}]"""
        };
        var repository = new GroceryRepository(store);

        var item = Assert.Single(await repository.GetAllAsync());

        Assert.Equal("Legacy Brie", item.Name);
        Assert.Equal("CHE-0007", item.Sku);
        Assert.Equal(GroceryRepository.UnassignedSupplier, item.Supplier);
        Assert.Equal(UnitOfMeasure.Each, item.Unit);
        Assert.Equal(5, item.ReorderPoint);
        Assert.Equal(40m, item.TargetMarginPercent);
        Assert.Empty(item.Tags);
        Assert.False(item.IsArchived);

        // The repair is written back, so the visitor does not pay for it on every page load.
        Assert.Contains("\"sku\":\"CHE-0007\"", store.Raw);
    }

    [Fact]
    public async Task GetAllAsync_WhenThePayloadIsAlreadyComplete_DoesNotRewriteIt()
    {
        await _repository.GetAllAsync();
        var afterSeed = _store.Payload;

        // A second repository over the same payload represents the next visit.
        await new GroceryRepository(_store).GetAllAsync();

        Assert.Equal(afterSeed, _store.Payload);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsDetachedCopiesSoTheCachedCatalogueCannotBeMutated()
    {
        await _repository.GetAllAsync();

        var first = (await _repository.GetByIdAsync(1))!;
        first.Name = "Changed without saving";
        first.Tags.Add("tampered");

        var reloaded = (await _repository.GetByIdAsync(1))!;
        Assert.Equal("Apple", reloaded.Name);
        Assert.Empty(reloaded.Tags);
    }

    [Fact]
    public async Task UpdateAsync_LeavesTheSkuAloneWhenTheUpdateOmitsIt()
    {
        await _repository.GetAllAsync();
        var original = (await _repository.GetByIdAsync(1))!;

        // NewItem leaves the SKU blank, which the editor means as "keep the existing code".
        var updated = await _repository.UpdateAsync(original.Id, NewItem("Renamed Apple"));

        Assert.NotNull(updated);
        Assert.Equal(original.Sku, updated!.Sku);
        Assert.Equal("Renamed Apple", updated.Name);
    }

    [Fact]
    public async Task UpdateAsync_PreservesTheFieldsThatTheirOwnActionsManage()
    {
        await _repository.GetAllAsync();

        var archived = (await _repository.GetByIdAsync(1))!;
        archived.IsArchived = true;
        archived.LastRestockedUtc = new DateTime(2025, 2, 2, 8, 0, 0, DateTimeKind.Utc);
        var before = archived.CreatedUtc;
        await _repository.ReplaceAllAsync(new[] { archived });

        var updated = await _repository.UpdateAsync(1, NewItem("Archived Apple"));

        // Archiving, restocking and the creation timestamp are not edit form fields.
        Assert.NotNull(updated);
        Assert.True(updated!.IsArchived);
        Assert.Equal(new DateTime(2025, 2, 2, 8, 0, 0, DateTimeKind.Utc), updated.LastRestockedUtc);
        Assert.Equal(before, updated.CreatedUtc);
    }

    #endregion

    #region ReplaceAllAsync

    [Fact]
    public async Task ReplaceAllAsync_ReplacesTheCatalogueAndPersistsItForTheNextVisit()
    {
        await _repository.GetAllAsync();
        var item = NewItem("Only Item");
        item.Id = 4;

        var stored = await _repository.ReplaceAllAsync(new[] { item });

        Assert.Equal("Only Item", Assert.Single(stored).Name);
        Assert.Equal(4, Assert.Single(await new GroceryRepository(_store).GetAllAsync()).Id);
    }

    [Fact]
    public async Task ReplaceAllAsync_ContinuesIdentifierAllocationAfterTheHighestIdentifier()
    {
        await _repository.GetAllAsync();
        var first = NewItem("First");
        first.Id = 3;
        var second = NewItem("Second");
        second.Id = 9;

        await _repository.ReplaceAllAsync(new[] { first, second });

        Assert.Equal(10, (await _repository.CreateAsync(NewItem("New Item"))).Id);
    }

    [Fact]
    public async Task ReplaceAllAsync_WithAnEmptyCollection_EmptiesTheCatalogueAndRestartsIdentifiers()
    {
        await _repository.GetAllAsync();

        Assert.Empty(await _repository.ReplaceAllAsync(Array.Empty<GroceryItem>()));

        Assert.Empty(await _repository.GetAllAsync());
        Assert.Equal(1, (await _repository.CreateAsync(NewItem())).Id);
    }

    [Fact]
    public async Task ReplaceAllAsync_DoesNotAliasTheSuppliedItems()
    {
        var item = NewItem("Supplied");
        item.Id = 2;

        await _repository.ReplaceAllAsync(new[] { item });

        // Changing the caller's object after the write must not reach into the stored catalogue.
        item.Name = "Mutated afterwards";
        item.Tags.Add("tampered");

        var stored = Assert.Single(await _repository.GetAllAsync());
        Assert.Equal("Supplied", stored.Name);
        Assert.Empty(stored.Tags);
    }

    [Fact]
    public async Task ReplaceAllAsync_AssignsTheGeneratedFieldsToItemsThatLackThem()
    {
        var item = NewItem("Unassigned");
        item.Id = 6;
        item.Category = Category.Bread;

        var stored = Assert.Single(await _repository.ReplaceAllAsync(new[] { item }));

        Assert.Equal("BRE-0006", stored.Sku);
        Assert.Equal(GroceryRepository.UnassignedSupplier, stored.Supplier);
    }

    #endregion

    /// <summary>
    /// Store double that accepts an arbitrary raw payload, so a test can present the repository with
    /// a record written by an earlier build.
    /// </summary>
    private sealed class RawCatalogueStore : JsonGroceryStore
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
}
