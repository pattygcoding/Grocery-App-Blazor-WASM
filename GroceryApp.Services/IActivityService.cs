using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Records what happens to the catalogue and reads it back.
/// </summary>
/// <remarks>
/// The log is the application's only history: the catalogue itself is a current snapshot. Everything
/// that needs a sense of time - the velocity figures, the activity timeline, how much moved this week -
/// is derived from these entries, so a mutation that is not recorded is a period the application will
/// always believe nothing happened in.
/// </remarks>
public interface IActivityService
{
    /// <summary>
    /// Reads the whole log, newest first.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the recorded entries.</returns>
    Task<IReadOnlyList<ActivityEntry>> GetAllAsync();

    /// <summary>
    /// Reads the most recent entries.
    /// </summary>
    /// <param name="count">How many entries to return at most.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the newest entries.</returns>
    Task<IReadOnlyList<ActivityEntry>> GetRecentAsync(int count);

    /// <summary>
    /// Reads the entries recorded inside a window.
    /// </summary>
    /// <param name="fromUtc">Start of the window, inclusive.</param>
    /// <param name="toUtc">End of the window, inclusive.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the matching entries, newest first.</returns>
    Task<IReadOnlyList<ActivityEntry>> GetBetweenAsync(DateTime fromUtc, DateTime toUtc);

    /// <summary>
    /// Records something that happened to one item.
    /// </summary>
    /// <param name="kind">What happened.</param>
    /// <param name="item">The item it happened to.</param>
    /// <param name="stockDelta">Change in units on hand, when the change moved stock.</param>
    /// <param name="detail">Human readable detail for the timeline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RecordItemAsync(ActivityKind kind, GroceryItemDto item, int stockDelta = 0, string detail = "");

    /// <summary>
    /// Records something that happened to the catalogue rather than to one item, such as restoring the
    /// demo data or importing a file.
    /// </summary>
    /// <param name="kind">What happened.</param>
    /// <param name="detail">Human readable detail for the timeline.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RecordCatalogueAsync(ActivityKind kind, string detail);

    /// <summary>
    /// Records a batch of entries that already carry their own timestamps.
    /// </summary>
    /// <param name="entries">The entries to append.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// Used where a whole history arrives at once - building the demonstration data or importing a
    /// backup - rather than recording one event at a time. The batch is merged into the log in
    /// chronological order, so the log's ordering invariant holds however the batch is supplied.
    /// </remarks>
    Task RecordManyAsync(IEnumerable<ActivityEntry> entries);

    /// <summary>
    /// Counts the recorded entries.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entry count.</returns>
    Task<int> CountAsync();

    /// <summary>
    /// Removes every recorded entry.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ClearAsync();
}
