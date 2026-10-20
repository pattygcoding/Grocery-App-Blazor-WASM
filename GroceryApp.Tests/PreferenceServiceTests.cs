namespace GroceryApp.Tests;

/// <summary>
/// Tests for the interface preferences: they default sensibly, survive a read, and announce a change so the
/// pages that follow them can react.
/// </summary>
public class PreferenceServiceTests
{
    private readonly InMemoryAppStateStore _state = new();
    private readonly PreferenceService _preferences;

    public PreferenceServiceTests()
    {
        _preferences = new PreferenceService(new UserPreferencesStore(_state));
    }

    [Fact]
    public void BeforeAnythingIsRead_TheDefaultsAreInForce()
    {
        Assert.False(_preferences.IsLoaded);
        Assert.Equal("/", _preferences.Current.LandingPage);
        Assert.Equal(TableDensity.Comfortable, _preferences.Current.Density);
        Assert.True(_preferences.Current.ConfirmBeforeDelete);
        Assert.False(_preferences.Current.ShowArchived);
    }

    [Fact]
    public async Task LoadAsync_WhenNothingIsStored_HandsBackTheDefaultsAndMarksItselfLoaded()
    {
        var loaded = await _preferences.LoadAsync();

        Assert.True(_preferences.IsLoaded);
        Assert.Equal(TableDensity.Comfortable, loaded.Density);
        Assert.Same(loaded, _preferences.Current);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_ReadsBackWhatWasStored()
    {
        await _preferences.SaveAsync(new UserPreferences
        {
            Density = TableDensity.Compact,
            ShowArchived = true,
            ConfirmBeforeDelete = false,
            LandingPage = "/dashboard"
        });

        // A second service over the same store is what a reload looks like.
        var reloaded = new PreferenceService(new UserPreferencesStore(_state));
        var loaded = await reloaded.LoadAsync();

        Assert.Equal(TableDensity.Compact, loaded.Density);
        Assert.True(loaded.ShowArchived);
        Assert.False(loaded.ConfirmBeforeDelete);
        Assert.Equal("/dashboard", loaded.LandingPage);
    }

    [Fact]
    public async Task LoadAsync_CalledTwice_ReadsTheStoreOnce()
    {
        await _preferences.LoadAsync();

        var counts = new CountingStore(_state);
        var service = new PreferenceService(new UserPreferencesStore(counts));

        await service.LoadAsync();
        await service.LoadAsync();
        await service.LoadAsync();

        Assert.Equal(1, counts.Reads);
    }

    [Fact]
    public async Task SaveAsync_AnnouncesTheChange()
    {
        var notifications = 0;
        _preferences.Changed += () => notifications++;

        await _preferences.SaveAsync(new UserPreferences { Density = TableDensity.Compact });

        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task SaveAsync_WhenThePreferencesAreNull_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _preferences.SaveAsync(null!));
    }

    [Fact]
    public async Task ResetAsync_RemovesTheStoredPayloadAndRestoresTheDefaults()
    {
        await _preferences.SaveAsync(new UserPreferences { Density = TableDensity.Compact, LandingPage = "/reports" });

        var restored = await _preferences.ResetAsync();

        Assert.Equal(TableDensity.Comfortable, restored.Density);
        Assert.Equal("/", restored.LandingPage);
        Assert.Null(await _state.ReadAsync(AppStateKeys.Preferences));
    }

    [Fact]
    public async Task LoadAsync_WhenTheStoredPayloadIsUnreadable_FallsBackToTheDefaults()
    {
        await _state.WriteAsync(AppStateKeys.Preferences, "{ this is not json");

        var loaded = await _preferences.LoadAsync();

        Assert.Equal(TableDensity.Comfortable, loaded.Density);
    }

    /// <summary>Wraps a store to count how often it is read, which is what the cache is for.</summary>
    private sealed class CountingStore : IAppStateStore
    {
        private readonly IAppStateStore _inner;

        public CountingStore(IAppStateStore inner) => _inner = inner;

        public int Reads { get; private set; }

        public Task<string?> ReadAsync(string key)
        {
            Reads++;
            return _inner.ReadAsync(key);
        }

        public Task WriteAsync(string key, string value) => _inner.WriteAsync(key, value);

        public Task RemoveAsync(string key) => _inner.RemoveAsync(key);

        public Task<IReadOnlyList<string>> KeysAsync() => _inner.KeysAsync();
    }
}