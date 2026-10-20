using GroceryApp.Data.State;
using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Reads and writes the activity log.
/// </summary>
/// <remarks>
/// <para>
/// The log is cached in memory for the session, like the catalogue, and is kept in chronological order
/// oldest first. That invariant is what makes "the recent past" a slice from the end and pruning a slice
/// from the start, so a batch of entries is merged by timestamp rather than appended.
/// </para>
/// <para>
/// Pruning is by count rather than by age: a quiet catalogue keeps its history, a busy one keeps a
/// bounded amount of it.
/// </para>
/// </remarks>
public class ActivityService : IActivityService
{
    /// <summary>
    /// How many entries are kept before the oldest are dropped.
    /// </summary>
    /// <remarks>
    /// Sized against browser storage rather than by taste: an entry costs roughly 200 bytes once
    /// serialised, so 3000 entries is about 600 KB, which leaves the catalogue and everything else the
    /// application stores comfortably inside a 5 MB budget.
    /// </remarks>
    public const int MaxEntries = 3000;

    private readonly ActivityLogStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private List<ActivityEntry>? _entries;

    /// <summary>
    /// Initializes a new instance of the ActivityService class.
    /// </summary>
    /// <param name="store">The typed store the log is persisted through.</param>
    public ActivityService(ActivityLogStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityEntry>> GetAllAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var entries = await EnsureLoadedCoreAsync();

            var newestFirst = new List<ActivityEntry>(entries);
            newestFirst.Reverse();
            return newestFirst;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityEntry>> GetRecentAsync(int count)
    {
        var recent = await GetAllAsync();

        return count <= 0
            ? Array.Empty<ActivityEntry>()
            : recent.Take(count).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityEntry>> GetBetweenAsync(DateTime fromUtc, DateTime toUtc)
    {
        await _gate.WaitAsync();
        try
        {
            var entries = await EnsureLoadedCoreAsync();

            return entries
                .Where(entry => entry.TimestampUtc >= fromUtc && entry.TimestampUtc <= toUtc)
                .Reverse()
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public Task RecordItemAsync(ActivityKind kind, GroceryItemDto item, int stockDelta = 0, string detail = "")
    {
        ArgumentNullException.ThrowIfNull(item);

        return AppendManyAsync(new[]
        {
            new ActivityEntry
            {
                Kind = kind,
                ItemId = item.Id,
                ItemName = item.Name,
                StockDelta = stockDelta,
                Detail = detail
            }
        });
    }

    /// <inheritdoc />
    public Task RecordCatalogueAsync(ActivityKind kind, string detail) =>
        AppendManyAsync(new[] { new ActivityEntry { Kind = kind, Detail = detail } });

    /// <inheritdoc />
    public Task RecordManyAsync(IEnumerable<ActivityEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return AppendManyAsync(entries);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return (await EnsureLoadedCoreAsync()).Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _entries = null;
            await _store.ClearAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Merges entries into the log, keeping it in chronological order, and writes it back once.
    /// </summary>
    private async Task AppendManyAsync(IEnumerable<ActivityEntry> additions)
    {
        var batch = additions.ToList();

        if (batch.Count == 0)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            var entries = await EnsureLoadedCoreAsync();

            foreach (var entry in batch)
            {
                // Inserting each entry after the last one that is not newer than it keeps the log ordered
                // whatever order the batch arrives in, including a batch whose entries share a timestamp.
                var insertAfter = entries.FindLastIndex(existing => existing.TimestampUtc <= entry.TimestampUtc);
                entries.Insert(insertAfter + 1, entry);
            }

            if (entries.Count > MaxEntries)
            {
                entries.RemoveRange(0, entries.Count - MaxEntries);
            }

            await _store.SaveAsync(entries);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Loads the log on first use. The caller must already hold the gate.
    /// </summary>
    private async Task<List<ActivityEntry>> EnsureLoadedCoreAsync()
    {
        if (_entries is not null)
        {
            return _entries;
        }

        var stored = await _store.LoadAsync();

        _entries = stored ?? new List<ActivityEntry>();

        // A payload written elsewhere, or restored from a backup, need not be ordered.
        _entries.Sort((left, right) => left.TimestampUtc.CompareTo(right.TimestampUtc));

        return _entries;
    }
}
