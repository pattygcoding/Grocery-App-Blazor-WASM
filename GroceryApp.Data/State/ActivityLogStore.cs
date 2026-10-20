using GroceryApp.Models;

namespace GroceryApp.Data.State;

/// <summary>
/// Stores the activity log: the append-only history of what happened to the catalogue.
/// </summary>
/// <remarks>
/// The whole log is one payload rather than one record per key. That keeps the number of browser
/// storage writes proportional to the number of edits rather than the number of entries, and lets
/// the log be pruned from the front in a single write.
/// </remarks>
public sealed class ActivityLogStore : JsonAppStateStore<List<ActivityEntry>>
{
    /// <summary>
    /// Initializes a new instance of the ActivityLogStore class.
    /// </summary>
    /// <param name="store">The raw keyed store to read and write through.</param>
    public ActivityLogStore(IAppStateStore store)
        : base(store, AppStateKeys.Activity, AppStateJsonContext.Default.ListActivityEntry)
    {
    }
}
