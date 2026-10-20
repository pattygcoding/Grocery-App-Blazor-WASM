namespace GroceryApp.Tests;

/// <summary>
/// Tests for the keyed store that holds everything the application remembers beside the catalogue:
/// the activity log, interface preferences, saved views and the pinned item lists.
/// </summary>
public class AppStateStoreTests
{
    #region Raw store behaviour

    [Fact]
    public async Task ReadAsync_WhenKeyIsNotSet_ReturnsNull()
    {
        var store = new InMemoryAppStateStore();

        Assert.Null(await store.ReadAsync(AppStateKeys.Preferences));
    }

    [Fact]
    public async Task WriteAsync_ThenReadAsync_RoundTripsTheValue()
    {
        var store = new InMemoryAppStateStore();

        await store.WriteAsync(AppStateKeys.Preferences, "{}");

        Assert.Equal("{}", await store.ReadAsync(AppStateKeys.Preferences));
    }

    [Fact]
    public async Task WriteAsync_ForTheSameKeyTwice_ReplacesThePreviousValue()
    {
        var store = new InMemoryAppStateStore();

        await store.WriteAsync(AppStateKeys.Activity, "[]");
        await store.WriteAsync(AppStateKeys.Activity, "[1]");

        Assert.Equal("[1]", await store.ReadAsync(AppStateKeys.Activity));
    }

    [Fact]
    public async Task RemoveAsync_ForgetsOnlyThatKey()
    {
        var store = new InMemoryAppStateStore();
        await store.WriteAsync(AppStateKeys.Activity, "[]");
        await store.WriteAsync(AppStateKeys.Preferences, "{}");

        await store.RemoveAsync(AppStateKeys.Activity);

        Assert.Null(await store.ReadAsync(AppStateKeys.Activity));
        Assert.Equal("{}", await store.ReadAsync(AppStateKeys.Preferences));
    }

    [Fact]
    public async Task KeysAsync_ReturnsTheApplicationKeysInOrdinalOrder()
    {
        var store = new InMemoryAppStateStore();
        await store.WriteAsync(AppStateKeys.Preferences, "{}");
        await store.WriteAsync(AppStateKeys.Activity, "[]");

        var keys = await store.KeysAsync();

        Assert.Equal(new[] { AppStateKeys.Activity, AppStateKeys.Preferences }, keys);
    }

    [Fact]
    public async Task KeysAsync_IgnoresKeysTheApplicationDoesNotOwn()
    {
        var store = new InMemoryAppStateStore();
        await store.WriteAsync("someotherapp.key", "value");
        await store.WriteAsync(AppStateKeys.Preferences, "{}");

        var keys = await store.KeysAsync();

        Assert.Equal(new[] { AppStateKeys.Preferences }, keys);
    }

    [Fact]
    public async Task KeysAsync_WhenNothingIsStored_ReturnsEmpty()
    {
        var store = new InMemoryAppStateStore();

        Assert.Empty(await store.KeysAsync());
    }

    #endregion

    #region User preferences

    [Fact]
    public async Task Preferences_WhenNothingIsStored_ReportsNothingStored()
    {
        var store = new UserPreferencesStore(new InMemoryAppStateStore());

        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public async Task Preferences_RoundTripsEveryFieldIncludingTheDensityName()
    {
        var raw = new InMemoryAppStateStore();
        var store = new UserPreferencesStore(raw);

        await store.SaveAsync(new UserPreferences
        {
            LandingPage = "/restock",
            Density = TableDensity.Compact,
            ConfirmBeforeDelete = false,
            ReduceMotion = true,
            ShowArchived = true
        });

        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("/restock", loaded!.LandingPage);
        Assert.Equal(TableDensity.Compact, loaded.Density);
        Assert.False(loaded.ConfirmBeforeDelete);
        Assert.True(loaded.ReduceMotion);
        Assert.True(loaded.ShowArchived);

        // Enums are written by name, so a stored payload stays readable and survives reordering.
        Assert.Contains("\"density\":\"Compact\"", raw.Values[AppStateKeys.Preferences]);
    }

    [Fact]
    public async Task Preferences_WhenThePayloadIsCorrupt_ReportsNothingStoredInsteadOfThrowing()
    {
        var raw = new InMemoryAppStateStore();
        await raw.WriteAsync(AppStateKeys.Preferences, "{ not json");
        var store = new UserPreferencesStore(raw);

        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public async Task Preferences_ClearAsync_RemovesTheStoredPayload()
    {
        var raw = new InMemoryAppStateStore();
        var store = new UserPreferencesStore(raw);
        await store.SaveAsync(new UserPreferences());

        await store.ClearAsync();

        Assert.Null(await raw.ReadAsync(AppStateKeys.Preferences));
    }

    [Fact]
    public async Task Preferences_SizeAsync_ReportsTheStoredPayloadLength()
    {
        var raw = new InMemoryAppStateStore();
        var store = new UserPreferencesStore(raw);
        await store.SaveAsync(new UserPreferences());

        var expected = (await raw.ReadAsync(AppStateKeys.Preferences))!.Length;

        Assert.Equal(expected, await store.SizeAsync());
        Assert.True(expected > 0);
    }

    [Fact]
    public async Task Preferences_SizeAsync_WhenNothingIsStored_IsZero()
    {
        var store = new UserPreferencesStore(new InMemoryAppStateStore());

        Assert.Equal(0, await store.SizeAsync());
    }

    #endregion

    #region Activity log

    [Fact]
    public async Task ActivityLog_RoundTripsEntriesIncludingTheActivityKind()
    {
        var raw = new InMemoryAppStateStore();
        var store = new ActivityLogStore(raw);
        var timestamp = new DateTime(2025, 3, 14, 9, 26, 53, DateTimeKind.Utc);

        await store.SaveAsync(new List<ActivityEntry>
        {
            new()
            {
                TimestampUtc = timestamp,
                Kind = ActivityKind.StockAdjusted,
                ItemId = 12,
                ItemName = "Conference Pear",
                StockDelta = -3,
                Detail = "Stock corrected after stocktake"
            }
        });

        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        var entry = Assert.Single(loaded!);
        Assert.Equal(timestamp, entry.TimestampUtc);
        Assert.Equal(ActivityKind.StockAdjusted, entry.Kind);
        Assert.Equal(12, entry.ItemId);
        Assert.Equal("Conference Pear", entry.ItemName);
        Assert.Equal(-3, entry.StockDelta);
        Assert.True(entry.IsStockMovement);
        Assert.Contains("\"kind\":\"StockAdjusted\"", raw.Values[AppStateKeys.Activity]);
    }

    [Fact]
    public async Task ActivityLog_DoesNotRoundTripTheDerivedStockMovementFlag()
    {
        var raw = new InMemoryAppStateStore();
        var store = new ActivityLogStore(raw);

        await store.SaveAsync(new List<ActivityEntry> { new() { StockDelta = 4 } });

        // The flag is derived from StockDelta, so it must not be written out as well.
        Assert.DoesNotContain("isStockMovement", raw.Values[AppStateKeys.Activity]);
        Assert.True((await store.LoadAsync())![0].IsStockMovement);
    }

    [Fact]
    public async Task ActivityLog_WhenNothingIsStored_ReportsNothingStored()
    {
        var store = new ActivityLogStore(new InMemoryAppStateStore());

        Assert.Null(await store.LoadAsync());
    }

    #endregion

    #region Saved views and pinned items

    [Fact]
    public async Task SavedViews_RoundTripsTheStoredFilters()
    {
        var store = new SavedViewStore(new InMemoryAppStateStore());

        await store.SaveAsync(new List<SavedView>
        {
            new() { Id = "abc123", Name = "Cheese to restock", QueryString = "cat=Cheese&sort=stock&dir=asc" }
        });

        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        var view = Assert.Single(loaded!);
        Assert.Equal("abc123", view.Id);
        Assert.Equal("Cheese to restock", view.Name);
        Assert.Equal("cat=Cheese&sort=stock&dir=asc", view.QueryString);
    }

    [Fact]
    public async Task PinnedItemLists_AreStoredUnderSeparateKeys()
    {
        var raw = new InMemoryAppStateStore();
        var favourites = new FavouriteItemStore(raw);
        var recentlyViewed = new RecentlyViewedStore(raw);

        await favourites.SaveAsync(new List<int> { 3, 9 });
        await recentlyViewed.SaveAsync(new List<int> { 9, 3, 41 });

        var loadedFavourites = await favourites.LoadAsync();
        var loadedRecent = await recentlyViewed.LoadAsync();

        Assert.Equal(new[] { 3, 9 }, loadedFavourites);
        Assert.Equal(new[] { 9, 3, 41 }, loadedRecent);
        Assert.Equal(new[] { AppStateKeys.Favourites, AppStateKeys.RecentlyViewed }, await raw.KeysAsync());
    }

    #endregion
}
