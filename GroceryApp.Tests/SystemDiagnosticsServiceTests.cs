using GroceryApp.Data.State;

namespace GroceryApp.Tests;

/// <summary>
/// Tests for the page that answers "where is my data?": it reports what this browser is holding, and can put
/// it all back.
/// </summary>
public class SystemDiagnosticsServiceTests
{
    private readonly InMemoryAppStateStore _state = new();
    private readonly InMemoryGroceryStore _catalogue = new();
    private readonly GroceryRepository _repository;
    private readonly ActivityService _activity;
    private readonly PreferenceService _preferences;
    private readonly CatalogueHistoryService _history;
    private readonly SystemDiagnosticsService _diagnostics;

    public SystemDiagnosticsServiceTests()
    {
        _repository = new GroceryRepository(_catalogue);
        _activity = new ActivityService(new ActivityLogStore(_state));
        _preferences = new PreferenceService(new UserPreferencesStore(_state));
        _history = new CatalogueHistoryService(_repository, _activity);

        _diagnostics = new SystemDiagnosticsService(_state, _catalogue, _repository, _activity, _history, _preferences);
    }

    [Fact]
    public async Task CollectAsync_ReportsTheCatalogueAndTheLog()
    {
        await _activity.RecordCatalogueAsync(ActivityKind.Reset, "Restored the demo data");

        var report = await _diagnostics.CollectAsync();

        Assert.True(report.CatalogueCount > 0);
        Assert.Equal(0, report.ArchivedCount);
        Assert.Equal(1, report.ActivityEntries);
        Assert.Equal(ActivityService.MaxEntries, report.ActivityLogLimit);
    }

    [Fact]
    public async Task CollectAsync_MeasuresWhatEachKeyIsHolding()
    {
        await _preferences.LoadAsync();
        await _preferences.SaveAsync(new UserPreferences { Density = TableDensity.Compact });

        var report = await _diagnostics.CollectAsync();

        var preferences = report.Storage.Single(footprint => footprint.Key == AppStateKeys.Preferences);

        Assert.False(preferences.IsEmpty);
        Assert.Contains("preferences", preferences.What, StringComparison.OrdinalIgnoreCase);
        Assert.True(preferences.Bytes > 0);
        Assert.Equal(report.Storage.Sum(footprint => footprint.Bytes), report.TotalBytes);
    }

    [Fact]
    public async Task CollectAsync_ListsTheCatalogueOnceEvenWhenTheKeyedStoreAlsoHoldsItsKey()
    {
        // The catalogue carries the application prefix, so a keyed store backed by the browser's own storage
        // enumerates it. It must still be reported once, and by the store that owns it.
        await _state.WriteAsync(AppStateKeys.Catalogue, "[]");

        var report = await _diagnostics.CollectAsync();

        Assert.Equal(1, report.Storage.Count(footprint => footprint.Key == AppStateKeys.Catalogue));
        Assert.Equal(AppStateKeys.Catalogue, report.Storage[0].Key);
    }

    [Fact]
    public async Task CollectAsync_SaysWhatEachKeyIsFor()
    {
        await _activity.RecordCatalogueAsync(ActivityKind.Reset, "Restored");

        var report = await _diagnostics.CollectAsync();

        var catalogue = report.Storage.Single(footprint => footprint.Key == AppStateKeys.Catalogue);

        Assert.Contains("catalogue", catalogue.What, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CollectAsync_ReportsWhatTheNextUndoWouldPutBack()
    {
        await _history.RunAsync("Deleting an item", () => Task.CompletedTask);

        var report = await _diagnostics.CollectAsync();

        Assert.Equal(1, report.UndoDepth);
        Assert.Equal("Deleting an item", report.NextUndoLabel);
    }

    [Fact]
    public async Task CollectAsync_ReportsThePreferencesInForce()
    {
        await _preferences.SaveAsync(new UserPreferences { Density = TableDensity.Compact, LandingPage = "/reports" });

        var report = await _diagnostics.CollectAsync();

        Assert.Equal(TableDensity.Compact, report.Preferences.Density);
        Assert.Equal("/reports", report.Preferences.LandingPage);
    }

    [Fact]
    public async Task CollectAsync_WhenNoThemeWasChosen_ReportsTheSystemRatherThanNothing()
    {
        var report = await _diagnostics.CollectAsync();

        Assert.Equal("system", report.Theme);
    }

    [Fact]
    public async Task CollectAsync_ReportsWhetherTheLogHasReachedItsLimit()
    {
        var report = await _diagnostics.CollectAsync();

        Assert.False(report.LogIsFull);
    }

    [Fact]
    public async Task ClearEverythingAsync_RemovesEveryKeyTheApplicationOwns()
    {
        await _preferences.SaveAsync(new UserPreferences { Density = TableDensity.Compact });
        await _activity.RecordCatalogueAsync(ActivityKind.Reset, "Restored");

        await _diagnostics.ClearEverythingAsync();

        Assert.Empty(await _state.KeysAsync());
    }

    [Fact]
    public async Task ClearEverythingAsync_LeavesASeededCatalogueRatherThanAnEmptyOne()
    {
        await _diagnostics.ClearEverythingAsync();

        var report = await _diagnostics.CollectAsync();

        // A first visit finds the seeded catalogue, so clearing everything has to leave the application in
        // that state rather than in one with nothing in it.
        Assert.True(report.CatalogueCount > 0);
        Assert.Equal(0, report.ActivityEntries);
    }

    [Fact]
    public void Constructor_WhenAStoreIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemDiagnosticsService(
            null!, _catalogue, _repository, _activity, _history, _preferences));
    }

    [Fact]
    public void Constructor_WhenTheCatalogueStoreIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemDiagnosticsService(
            _state, null!, _repository, _activity, _history, _preferences));
    }
}