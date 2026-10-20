using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Tests;

/// <summary>
/// Tests for the command palette's contents and its ranking. What a palette gets wrong is usually the order
/// it puts results in, so most of these are about order.
/// </summary>
public class CommandIndexServiceTests
{
    private readonly CommandIndexService _palette = new();

    private static Command Cmd(string title, string subtitle = "", string group = "Test") => new()
    {
        Id = title.ToLowerInvariant().Replace(' ', '-'),
        Title = title,
        Subtitle = subtitle,
        Group = group,
        Kind = CommandKind.Action,
        ActionKey = "test"
    };

    private static GroceryItemDto Item(int id, string name) => new()
    {
        Id = id,
        Name = name,
        Sku = $"FRU-{id:D4}",
        Category = Category.Fruit,
        Price = 1m,
        CostToProduce = 0.5m,
        Stock = 7
    };

    #region Build

    [Fact]
    public void Build_OffersEveryPageTheApplicationHas()
    {
        var commands = _palette.Build(null);

        var hrefs = commands
            .Where(command => command.Kind == CommandKind.Navigation)
            .Select(command => command.Href)
            .ToList();

        Assert.Contains("/", hrefs);
        Assert.Contains("/dashboard", hrefs);
        Assert.Contains("/groceries", hrefs);
        Assert.Contains("/restock", hrefs);
        Assert.Contains("/reports", hrefs);
        Assert.Contains("/groceries/create", hrefs);
    }

    [Fact]
    public void Build_GivesEveryCommandAUniqueIdentifier()
    {
        var commands = _palette.Build(new[] { Item(1, "Apple"), Item(2, "Brie") }, canUndo: true, canRedo: true);

        var ids = commands.Select(command => command.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Build_WhenTheCatalogueIsNull_StillOffersThePagesAndActions()
    {
        var commands = _palette.Build(null);

        Assert.NotEmpty(commands);
        Assert.All(commands, command => Assert.NotEqual(CommandKind.Item, command.Kind));
        Assert.Contains(commands, command => command.ActionKey == "reset");
    }

    [Fact]
    public void Build_OffersUndoAndRedoOnlyWhenTheyArePossible()
    {
        var neither = _palette.Build(null);
        var both = _palette.Build(null, canUndo: true, canRedo: true);

        Assert.DoesNotContain(neither, command => command.ActionKey == "undo");
        Assert.DoesNotContain(neither, command => command.ActionKey == "redo");
        Assert.Contains(both, command => command.ActionKey == "undo");
        Assert.Contains(both, command => command.ActionKey == "redo");
    }

    [Fact]
    public void Build_ListsTheCatalogueAfterThePagesAndActions()
    {
        var commands = _palette.Build(new[] { Item(1, "Apple"), Item(2, "Brie") });

        var itemCommands = commands.Where(command => command.Kind == CommandKind.Item).ToList();

        Assert.Equal(new[] { "Apple", "Brie" }, itemCommands.Select(command => command.Title));
        Assert.Equal(itemCommands[0].Id, commands[^2].Id);
        Assert.Equal(itemCommands[1].Id, commands[^1].Id);
    }

    [Fact]
    public void Build_CarriesTheCodeCategoryAndStockIntoTheItemsSubtitle()
    {
        var command = _palette
            .Build(new[] { Item(3, "Apple") })
            .Single(candidate => candidate.Kind == CommandKind.Item);

        Assert.Equal("FRU-0003 · Fruit · 7 in stock", command.Subtitle);
        Assert.Equal("/groceries/3", command.Href);
    }

    #endregion

    #region Search

    [Fact]
    public void Search_WhenNothingWasTyped_ReturnsTheCommandsInTheirNaturalOrder()
    {
        var commands = new[] { Cmd("One"), Cmd("Two"), Cmd("Three") };

        Assert.Equal(new[] { "One", "Two" }, _palette.Search(commands, "  ", limit: 2).Select(c => c.Title));
    }

    [Fact]
    public void Search_WhenNothingMatches_ReturnsNothing()
    {
        Assert.Empty(_palette.Search(new[] { Cmd("Restock"), Cmd("Reports") }, "zzzz"));
    }

    [Fact]
    public void Search_IsCaseInsensitive()
    {
        var found = _palette.Search(new[] { Cmd("Restock") }, "RESTOCK");

        Assert.Equal("Restock", Assert.Single(found).Title);
    }

    [Fact]
    public void Search_PrefersTheStartOfTheTitleOverTheMiddleOfIt()
    {
        // "Stock control" starts with the term; the other two only contain it.
        var commands = new[] { Cmd("Restock"), Cmd("Restocking worksheet"), Cmd("Stock control") };

        var ranked = _palette.Search(commands, "stock");

        Assert.Equal(
            new[] { "Stock control", "Restock", "Restocking worksheet" },
            ranked.Select(command => command.Title));
    }

    [Fact]
    public void Search_PrefersTheStartOfAWordOverTheMiddleOfOne()
    {
        var commands = new[] { Cmd("Reordering report"), Cmd("Order history") };

        Assert.Equal("Order history", _palette.Search(commands, "order")[0].Title);
    }

    [Fact]
    public void Search_FallsBackToTheSubtitleWhenTheTitleDoesNotMatch()
    {
        var commands = new[]
        {
            Cmd("Dashboard", "What the stock is worth and what has been moving"),
            Cmd("Reports", "Profit and price bands")
        };

        Assert.Equal("Dashboard", Assert.Single(_palette.Search(commands, "moving")).Title);
    }

    [Fact]
    public void Search_RequiresEveryTermToMatchSoASecondWordNarrowsTheResults()
    {
        var commands = new[]
        {
            Cmd("Bulk Cheddar", "Dairy Co"),
            Cmd("Bulk Bread", "Bakery")
        };

        Assert.Equal(2, _palette.Search(commands, "bulk").Count);
        Assert.Equal("Bulk Cheddar", Assert.Single(_palette.Search(commands, "bulk dairy")).Title);
    }

    [Fact]
    public void Search_OrdersCommandsThatScoreTheSameByTitle()
    {
        var ranked = _palette.Search(new[] { Cmd("Pear"), Cmd("Peach") }, "pea");

        Assert.Equal(new[] { "Peach", "Pear" }, ranked.Select(command => command.Title));
    }

    [Fact]
    public void Search_RespectsTheLimit()
    {
        var commands = new[] { Cmd("One"), Cmd("Two"), Cmd("Three") };

        Assert.Equal(2, _palette.Search(commands, null, limit: 2).Count);
        Assert.Empty(_palette.Search(commands, null, limit: 0));
    }

    [Fact]
    public void Search_FindsAnItemByItsCode()
    {
        var commands = _palette.Build(new[] { Item(12, "Conference Pear") });

        Assert.Equal("Conference Pear", Assert.Single(_palette.Search(commands, "FRU-0012")).Title);
    }

    [Fact]
    public void Search_WhenTheCommandsAreNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _palette.Search(null!, "apple"));
    }

    #endregion
}
