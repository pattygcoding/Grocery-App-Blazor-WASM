using System.Linq.Expressions;

namespace GroceryApp.Tests;

public class GroceryServiceTests
{
    private readonly IGroceryRepository _mockRepository;
    private readonly IGroceryService _service;

    public GroceryServiceTests()
    {
        _mockRepository = Substitute.For<IGroceryRepository>();
        _service = new GroceryService(_mockRepository);
    }

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
    public async Task DeleteGroceryAsync_CallsRepositoryOnlyOnce()
    {
        // Arrange
        _mockRepository.DeleteAsync(1).Returns(true);

        // Act
        await _service.DeleteGroceryAsync(1);

        // Assert
        await _mockRepository.Received(1).DeleteAsync(1);
        await _mockRepository.DidNotReceive().GetByIdAsync(Arg.Any<int>());
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
}