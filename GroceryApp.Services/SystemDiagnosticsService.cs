using GroceryApp.Data;
using GroceryApp.Data.Repositories;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <inheritdoc />
public class SystemDiagnosticsService : ISystemDiagnosticsService
{
    private readonly IAppStateStore _store;
    private readonly IGroceryStore _groceryStore;
    private readonly IGroceryRepository _repository;
    private readonly IActivityService _activity;
    private readonly ICatalogueHistoryService _history;
    private readonly IPreferenceService _preferences;

    /// <summary>
    /// Initializes a new instance of the SystemDiagnosticsService class.
    /// </summary>
    /// <param name="store">The keyed store, read for the keys and their sizes.</param>
    /// <param name="groceryStore">The catalogue's own store, which is measured separately because it is not a keyed payload.</param>
    /// <param name="repository">The catalogue, read for its counts.</param>
    /// <param name="activity">The log, read for its entries.</param>
    /// <param name="history">The undo stack, read for its depth.</param>
    /// <param name="preferences">The preferences in force.</param>
    public SystemDiagnosticsService(
        IAppStateStore store,
        IGroceryStore groceryStore,
        IGroceryRepository repository,
        IActivityService activity,
        ICatalogueHistoryService history,
        IPreferenceService preferences)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _groceryStore = groceryStore ?? throw new ArgumentNullException(nameof(groceryStore));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    /// <inheritdoc />
    public async Task<DiagnosticsReport> CollectAsync()
    {
        await _preferences.LoadAsync();

        var catalogue = (await _repository.GetAllAsync()).ToList();
        var entries = (await _activity.GetAllAsync()).ToList();
        var keys = await _store.KeysAsync();

        var storage = new List<StorageFootprint>();

        foreach (var key in keys)
        {
            // The catalogue carries the application prefix, so the keyed store enumerates it like any other
            // key. It is skipped here and measured through its own store below, which is the thing that knows
            // how big a whole-document payload is - and which reports it even in a setup where the keyed store
            // holds no catalogue at all.
            if (key == AppStateKeys.Catalogue)
            {
                continue;
            }

            var payload = await _store.ReadAsync(key);
            storage.Add(new StorageFootprint
            {
                Key = key,
                What = Describe(key),
                Characters = payload?.Length ?? 0
            });
        }

        storage.Insert(0, new StorageFootprint
        {
            Key = AppStateKeys.Catalogue,
            What = Describe(AppStateKeys.Catalogue),
            Characters = await _groceryStore.SizeAsync()
        });

        return new DiagnosticsReport
        {
            Storage = storage.OrderByDescending(footprint => footprint.Characters).ToList(),
            TotalBytes = storage.Sum(footprint => footprint.Bytes),
            CatalogueCount = catalogue.Count,
            ArchivedCount = catalogue.Count(item => item.IsArchived),
            ActivityEntries = entries.Count,
            GeneratedEntries = entries.Count(entry => entry.IsSeeded),
            EarliestEntryUtc = entries.Count == 0 ? null : entries.Min(entry => entry.TimestampUtc),
            LatestEntryUtc = entries.Count == 0 ? null : entries.Max(entry => entry.TimestampUtc),
            UndoDepth = _history.UndoDepth,
            NextUndoLabel = _history.NextUndoLabel,
            ActivityLogLimit = ActivityService.MaxEntries,
            Preferences = _preferences.Current,

            // Read from the store rather than from the document: the theme is applied by a script before
            // first paint, but the stored choice is what the application itself can see.
            Theme = await ReadThemeAsync()
        };
    }

    /// <summary>
    /// Reads the stored theme choice, which is the resolved theme when one was chosen and "system" when the
    /// browser's own setting is being followed.
    /// </summary>
    private async Task<string> ReadThemeAsync()
    {
        var stored = await _store.ReadAsync(AppStateKeys.Theme);

        return string.IsNullOrWhiteSpace(stored) ? "system" : stored;
    }

    /// <inheritdoc />
    public async Task ClearEverythingAsync()
    {
        // The catalogue first: it has its own key and its own store, so it is not covered by the sweep below.
        await _groceryStore.ClearAsync();

        foreach (var key in await _store.KeysAsync())
        {
            await _store.RemoveAsync(key);
        }

        // Last, and after the removals: this drops the in-memory catalogue and writes the seed data back, so
        // what is left behind is the state a first visit finds rather than an empty one.
        await _repository.ResetAsync();
    }

    /// <summary>
    /// Says what a storage key is for, in the words the diagnostics page uses.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <returns>The description, or a plain answer for a key this build does not know about.</returns>
    private static string Describe(string key) => key switch
    {
        AppStateKeys.Catalogue => "The catalogue: every item, with its stock and price",
        AppStateKeys.Activity => "The activity log: what has happened, and what has sold",
        AppStateKeys.Preferences => "Interface preferences, which a demo reset leaves alone",
        AppStateKeys.SavedViews => "Named sets of catalogue filters",
        AppStateKeys.Favourites => "Items you starred",
        AppStateKeys.RecentlyViewed => "Items you opened most recently",
        AppStateKeys.Suppliers => "The managed list of suppliers",
        AppStateKeys.Theme => "The theme the browser resolved",
        _ => "Stored by an earlier build of this application"
    };
}