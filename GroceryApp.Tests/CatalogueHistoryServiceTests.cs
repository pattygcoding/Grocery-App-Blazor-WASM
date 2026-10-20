using GroceryApp.Models;

namespace GroceryApp.Tests;

/// <summary>
/// Tests for the undo stack, run against the real repository over an in-memory store so a restore goes
/// through the same write path the application uses.
/// </summary>
public class CatalogueHistoryServiceTests
{
    private readonly InMemoryGroceryStore _store = new();
    private readonly InMemoryAppStateStore _state = new();
    private readonly GroceryRepository _repository;
    private readonly ActivityService _activity;
    private readonly CatalogueHistoryService _history;

    public CatalogueHistoryServiceTests()
    {
        _repository = new GroceryRepository(_store);
        _activity = new ActivityService(new ActivityLogStore(_state));
        _history = new CatalogueHistoryService(_repository, _activity);
    }

    /// <summary>Removes the first item of the catalogue as one undoable step.</summary>
    private Task RemoveFirstItemAsync() =>
        _history.RunAsync("Deleting an item", async () =>
        {
            var items = (await _repository.GetAllAsync()).ToList();
            items.RemoveAt(0);
            await _repository.ReplaceAllAsync(items);
        });

    [Fact]
    public async Task BeforeAnythingHappens_ThereIsNothingToUndoOrRedo()
    {
        await _repository.GetAllAsync();

        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
        Assert.Null(_history.NextUndoLabel);
        Assert.Null(_history.NextRedoLabel);
        Assert.Equal(0, _history.UndoDepth);
    }

    [Fact]
    public async Task RunAsync_ThenUndo_PutsTheCatalogueBackAsItWas()
    {
        var before = (await _repository.GetAllAsync()).ToList();

        await RemoveFirstItemAsync();

        Assert.Equal(49, (await _repository.GetAllAsync()).Count());
        Assert.True(_history.CanUndo);
        Assert.Equal("Deleting an item", _history.NextUndoLabel);

        Assert.True(await _history.UndoAsync());

        var after = (await _repository.GetAllAsync()).ToList();
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(before.Select(item => item.Id).OrderBy(id => id), after.Select(item => item.Id).OrderBy(id => id));
        Assert.False(_history.CanUndo);
        Assert.True(_history.CanRedo);
    }

    [Fact]
    public async Task UndoAsync_RestoresStockLevelsAndNotJustTheListOfItems()
    {
        await _repository.GetAllAsync();

        await _history.RunAsync("Restocking", async () =>
        {
            var items = (await _repository.GetAllAsync()).ToList();
            items.Single(item => item.Id == 1).Stock = 999;
            await _repository.ReplaceAllAsync(items);
        });

        Assert.Equal(999, (await _repository.GetByIdAsync(1))!.Stock);

        await _history.UndoAsync();

        Assert.Equal(10, (await _repository.GetByIdAsync(1))!.Stock);
    }

    [Fact]
    public async Task UndoAsync_WhenThereIsNothingToUndo_ReturnsFalse()
    {
        Assert.False(await _history.UndoAsync());
    }

    [Fact]
    public async Task RedoAsync_AppliesTheChangeAgain()
    {
        await RemoveFirstItemAsync();
        await _history.UndoAsync();

        Assert.True(await _history.RedoAsync());

        Assert.Equal(49, (await _repository.GetAllAsync()).Count());
        Assert.Equal("Deleting an item", _history.NextUndoLabel);
        Assert.False(_history.CanRedo);
    }

    [Fact]
    public async Task RedoAsync_WhenNothingHasBeenUndone_ReturnsFalse()
    {
        await RemoveFirstItemAsync();

        Assert.False(await _history.RedoAsync());
    }

    [Fact]
    public async Task RunAsync_AfterAnUndo_AbandonsTheRedoBranch()
    {
        await RemoveFirstItemAsync();
        await _history.UndoAsync();
        Assert.True(_history.CanRedo);

        // A fresh change means a redo would be applying an old change to a catalogue that has moved on.
        await RemoveFirstItemAsync();

        Assert.False(_history.CanRedo);
        Assert.Null(_history.NextRedoLabel);
    }

    [Fact]
    public async Task RunAsync_WhenTheChangeThrows_RecordsNothingAndLetsTheErrorOut()
    {
        await _repository.GetAllAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _history.RunAsync("A change that fails", () => throw new InvalidOperationException("nope")));

        Assert.False(_history.CanUndo);
        Assert.Equal(0, _history.UndoDepth);
    }

    [Fact]
    public async Task UndoAndRedo_AreRecordedInTheActivityLog()
    {
        await RemoveFirstItemAsync();
        await _history.UndoAsync();
        await _history.RedoAsync();

        var entries = await _activity.GetAllAsync();

        Assert.Contains(entries, entry => entry.Kind == ActivityKind.Undone);
        Assert.Contains(entries, entry => entry.Kind == ActivityKind.Redone);
    }

    [Fact]
    public async Task RunAsync_KeepsTheStepsUpToTheMaximumDepth()
    {
        await _repository.GetAllAsync();

        for (var step = 0; step < CatalogueHistoryService.MaxDepth + 3; step++)
        {
            await _history.RunAsync($"Step {step}", () => Task.CompletedTask);
        }

        Assert.Equal(CatalogueHistoryService.MaxDepth, _history.UndoDepth);
    }

    [Fact]
    public async Task ClearAsync_ForgetsEverything()
    {
        await RemoveFirstItemAsync();
        await _history.UndoAsync();

        await _history.ClearAsync();

        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
    }

    [Fact]
    public async Task Changed_IsRaisedWheneverTheHistoryMoves()
    {
        var notifications = 0;
        _history.Changed += () => notifications++;

        await RemoveFirstItemAsync();
        await _history.UndoAsync();
        await _history.RedoAsync();
        await _history.ClearAsync();

        Assert.Equal(4, notifications);
    }

    [Fact]
    public async Task RunAsync_AChangeMadeInsideAnotherChange_IsPartOfThatStep()
    {
        // What a restock through a service that records its own edits looks like: the inner RunAsync must not
        // add a step, or a twenty line order would take twenty undos.
        await _history.RunAsync("Ordering from the worksheet", async () =>
        {
            await _history.RunAsync("Adding Apple", () => Task.CompletedTask);
            await _history.RunAsync("Adding Brie", () => Task.CompletedTask);
        });

        Assert.Equal(1, _history.UndoDepth);
        Assert.Equal("Ordering from the worksheet", _history.NextUndoLabel);
    }

    [Fact]
    public async Task RunAsync_AChangeMadeInsideAnotherChange_StillRuns()
    {
        var innerRan = false;

        await _history.RunAsync("The outer step", async () =>
        {
            await _history.RunAsync("The inner step", () =>
            {
                innerRan = true;
                return Task.CompletedTask;
            });
        });

        Assert.True(innerRan);
    }

    [Fact]
    public async Task RunAsync_OfAChangeThatReturnsSomething_HandsTheResultBack()
    {
        Assert.Equal(42, await _history.RunAsync("A step that returns", () => Task.FromResult(42)));
    }

    [Fact]
    public void Constructor_WhenTheActivityServiceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CatalogueHistoryService(_repository, null!));
    }

    [Fact]
    public void Constructor_WhenTheRepositoryIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CatalogueHistoryService(null!, _activity));
    }
}
