using System.Linq.Expressions;

namespace GroceryApp.Tests;

public class GroceryServiceTests
{
    private readonly IGroceryRepository _mockRepository;
    private readonly IActivityService _mockActivity;
    private readonly CatalogueHistoryService _history;
    private readonly IGroceryService _service;

    public GroceryServiceTests()
    {
        _mockRepository = Substitute.For<IGroceryRepository>();
        _mockActivity = Substitute.For<IActivityService>();

        // A real history service over the mocked repository, so these tests see the same "one change, one undo
        // step" behaviour the application has rather than a stub that agrees with them.
        _history = new CatalogueHistoryService(_mockRepository, _mockActivity);
        _service = new GroceryService(_mockRepository, _mockActivity, _history);
    }

    /// <summary>
    /// Points the mocked repository at a catalogue, handing out fresh copies on every read the way the real
    /// repository does.
    /// </summary>
    /// <param name="items">The items the repository holds.</param>
    /// <remarks>
    /// The history stack snapshots what a read hands it, so a mock returning the same instances would let a
    /// later edit reach back into a snapshot and quietly change what an undo puts back.
    /// </remarks>
    private void RepositoryHolds(params GroceryItem[] items) =>
        _mockRepository.GetAllAsync().Returns(
            _ => (IEnumerable<GroceryItem>)items.Select(Copy).ToList());

    /// <summary>Copies an item the way the repository does when it hands one out.</summary>
    private static GroceryItem Copy(GroceryItem item) => new()
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
        Tags = new List<string>(item.Tags),
        Notes = item.Notes,
        IsArchived = item.IsArchived,
        LastRestockedUtc = item.LastRestockedUtc,
        CreatedUtc = item.CreatedUtc
    };

    #region GetAllGroceriesAsync Tests

    [Fact]
    public async Task GetAllGroceriesAsync_WhenRepositoryReturnsItems_ReturnsAllItems()
    {
        // Arrange
        var expectedItems = new List<GroceryItem>
        {
            new() { Id = 1, Name = "Apple", Category = Category.Fruit, Price = 1.99m, CostToProduce = 0.50m, Stock = 10 },
            new() { Id = 2, Name = "Banana", Category = Category.Fruit, Price = 0.99m, CostToProduce = 0.30m, Stock = 5 }
        };
        _mockRepository.GetAllAsync().Returns(expectedItems);

        // Act
        var result = await _service.GetAllGroceriesAsync();

        // Assert
        Assert.Equal(2, result.Count());
        var resultList = result.ToList();
        Assert.Equal("Apple", resultList[0].Name);
        Assert.Equal("Banana", resultList[1].Name);
        await _mockRepository.Received(1).GetAllAsync();
    }

    [Fact]
    public async Task GetAllGroceriesAsync_WhenRepositoryReturnsEmpty_ReturnsEmptyCollection()
    {
        // Arrange
        _mockRepository.GetAllAsync().Returns(new List<GroceryItem>());

        // Act
        var result = await _service.GetAllGroceriesAsync();

        // Assert
        Assert.Empty(result);
        await _mockRepository.Received(1).GetAllAsync();
    }

    [Fact]
    public async Task GetAllGroceriesAsync_WhenRepositoryThrowsException_PropagatesException()
    {
        // Arrange
        _mockRepository.GetAllAsync().Returns(Task.FromException<IEnumerable<GroceryItem>>(new InvalidOperationException("Database error")));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetAllGroceriesAsync());
        Assert.Equal("Database error", exception.Message);
        await _mockRepository.Received(1).GetAllAsync();
    }

    [Fact]
    public async Task GetAllGroceriesAsync_WhenRepositoryReturnsLargeCollection_ReturnsAllItems()
    {
        // Arrange
        var expectedItems = Enumerable.Range(1, 1000)
            .Select(i => new GroceryItem { Id = i, Name = $"Item{i}", Category = Category.Fruit, Price = i * 0.99m, CostToProduce = i * 0.50m, Stock = i })
            .ToList();
        _mockRepository.GetAllAsync().Returns(expectedItems);

        // Act
        var result = await _service.GetAllGroceriesAsync();

        // Assert
        Assert.Equal(1000, result.Count());
        await _mockRepository.Received(1).GetAllAsync();
    }

    #endregion

    #region GetGroceryByIdAsync Tests

    [Fact]
    public async Task GetGroceryByIdAsync_WhenItemExists_ReturnsItem()
    {
        // Arrange
        var expectedItem = new GroceryItem { Id = 1, Name = "Apple", Category = Category.Fruit, Price = 1.99m, CostToProduce = 0.50m, Stock = 10 };
        _mockRepository.GetByIdAsync(1).Returns(expectedItem);

        // Act
        var result = await _service.GetGroceryByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Apple", result.Name);
        Assert.Equal(Category.Fruit, result.Category);
        Assert.Equal(1.99m, result.Price);
        Assert.Equal(0.50m, result.CostToProduce);
        Assert.Equal(10, result.Stock);
        await _mockRepository.Received(1).GetByIdAsync(1);
    }

    [Fact]
    public async Task GetGroceryByIdAsync_WhenItemDoesNotExist_ReturnsNull()
    {
        // Arrange
        _mockRepository.GetByIdAsync(999).Returns((GroceryItem?)null);

        // Act
        var result = await _service.GetGroceryByIdAsync(999);

        // Assert
        Assert.Null(result);
        await _mockRepository.Received(1).GetByIdAsync(999);
    }

    [Fact]
    public async Task GetGroceryByIdAsync_WithZeroId_ReturnsNull()
    {
        // Arrange
        _mockRepository.GetByIdAsync(0).Returns((GroceryItem?)null);

        // Act
        var result = await _service.GetGroceryByIdAsync(0);

        // Assert
        Assert.Null(result);
        await _mockRepository.Received(1).GetByIdAsync(0);
    }

    [Fact]
    public async Task GetGroceryByIdAsync_WithNegativeId_ReturnsNull()
    {
        // Arrange
        _mockRepository.GetByIdAsync(-1).Returns((GroceryItem?)null);

        // Act
        var result = await _service.GetGroceryByIdAsync(-1);

        // Assert
        Assert.Null(result);
        await _mockRepository.Received(1).GetByIdAsync(-1);
    }

    [Fact]
    public async Task GetGroceryByIdAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        _mockRepository.GetByIdAsync(1).Returns(Task.FromException<GroceryItem?>(new InvalidOperationException("Database error")));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetGroceryByIdAsync(1));
        Assert.Equal("Database error", exception.Message);
    }

    #endregion

    #region CreateGroceryAsync Tests

    [Fact]
    public async Task CreateGroceryAsync_WithValidDto_CreatesAndReturnsItem()
    {
        // Arrange
        var createDto = new CreateGroceryItemDto
        {
            Name = "Apple",
            Category = Category.Fruit,
            Price = 1.99m,
            CostToProduce = 0.50m,
            Stock = 10
        };

        var createdItem = new GroceryItem
        {
            Id = 1,
            Name = "Apple",
            Category = Category.Fruit,
            Price = 1.99m,
            CostToProduce = 0.50m,
            Stock = 10,
            CreatedUtc = DateTime.UtcNow
        };

        _mockRepository.CreateAsync(Arg.Any<GroceryItem>()).Returns(createdItem);

        // Act
        var result = await _service.CreateGroceryAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Apple", result.Name);
        Assert.Equal(Category.Fruit, result.Category);
        Assert.Equal(1.99m, result.Price);
        Assert.Equal(0.50m, result.CostToProduce);
        Assert.Equal(10, result.Stock);

        await _mockRepository.Received(1).CreateAsync(Arg.Is<GroceryItem>(x =>
            x.Name == "Apple" &&
            x.Category == Category.Fruit &&
            x.Price == 1.99m &&
            x.CostToProduce == 0.50m &&
            x.Stock == 10));
    }

    [Fact]
    public async Task CreateGroceryAsync_WithMinimumValidValues_CreatesItem()
    {
        // Arrange
        var createDto = new CreateGroceryItemDto
        {
            Name = "A",
            Category = Category.Cheese,
            Price = 0.01m,
            CostToProduce = 0.01m,
            Stock = 0
        };

        var createdItem = new GroceryItem
        {
            Id = 2,
            Name = "A",
            Category = Category.Cheese,
            Price = 0.01m,
            CostToProduce = 0.01m,
            Stock = 0,
            CreatedUtc = DateTime.UtcNow
        };

        _mockRepository.CreateAsync(Arg.Any<GroceryItem>()).Returns(createdItem);

        // Act
        var result = await _service.CreateGroceryAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("A", result.Name);
        Assert.Equal(Category.Cheese, result.Category);
        Assert.Equal(0.01m, result.Price);
        Assert.Equal(0.01m, result.CostToProduce);
        Assert.Equal(0, result.Stock);
    }

    [Fact]
    public async Task CreateGroceryAsync_WithHighValues_CreatesItem()
    {
        // Arrange
        var createDto = new CreateGroceryItemDto
        {
            Name = new string('A', 100),
            Category = Category.Meat,
            Price = 999.99m,
            CostToProduce = 500.00m,
            Stock = int.MaxValue
        };

        var createdItem = new GroceryItem
        {
            Id = 3,
            Name = new string('A', 100),
            Category = Category.Meat,
            Price = 999.99m,
            CostToProduce = 500.00m,
            Stock = int.MaxValue,
            CreatedUtc = DateTime.UtcNow
        };

        _mockRepository.CreateAsync(Arg.Any<GroceryItem>()).Returns(createdItem);

        // Act
        var result = await _service.CreateGroceryAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(999.99m, result.Price);
        Assert.Equal(500.00m, result.CostToProduce);
        Assert.Equal(int.MaxValue, result.Stock);
    }

    [Fact]
    public async Task CreateGroceryAsync_WhenRepositoryThrowsException_PropagatesException()
    {
        // Arrange
        var createDto = new CreateGroceryItemDto
        {
            Name = "Apple",
            Category = Category.Fruit,
            Price = 1.99m,
            CostToProduce = 0.50m,
            Stock = 10
        };

        _mockRepository.CreateAsync(Arg.Any<GroceryItem>()).Returns(Task.FromException<GroceryItem>(new InvalidOperationException("Creation failed")));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateGroceryAsync(createDto));
        Assert.Equal("Creation failed", exception.Message);
    }

    [Fact]
    public async Task CreateGroceryAsync_SetsCreatedUtcTimestamp()
    {
        // Arrange
        var createDto = new CreateGroceryItemDto
        {
            Name = "Timestamp Test",
            Category = Category.Fruit,
            Price = 1.99m,
            CostToProduce = 0.50m,
            Stock = 10
        };

        var createdItem = new GroceryItem
        {
            Id = 1,
            Name = "Timestamp Test",
            Category = Category.Fruit,
            Price = 1.99m,
            CostToProduce = 0.50m,
            Stock = 10,
            CreatedUtc = DateTime.UtcNow
        };

        _mockRepository.CreateAsync(Arg.Any<GroceryItem>()).Returns(createdItem);

        // Act
        await _service.CreateGroceryAsync(createDto);

        // Assert
        await _mockRepository.Received(1).CreateAsync(Arg.Is<GroceryItem>(x =>
            x.CreatedUtc != default(DateTime)));
    }

    #endregion

    #region UpdateGroceryAsync Tests

    [Fact]
    public async Task UpdateGroceryAsync_WhenItemExists_UpdatesAndReturnsItem()
    {
        // Arrange
        var updateDto = new UpdateGroceryItemDto
        {
            Name = "Updated Apple",
            Category = Category.Fruit,
            Price = 2.99m,
            CostToProduce = 1.00m,
            Stock = 5
        };

        var updatedItem = new GroceryItem
        {
            Id = 1,
            Name = "Updated Apple",
            Category = Category.Fruit,
            Price = 2.99m,
            CostToProduce = 1.00m,
            Stock = 5,
            CreatedUtc = DateTime.UtcNow.AddDays(-1)
        };

        _mockRepository.UpdateAsync(1, Arg.Any<GroceryItem>()).Returns(updatedItem);

        // Act
        var result = await _service.UpdateGroceryAsync(1, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Apple", result.Name);
        Assert.Equal(2.99m, result.Price);
        Assert.Equal(1.00m, result.CostToProduce);
        Assert.Equal(5, result.Stock);

        await _mockRepository.Received(1).UpdateAsync(1, Arg.Is<GroceryItem>(x =>
            x.Name == "Updated Apple" &&
            x.Price == 2.99m &&
            x.CostToProduce == 1.00m &&
            x.Stock == 5));
    }

    [Fact]
    public async Task UpdateGroceryAsync_WhenItemDoesNotExist_ReturnsNull()
    {
        // Arrange
        var updateDto = new UpdateGroceryItemDto
        {
            Name = "Non-existent",
            Category = Category.Fruit,
            Price = 1.99m,
            CostToProduce = 0.50m,
            Stock = 10
        };

        _mockRepository.UpdateAsync(999, Arg.Any<GroceryItem>()).Returns((GroceryItem?)null);

        // Act
        var result = await _service.UpdateGroceryAsync(999, updateDto);

        // Assert
        Assert.Null(result);
        await _mockRepository.Received(1).UpdateAsync(999, Arg.Any<GroceryItem>());
    }

    [Fact]
    public async Task UpdateGroceryAsync_WithZeroStock_UpdatesSuccessfully()
    {
        // Arrange
        var updateDto = new UpdateGroceryItemDto
        {
            Name = "Out of Stock",
            Category = Category.Fruit,
            Price = 1.99m,
            CostToProduce = 0.50m,
            Stock = 0
        };

        var updatedItem = new GroceryItem
        {
            Id = 1,
            Name = "Out of Stock",
            Category = Category.Fruit,
            Price = 1.99m,
            CostToProduce = 0.50m,
            Stock = 0,
            CreatedUtc = DateTime.UtcNow.AddDays(-1)
        };

        _mockRepository.UpdateAsync(1, Arg.Any<GroceryItem>()).Returns(updatedItem);

        // Act
        var result = await _service.UpdateGroceryAsync(1, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Stock);
    }

    [Fact]
    public async Task UpdateGroceryAsync_VerifiesAllPropertiesUpdated()
    {
        // Arrange
        var updateDto = new UpdateGroceryItemDto
        {
            Name = "Modified Test",
            Category = Category.Vegetable,
            Price = 2.99m,
            CostToProduce = 1.50m,
            Stock = 5
        };

        var updatedItem = new GroceryItem
        {
            Id = 1,
            Name = "Modified Test",
            Category = Category.Vegetable,
            Price = 2.99m,
            CostToProduce = 1.50m,
            Stock = 5,
            CreatedUtc = DateTime.UtcNow.AddDays(-1)
        };

        _mockRepository.UpdateAsync(1, Arg.Any<GroceryItem>()).Returns(updatedItem);

        // Act
        await _service.UpdateGroceryAsync(1, updateDto);

        // Assert
        await _mockRepository.Received(1).UpdateAsync(1, Arg.Is<GroceryItem>(x =>
            x.Name == "Modified Test" &&
            x.Category == Category.Vegetable &&
            x.Price == 2.99m &&
            x.CostToProduce == 1.50m &&
            x.Stock == 5));
    }

    [Fact]
    public async Task UpdateGroceryAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var updateDto = new UpdateGroceryItemDto
        {
            Name = "Test",
            Category = Category.Fruit,
            Price = 1.99m,
            CostToProduce = 0.50m,
            Stock = 10
        };

        _mockRepository.UpdateAsync(1, Arg.Any<GroceryItem>()).Returns(Task.FromException<GroceryItem?>(new InvalidOperationException("Update failed")));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateGroceryAsync(1, updateDto));
        Assert.Equal("Update failed", exception.Message);
    }

    #endregion

    #region DeleteGroceryAsync Tests

    [Fact]
    public async Task DeleteGroceryAsync_WhenItemExists_DeletesAndReturnsTrue()
    {
        // Arrange
        _mockRepository.DeleteAsync(1).Returns(true);

        // Act
        var result = await _service.DeleteGroceryAsync(1);

        // Assert
        Assert.True(result);
        await _mockRepository.Received(1).DeleteAsync(1);
    }

    [Fact]
    public async Task DeleteGroceryAsync_WhenItemDoesNotExist_ReturnsFalse()
    {
        // Arrange
        _mockRepository.DeleteAsync(999).Returns(false);

        // Act
        var result = await _service.DeleteGroceryAsync(999);

        // Assert
        Assert.False(result);
        await _mockRepository.Received(1).DeleteAsync(999);
    }

    [Fact]
    public async Task DeleteGroceryAsync_WithZeroId_ReturnsFalse()
    {
        // Arrange
        _mockRepository.DeleteAsync(0).Returns(false);

        // Act
        var result = await _service.DeleteGroceryAsync(0);

        // Assert
        Assert.False(result);
        await _mockRepository.Received(1).DeleteAsync(0);
    }

    [Fact]
    public async Task DeleteGroceryAsync_WithNegativeId_ReturnsFalse()
    {
        // Arrange
        _mockRepository.DeleteAsync(-1).Returns(false);

        // Act
        var result = await _service.DeleteGroceryAsync(-1);

        // Assert
        Assert.False(result);
        await _mockRepository.Received(1).DeleteAsync(-1);
    }

    [Fact]
    public async Task DeleteGroceryAsync_WhenRepositoryThrowsException_PropagatesException()
    {
        // Arrange
        _mockRepository.DeleteAsync(1).Returns(Task.FromException<bool>(new InvalidOperationException("Deletion failed")));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteGroceryAsync(1));
        Assert.Equal("Deletion failed", exception.Message);
    }

    #endregion

    #region GroceryExistsAsync Tests

    [Fact]
    public async Task GroceryExistsAsync_WhenItemExists_ReturnsTrue()
    {
        // Arrange
        _mockRepository.ExistsAsync(1).Returns(true);

        // Act
        var result = await _service.GroceryExistsAsync(1);

        // Assert
        Assert.True(result);
        await _mockRepository.Received(1).ExistsAsync(1);
    }

    [Fact]
    public async Task GroceryExistsAsync_WhenItemDoesNotExist_ReturnsFalse()
    {
        // Arrange
        _mockRepository.ExistsAsync(999).Returns(false);

        // Act
        var result = await _service.GroceryExistsAsync(999);

        // Assert
        Assert.False(result);
        await _mockRepository.Received(1).ExistsAsync(999);
    }

    [Fact]
    public async Task GroceryExistsAsync_WithZeroId_ReturnsFalse()
    {
        // Arrange
        _mockRepository.ExistsAsync(0).Returns(false);

        // Act
        var result = await _service.GroceryExistsAsync(0);

        // Assert
        Assert.False(result);
        await _mockRepository.Received(1).ExistsAsync(0);
    }

    [Fact]
    public async Task GroceryExistsAsync_WithNegativeId_ReturnsFalse()
    {
        // Arrange
        _mockRepository.ExistsAsync(-1).Returns(false);

        // Act
        var result = await _service.GroceryExistsAsync(-1);

        // Assert
        Assert.False(result);
        await _mockRepository.Received(1).ExistsAsync(-1);
    }

    [Fact]
    public async Task GroceryExistsAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        _mockRepository.ExistsAsync(1).Returns(Task.FromException<bool>(new InvalidOperationException("Exists check failed")));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GroceryExistsAsync(1));
        Assert.Equal("Exists check failed", exception.Message);
    }

    #endregion

    #region Edge Cases and Category Tests

    [Fact]
    public async Task CreateGroceryAsync_WithAllCategories_CreatesSuccessfully()
    {
        // Arrange & Act & Assert for each category
        var categories = Enum.GetValues<Category>();

        foreach (var category in categories)
        {
            var createDto = new CreateGroceryItemDto
            {
                Name = $"Test {category}",
                Category = category,
                Price = 1.99m,
                CostToProduce = 0.50m,
                Stock = 10
            };

            var createdItem = new GroceryItem
            {
                Id = 1,
                Name = $"Test {category}",
                Category = category,
                Price = 1.99m,
                CostToProduce = 0.50m,
                Stock = 10,
                CreatedUtc = DateTime.UtcNow
            };

            _mockRepository.CreateAsync(Arg.Any<GroceryItem>()).Returns(createdItem);

            var result = await _service.CreateGroceryAsync(createDto);

            Assert.NotNull(result);
            Assert.Equal(category, result.Category);
        }
    }

    [Fact]
    public async Task GetAllGroceriesAsync_MultipleCallsToService_CallsRepositoryEachTime()
    {
        // Arrange
        var expectedItems = new List<GroceryItem>
        {
            new() { Id = 1, Name = "Apple", Category = Category.Fruit, Price = 1.99m, CostToProduce = 0.50m, Stock = 10 }
        };
        _mockRepository.GetAllAsync().Returns(expectedItems);

        // Act
        await _service.GetAllGroceriesAsync();
        await _service.GetAllGroceriesAsync();

        // Assert
        await _mockRepository.Received(2).GetAllAsync();
        await _mockRepository.DidNotReceive().GetByIdAsync(Arg.Any<int>());
        await _mockRepository.DidNotReceive().CreateAsync(Arg.Any<GroceryItem>());
    }

    [Fact]
    public async Task UpdateGroceryAsync_CallsRepositoryWithCorrectParameters()
    {
        // Arrange
        var updateDto = new UpdateGroceryItemDto
        {
            Name = "Test Update",
            Category = Category.Vegetable,
            Price = 5.99m,
            CostToProduce = 3.00m,
            Stock = 20
        };

        var updatedItem = new GroceryItem
        {
            Id = 42,
            Name = "Test Update",
            Category = Category.Vegetable,
            Price = 5.99m,
            CostToProduce = 3.00m,
            Stock = 20,
            CreatedUtc = DateTime.UtcNow.AddDays(-5)
        };

        _mockRepository.UpdateAsync(42, Arg.Any<GroceryItem>()).Returns(updatedItem);

        // Act
        await _service.UpdateGroceryAsync(42, updateDto);

        // Assert
        await _mockRepository.Received(1).UpdateAsync(42, Arg.Is<GroceryItem>(x =>
            x.Id == 0 && // New item should have Id = 0 when passed to repository
            x.Name == "Test Update" &&
            x.Category == Category.Vegetable &&
            x.Price == 5.99m &&
            x.CostToProduce == 3.00m &&
            x.Stock == 20));
    }

    [Fact]
    public async Task DeleteGroceryAsync_ReadsTheItemOnceSoTheLogCanNameItThenDeletesIt()
    {
        // Arrange
        _mockRepository.GetByIdAsync(1).Returns(Stored(1, "Apple"));
        _mockRepository.DeleteAsync(1).Returns(true);

        // Act
        await _service.DeleteGroceryAsync(1);

        // Assert: one read to name the item for the activity log, then one delete, and no other lookups.
        await _mockRepository.Received(1).GetByIdAsync(1);
        await _mockRepository.Received(1).DeleteAsync(1);
        await _mockRepository.DidNotReceive().ExistsAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task GetGroceryByIdAsync_WithValidId_CallsRepositoryOnce()
    {
        // Arrange
        var groceryItem = new GroceryItem
        {
            Id = 5,
            Name = "Test Item",
            Category = Category.Meat,
            Price = 10.99m,
            CostToProduce = 5.50m,
            Stock = 15
        };
        _mockRepository.GetByIdAsync(5).Returns(groceryItem);

        // Act
        var result = await _service.GetGroceryByIdAsync(5);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.Id);
        await _mockRepository.Received(1).GetByIdAsync(5);
        await _mockRepository.DidNotReceive().GetAllAsync();
    }

    [Fact]
    public async Task UpdateGroceryAsync_WithValidData_MapsAllPropertiesCorrectly()
    {
        // Arrange
        var updateDto = new UpdateGroceryItemDto
        {
            Name = "Comprehensive Update",
            Category = Category.Vegetable,
            Price = 4.75m,
            CostToProduce = 2.38m,
            Stock = 25
        };

        var updatedItem = new GroceryItem
        {
            Id = 10,
            Name = "Comprehensive Update",
            Category = Category.Vegetable,
            Price = 4.75m,
            CostToProduce = 2.38m,
            Stock = 25,
            CreatedUtc = DateTime.UtcNow.AddHours(-2)
        };

        _mockRepository.UpdateAsync(10, Arg.Any<GroceryItem>()).Returns(updatedItem);

        // Act
        var result = await _service.UpdateGroceryAsync(10, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Id);
        Assert.Equal("Comprehensive Update", result.Name);
        Assert.Equal(Category.Vegetable, result.Category);
        Assert.Equal(4.75m, result.Price);
        Assert.Equal(2.38m, result.CostToProduce);
        Assert.Equal(25, result.Stock);
    }

    #endregion

    #region Activity logging

    private static GroceryItem Stored(int id, string name, int stock = 5) => new()
    {
        Id = id,
        Name = name,
        Category = Category.Fruit,
        Price = 2.00m,
        CostToProduce = 1.00m,
        Stock = stock
    };

    [Fact]
    public async Task CreateGroceryAsync_RecordsTheItemInTheActivityLog()
    {
        _mockRepository.CreateAsync(Arg.Any<GroceryItem>()).Returns(call => call.Arg<GroceryItem>());

        await _service.CreateGroceryAsync(new CreateGroceryItemDto
        {
            Name = "Apple",
            Category = Category.Fruit,
            Price = 1.99m,
            CostToProduce = 1.00m,
            Stock = 12
        });

        await _mockActivity.Received(1).RecordItemAsync(
            ActivityKind.Created,
            Arg.Is<GroceryItemDto>(item => item.Name == "Apple"),
            12,
            Arg.Any<string>());
    }

    [Fact]
    public async Task UpdateGroceryAsync_RecordsTheEdit()
    {
        _mockRepository.UpdateAsync(1, Arg.Any<GroceryItem>()).Returns(Stored(1, "Updated"));

        await _service.UpdateGroceryAsync(1, new UpdateGroceryItemDto
        {
            Name = "Updated",
            Category = Category.Fruit,
            Price = 2.00m,
            CostToProduce = 1.00m,
            Stock = 5
        });

        await _mockActivity.Received(1).RecordItemAsync(
            ActivityKind.Updated,
            Arg.Is<GroceryItemDto>(item => item.Name == "Updated"),
            0,
            Arg.Any<string>());
    }

    [Fact]
    public async Task UpdateGroceryAsync_WhenTheItemDoesNotExist_RecordsNothing()
    {
        _mockRepository.UpdateAsync(999, Arg.Any<GroceryItem>()).Returns((GroceryItem?)null);

        await _service.UpdateGroceryAsync(999, new UpdateGroceryItemDto
        {
            Name = "Missing",
            Category = Category.Fruit,
            Price = 2.00m,
            CostToProduce = 1.00m,
            Stock = 5
        });

        await _mockActivity.DidNotReceive().RecordItemAsync(
            Arg.Any<ActivityKind>(),
            Arg.Any<GroceryItemDto>(),
            Arg.Any<int>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task DeleteGroceryAsync_RecordsTheItemByNameBeforeItIsGone()
    {
        _mockRepository.GetByIdAsync(1).Returns(Stored(1, "Apple"));
        _mockRepository.DeleteAsync(1).Returns(true);

        await _service.DeleteGroceryAsync(1);

        await _mockActivity.Received(1).RecordItemAsync(
            ActivityKind.Deleted,
            Arg.Is<GroceryItemDto>(item => item.Name == "Apple"),
            0,
            Arg.Any<string>());
    }

    [Fact]
    public async Task DeleteGroceryAsync_WhenNothingWasDeleted_RecordsNothing()
    {
        _mockRepository.DeleteAsync(404).Returns(false);

        await _service.DeleteGroceryAsync(404);

        await _mockActivity.DidNotReceive().RecordItemAsync(
            Arg.Any<ActivityKind>(),
            Arg.Any<GroceryItemDto>(),
            Arg.Any<int>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task AdjustStockAsync_RecordsTheChangeThatWasAppliedRatherThanTheOneRequested()
    {
        // Asking to remove a hundred units from five leaves five units removed, and that is what the log
        // has to say or the history will not add up to the stock on hand.
        _mockRepository.GetByIdAsync(1).Returns(Stored(1, "Apple", stock: 5));
        _mockRepository.UpdateAsync(1, Arg.Any<GroceryItem>()).Returns(call => call.Arg<GroceryItem>());

        await _service.AdjustStockAsync(1, -100);

        await _mockActivity.Received(1).RecordItemAsync(
            ActivityKind.StockAdjusted,
            Arg.Any<GroceryItemDto>(),
            -5,
            Arg.Any<string>());
    }

    [Fact]
    public async Task AdjustStockAsync_WhenTheItemDoesNotExist_RecordsNothing()
    {
        _mockRepository.GetByIdAsync(999).Returns((GroceryItem?)null);

        await _service.AdjustStockAsync(999, 1);

        await _mockActivity.DidNotReceive().RecordItemAsync(
            Arg.Any<ActivityKind>(),
            Arg.Any<GroceryItemDto>(),
            Arg.Any<int>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ResetDemoDataAsync_ClearsTheLogAsWellAsTheCatalogue()
    {
        await _service.ResetDemoDataAsync();

        await _mockRepository.Received(1).ResetAsync();

        // The old log described items that no longer exist, so it goes with them.
        await _mockActivity.Received(1).ClearAsync();
    }

    #endregion

    #region ApplyRestockAsync

    [Fact]
    public async Task ApplyRestockAsync_AddsStockAndStampsTheRestockTime()
    {
        _mockRepository.GetAllAsync().Returns(new List<GroceryItem>
        {
            Stored(1, "Apple", stock: 5),
            Stored(2, "Brie", stock: 0)
        });

        var restocked = await _service.ApplyRestockAsync(new Dictionary<int, int> { [1] = 10, [2] = 4 });

        Assert.Equal(2, restocked.Count);
        Assert.Equal(15, restocked.Single(item => item.Id == 1).Stock);
        Assert.All(restocked, item => Assert.NotNull(item.LastRestockedUtc));

        await _mockRepository.Received(1).ReplaceAllAsync(Arg.Is<IEnumerable<GroceryItem>>(items =>
            items.Single(item => item.Id == 1).Stock == 15 &&
            items.Single(item => item.Id == 2).Stock == 4));
    }

    [Fact]
    public async Task ApplyRestockAsync_RecordsOneEntryPerRestockedLine()
    {
        _mockRepository.GetAllAsync().Returns(new List<GroceryItem> { Stored(1, "Apple", stock: 5) });

        await _service.ApplyRestockAsync(new Dictionary<int, int> { [1] = 10 });

        await _mockActivity.Received(1).RecordManyAsync(Arg.Is<IEnumerable<ActivityEntry>>(entries =>
            entries.Count() == 1 &&
            entries.Single().Kind == ActivityKind.BulkRestock &&
            entries.Single().StockDelta == 10));
    }

    [Fact]
    public async Task ApplyRestockAsync_IgnoresQuantitiesThatAreNotPositive()
    {
        _mockRepository.GetAllAsync().Returns(new List<GroceryItem> { Stored(1, "Apple", stock: 5) });

        Assert.Empty(await _service.ApplyRestockAsync(new Dictionary<int, int> { [1] = 0 }));
        Assert.Empty(await _service.ApplyRestockAsync(new Dictionary<int, int> { [1] = -5 }));

        await _mockRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IEnumerable<GroceryItem>>());
    }

    [Fact]
    public async Task ApplyRestockAsync_IgnoresItemsThatAreNotInTheCatalogue()
    {
        _mockRepository.GetAllAsync().Returns(new List<GroceryItem> { Stored(1, "Apple") });

        Assert.Empty(await _service.ApplyRestockAsync(new Dictionary<int, int> { [999] = 10 }));
    }

    [Fact]
    public async Task ApplyRestockAsync_WithNoLines_DoesNotReadOrWriteAnything()
    {
        Assert.Empty(await _service.ApplyRestockAsync(new Dictionary<int, int>()));

        await _mockRepository.DidNotReceive().GetAllAsync();
    }

    [Fact]
    public async Task ApplyRestockAsync_WhenTheQuantitiesAreNull_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.ApplyRestockAsync(null!));
    }

    #endregion

    #region Undo tests

    [Fact]
    public async Task DeleteGroceryAsync_IsOneUndoableStepThatNamesTheItem()
    {
        // Arrange
        var item = new GroceryItem { Id = 7, Name = "Conference Pear", Category = Category.Fruit, Price = 1m, CostToProduce = 0.5m, Stock = 4 };
        _mockRepository.GetAllAsync().Returns(new List<GroceryItem> { item });
        _mockRepository.GetByIdAsync(7).Returns(item);
        _mockRepository.DeleteAsync(7).Returns(true);

        // Act
        Assert.True(await _service.DeleteGroceryAsync(7));

        // Assert
        Assert.Equal(1, _history.UndoDepth);
        Assert.Equal("Deleting Conference Pear", _history.NextUndoLabel);
    }

    [Fact]
    public async Task UndoAsync_AfterADelete_PutsTheWholeCatalogueBack()
    {
        // Arrange
        var item = new GroceryItem { Id = 7, Name = "Conference Pear", Category = Category.Fruit, Price = 1m, CostToProduce = 0.5m, Stock = 4 };
        _mockRepository.GetAllAsync().Returns(new List<GroceryItem> { item });
        _mockRepository.GetByIdAsync(7).Returns(item);
        _mockRepository.DeleteAsync(7).Returns(true);

        await _service.DeleteGroceryAsync(7);

        // Act
        Assert.True(await _history.UndoAsync());

        // Assert: the snapshot the step was taken from, written back through the repository.
        await _mockRepository.Received(1).ReplaceAllAsync(
            Arg.Is<IEnumerable<GroceryItem>>(items => items.Any(candidate => candidate.Id == 7)));
    }

    [Fact]
    public async Task CreateGroceryAsync_LeavesOneUndoStepNamedAfterTheItem()
    {
        // Arrange
        _mockRepository.GetAllAsync().Returns(new List<GroceryItem>());
        _mockRepository.CreateAsync(Arg.Any<GroceryItem>()).Returns(call => call.Arg<GroceryItem>());

        // Act
        await _service.CreateGroceryAsync(new CreateGroceryItemDto
        {
            Name = "Conference Pear",
            Category = Category.Fruit,
            Price = 1m,
            CostToProduce = 0.5m,
            Stock = 4
        });

        // Assert
        Assert.Equal(1, _history.UndoDepth);
        Assert.Equal("Adding Conference Pear", _history.NextUndoLabel);
    }

    [Fact]
    public async Task AdjustStockAsync_ByNothing_LeavesNoStepBehind()
    {
        // Arrange
        var item = new GroceryItem { Id = 3, Name = "Apple", Category = Category.Fruit, Price = 1m, CostToProduce = 0.5m, Stock = 5 };
        _mockRepository.GetByIdAsync(3).Returns(item);
        _mockRepository.GetAllAsync().Returns(new List<GroceryItem> { item });

        // Act
        var result = await _service.AdjustStockAsync(3, 0);

        // Assert: an adjustment that changes nothing should not put a step an undo could take back.
        Assert.NotNull(result);
        Assert.Equal(5, result!.Stock);
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public async Task ApplyRestockAsync_InsideAnotherStep_DoesNotAddAStepOfItsOwn()
    {
        // Arrange: what the restock worksheet does, so a whole order is one undo rather than one per line.
        var items = new List<GroceryItem>
        {
            new() { Id = 1, Name = "Apple", Category = Category.Fruit, Price = 1m, CostToProduce = 0.5m, Stock = 2 }
        };
        _mockRepository.GetAllAsync().Returns(items);

        // Act
        await _history.RunAsync(
            "Ordering from the worksheet",
            () => _service.ApplyRestockAsync(new Dictionary<int, int> { [1] = 5 }));

        // Assert
        Assert.Equal(1, _history.UndoDepth);
        Assert.Equal("Ordering from the worksheet", _history.NextUndoLabel);
        Assert.Equal(7, items[0].Stock);
    }

    #endregion

    #region Archive tests

    [Fact]
    public async Task SetArchivedAsync_ArchivesTheItemAndNamesTheStepAfterIt()
    {
        // Arrange
        RepositoryHolds(new GroceryItem { Id = 4, Name = "Brie", Category = Category.Cheese, Price = 8m, CostToProduce = 4m, Stock = 6 });
        _mockRepository.GetByIdAsync(4).Returns(new GroceryItem { Id = 4, Name = "Brie", Category = Category.Cheese, Price = 8m, CostToProduce = 4m, Stock = 6 });

        // Act
        var archived = await _service.SetArchivedAsync(4, archived: true);

        // Assert
        Assert.NotNull(archived);
        Assert.True(archived!.IsArchived);
        Assert.Equal("Archiving Brie", _history.NextUndoLabel);
    }

    [Fact]
    public async Task SetArchivedAsync_Restoring_PutsTheItemBack()
    {
        // Arrange
        RepositoryHolds(new GroceryItem { Id = 4, Name = "Brie", Category = Category.Cheese, Price = 8m, CostToProduce = 4m, Stock = 6, IsArchived = true });
        _mockRepository.GetByIdAsync(4).Returns(new GroceryItem { Id = 4, Name = "Brie", Category = Category.Cheese, Price = 8m, CostToProduce = 4m, Stock = 6, IsArchived = true });

        // Act
        var restored = await _service.SetArchivedAsync(4, archived: false);

        // Assert
        Assert.NotNull(restored);
        Assert.False(restored!.IsArchived);
        Assert.Equal("Restoring Brie", _history.NextUndoLabel);
    }

    [Fact]
    public async Task SetArchivedAsync_WhenTheItemDoesNotExist_ReturnsNullAndRecordsNothing()
    {
        _mockRepository.GetByIdAsync(999).Returns((GroceryItem?)null);

        Assert.Null(await _service.SetArchivedAsync(999, archived: true));
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public async Task SetArchivedAsync_WhenTheItemIsAlreadyInThatState_ChangesNothingAndAddsNoStep()
    {
        // Arrange: the item is already archived, so asking to archive it again is not a change.
        RepositoryHolds(new GroceryItem { Id = 4, Name = "Brie", Category = Category.Cheese, Price = 8m, CostToProduce = 4m, Stock = 6, IsArchived = true });
        _mockRepository.GetByIdAsync(4).Returns(new GroceryItem { Id = 4, Name = "Brie", Category = Category.Cheese, Price = 8m, CostToProduce = 4m, Stock = 6, IsArchived = true });

        // Act
        var unchanged = await _service.SetArchivedAsync(4, archived: true);

        // Assert: the answer still describes the item, but there is nothing an undo could take back.
        Assert.NotNull(unchanged);
        Assert.True(unchanged!.IsArchived);
        Assert.False(_history.CanUndo);
        await _mockRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IEnumerable<GroceryItem>>());
    }

    #endregion

    #region Delete many tests

    [Fact]
    public async Task DeleteManyAsync_RemovesTheItemsAndReportsHowMany()
    {
        // Arrange
        var items = new List<GroceryItem>
        {
            new() { Id = 1, Name = "Apple", Category = Category.Fruit, Price = 1m, CostToProduce = 0.5m, Stock = 3 },
            new() { Id = 2, Name = "Banana", Category = Category.Fruit, Price = 1m, CostToProduce = 0.5m, Stock = 3 },
            new() { Id = 3, Name = "Cheddar", Category = Category.Cheese, Price = 6m, CostToProduce = 3m, Stock = 3 }
        };
        _mockRepository.GetAllAsync().Returns(items);

        // Act
        var removed = await _service.DeleteManyAsync(new[] { 1, 2, 99 });

        // Assert: an identifier that is not in the catalogue is left out of the count and the label.
        Assert.Equal(2, removed);
        Assert.Equal("Deleting 2 items", _history.NextUndoLabel);
        await _mockRepository.Received(1).ReplaceAllAsync(
            Arg.Is<IEnumerable<GroceryItem>>(kept => kept.Count() == 1 && kept.All(item => item.Id == 3)));
    }

    [Fact]
    public async Task DeleteManyAsync_RecordsTheDeletedNamesSoTheTimelineStillReads()
    {
        // Arrange
        var items = new List<GroceryItem>
        {
            new() { Id = 1, Name = "Apple", Category = Category.Fruit, Price = 1m, CostToProduce = 0.5m, Stock = 3 }
        };
        _mockRepository.GetAllAsync().Returns(items);

        // Act
        await _service.DeleteManyAsync(new[] { 1 });

        // Assert
        await _mockActivity.Received(1).RecordManyAsync(
            Arg.Is<IEnumerable<ActivityEntry>>(entries =>
                entries.Any(entry => entry.Kind == ActivityKind.Deleted && entry.ItemName == "Apple")));
    }

    [Fact]
    public async Task DeleteManyAsync_WhenNoneOfTheIdsMatch_DoesNotWriteAnything()
    {
        _mockRepository.GetAllAsync().Returns(new List<GroceryItem>());

        Assert.Equal(0, await _service.DeleteManyAsync(new[] { 42 }));
        await _mockRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IEnumerable<GroceryItem>>());
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public async Task DeleteManyAsync_ForNoItems_DoesNotTouchTheCatalogue()
    {
        Assert.Equal(0, await _service.DeleteManyAsync(Array.Empty<int>()));

        await _mockRepository.DidNotReceive().GetAllAsync();
    }

    [Fact]
    public async Task DeleteManyAsync_WhenTheIdsAreNull_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.DeleteManyAsync((IReadOnlyCollection<int>)null!));
    }

    [Fact]
    public async Task SetArchivedAsync_ForSeveralItems_IsOneStepForTheWholeBatch()
    {
        // Arrange
        var apple = new GroceryItem { Id = 1, Name = "Apple", Category = Category.Fruit, Price = 1m, CostToProduce = 0.5m, Stock = 3 };
        var banana = new GroceryItem { Id = 2, Name = "Banana", Category = Category.Fruit, Price = 1m, CostToProduce = 0.5m, Stock = 3 };
        var cheddar = new GroceryItem { Id = 3, Name = "Cheddar", Category = Category.Cheese, Price = 6m, CostToProduce = 3m, Stock = 3 };
        RepositoryHolds(apple, banana, cheddar);

        // Act
        var changed = await _service.SetArchivedAsync(new[] { 1, 2 }, archived: true);

        // Assert: one write, carrying both rows over and leaving the third alone.
        Assert.Equal(2, changed.Count);
        Assert.Equal(1, _history.UndoDepth);
        Assert.Equal("Archiving 2 items", _history.NextUndoLabel);
        await _mockRepository.Received(1).ReplaceAllAsync(
            Arg.Is<IEnumerable<GroceryItem>>(written =>
                written.Count(item => item.IsArchived) == 2 && written.Count() == 3));
    }

    [Fact]
    public async Task SetArchivedAsync_ForASelectionAlreadyInThatState_ChangesNothing()
    {
        // Arrange: both rows are archived, so asking to archive them again is not a change.
        RepositoryHolds(
            new GroceryItem { Id = 1, Name = "Apple", Category = Category.Fruit, Price = 1m, CostToProduce = 0.5m, Stock = 3, IsArchived = true },
            new GroceryItem { Id = 2, Name = "Banana", Category = Category.Fruit, Price = 1m, CostToProduce = 0.5m, Stock = 3, IsArchived = true });

        // Act
        var changed = await _service.SetArchivedAsync(new[] { 1, 2 }, archived: true);

        // Assert
        Assert.Empty(changed);
        Assert.False(_history.CanUndo);
        await _mockRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IEnumerable<GroceryItem>>());
    }

    [Fact]
    public async Task SetArchivedAsync_ForNoItems_DoesNotTouchTheCatalogue()
    {
        Assert.Empty(await _service.SetArchivedAsync(Array.Empty<int>(), archived: true));

        await _mockRepository.DidNotReceive().GetAllAsync();
    }

    [Fact]
    public async Task SetArchivedAsync_WhenTheIdsAreNull_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SetArchivedAsync((IReadOnlyCollection<int>)null!, archived: true));
    }

    [Fact]
    public async Task SetArchivedAsync_RecordsAnEntryForEachArchivedItem()
    {
        // Arrange
        var items = new List<GroceryItem>
        {
            new() { Id = 1, Name = "Apple", Category = Category.Fruit, Price = 1m, CostToProduce = 0.5m, Stock = 3 }
        };
        _mockRepository.GetAllAsync().Returns(items);

        // Act
        await _service.SetArchivedAsync(new[] { 1 }, archived: true);

        // Assert: the timeline can still name the item, and says which way it went.
        await _mockActivity.Received(1).RecordManyAsync(
            Arg.Is<IEnumerable<ActivityEntry>>(entries =>
                entries.Any(entry => entry.Kind == ActivityKind.Archived && entry.ItemName == "Apple")));
    }

    [Fact]
    public async Task UndoAsync_AfterArchiving_PutsTheItemBackInTheCatalogue()
    {
        // Arrange
        RepositoryHolds(new GroceryItem { Id = 4, Name = "Brie", Category = Category.Cheese, Price = 8m, CostToProduce = 4m, Stock = 6 });
        _mockRepository.GetByIdAsync(4).Returns(new GroceryItem { Id = 4, Name = "Brie", Category = Category.Cheese, Price = 8m, CostToProduce = 4m, Stock = 6 });

        await _service.SetArchivedAsync(4, archived: true);

        // Act
        Assert.True(await _history.UndoAsync());

        // Assert: the snapshot taken before the change, written back through the repository.
        await _mockRepository.Received(1).ReplaceAllAsync(
            Arg.Is<IEnumerable<GroceryItem>>(candidates => candidates.All(candidate => !candidate.IsArchived)));
    }

    #endregion
}