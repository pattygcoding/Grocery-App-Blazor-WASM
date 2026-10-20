using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Tests;

/// <summary>
/// Tests for reading and writing a catalogue filter as a query string, which is what lets a narrowed
/// catalogue be bookmarked, shared and saved as a named view.
/// </summary>
public class InventoryFilterQueryTests
{
    [Fact]
    public void ToQueryString_WhenTheFilterMatchesEverything_IsEmpty()
    {
        Assert.Equal(string.Empty, InventoryFilterQuery.ToQueryString(new InventoryFilter()));
    }

    [Fact]
    public void ToQueryString_LeavesTheDefaultOrderingOutOfTheAddress()
    {
        Assert.Equal(string.Empty, InventoryFilterQuery.ToQueryString(new InventoryFilter
        {
            SortBy = InventorySortField.Name,
            Direction = SortDirection.Ascending
        }));
    }

    [Fact]
    public void Parse_WhenTheQueryStringIsEmptyOrMissing_MatchesEverything()
    {
        foreach (var query in new[] { null, string.Empty, "   ", "?" })
        {
            var filter = InventoryFilterQuery.Parse(query);

            Assert.False(filter.IsNarrowing);
            Assert.Equal(InventorySortField.Name, filter.SortBy);
            Assert.Equal(SortDirection.Ascending, filter.Direction);
        }
    }

    [Fact]
    public void ToQueryString_ThenParse_RoundTripsEveryControl()
    {
        var original = new InventoryFilter
        {
            SearchText = "bulk cheese",
            Categories = new List<Category> { Category.Fruit, Category.Cheese },
            Suppliers = new List<string> { "Acme", "Dairy Co" },
            Tags = new List<string> { "chilled", "bulk" },
            MinimumPrice = 1.5m,
            MaximumPrice = 12.75m,
            MaximumMarginPercent = 32.5m,
            Stock = StockFilter.AtOrBelowReorderPoint,
            AbcClass = AbcClass.B,
            IncludeArchived = true,
            FavouritesOnly = true,
            SortBy = InventorySortField.MarginPercent,
            Direction = SortDirection.Descending
        };

        var restored = InventoryFilterQuery.Parse(InventoryFilterQuery.ToQueryString(original));

        Assert.Equal(original.SearchText, restored.SearchText);
        Assert.Equal(original.Categories, restored.Categories);
        Assert.Equal(original.Suppliers, restored.Suppliers);
        Assert.Equal(original.Tags, restored.Tags);
        Assert.Equal(original.MinimumPrice, restored.MinimumPrice);
        Assert.Equal(original.MaximumPrice, restored.MaximumPrice);
        Assert.Equal(original.MaximumMarginPercent, restored.MaximumMarginPercent);
        Assert.Equal(original.Stock, restored.Stock);
        Assert.Equal(original.AbcClass, restored.AbcClass);
        Assert.Equal(original.IncludeArchived, restored.IncludeArchived);
        Assert.Equal(original.FavouritesOnly, restored.FavouritesOnly);
        Assert.Equal(original.SortBy, restored.SortBy);
        Assert.Equal(original.Direction, restored.Direction);
    }

    [Fact]
    public void Parse_AcceptsAQueryStringWithALeadingQuestionMark()
    {
        Assert.Equal("cheese", InventoryFilterQuery.Parse("?q=cheese").SearchText);
    }

    [Fact]
    public void Parse_EscapesAndUnescapesSpaces()
    {
        var query = InventoryFilterQuery.ToQueryString(new InventoryFilter { SearchText = "bulk cheese" });

        Assert.Equal("q=bulk%20cheese", query);
        Assert.Equal("bulk cheese", InventoryFilterQuery.Parse(query).SearchText);
    }

    [Fact]
    public void Parse_KeepsACommaInsideAValueThatWasEscaped()
    {
        var query = InventoryFilterQuery.ToQueryString(new InventoryFilter
        {
            Suppliers = new List<string> { "Acme, Inc" }
        });

        // The comma belongs to the supplier name, so it must survive the round trip as one entry rather
        // than becoming two suppliers.
        Assert.Equal(new[] { "Acme, Inc" }, InventoryFilterQuery.Parse(query).Suppliers);
    }

    [Fact]
    public void Parse_IsCaseInsensitiveAboutKeysAndValues()
    {
        var filter = InventoryFilterQuery.Parse("CAT=fruit&STOCK=outofstock&SORT=margin&DIR=DESC&ARCHIVED=TRUE");

        Assert.Equal(new[] { Category.Fruit }, filter.Categories);
        Assert.Equal(StockFilter.OutOfStock, filter.Stock);
        Assert.Equal(InventorySortField.Margin, filter.SortBy);
        Assert.Equal(SortDirection.Descending, filter.Direction);
        Assert.True(filter.IncludeArchived);
    }

    [Fact]
    public void Parse_ToleratesAnUnknownColumnName()
    {
        // A link written before a column was renamed still has to open the page.
        Assert.Equal(InventorySortField.Name, InventoryFilterQuery.Parse("sort=somethingelse").SortBy);
    }

    [Fact]
    public void Parse_ToleratesACategoryThatNoLongerExists()
    {
        // A category that has been removed from the enum is dropped; the rest of the link still works.
        var filter = InventoryFilterQuery.Parse("cat=Fruit,Unobtainium");

        Assert.Equal(new[] { Category.Fruit }, filter.Categories);
    }

    [Fact]
    public void Parse_ToleratesAValueThatIsNotANumber()
    {
        var filter = InventoryFilterQuery.Parse("min=cheap&max=2.5");

        Assert.Null(filter.MinimumPrice);
        Assert.Equal(2.5m, filter.MaximumPrice);
    }

    [Fact]
    public void Parse_ToleratesAnUnknownAbcBand()
    {
        Assert.Null(InventoryFilterQuery.Parse("abc=Z").AbcClass);
        Assert.Equal(AbcClass.A, InventoryFilterQuery.Parse("abc=A").AbcClass);
    }

    [Fact]
    public void Parse_IgnoresPairsThatCarryNoValue()
    {
        var filter = InventoryFilterQuery.Parse("archived&q=");

        Assert.False(filter.IncludeArchived);
        Assert.True(string.IsNullOrEmpty(filter.SearchText));
    }
}
