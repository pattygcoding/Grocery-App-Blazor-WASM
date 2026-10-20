using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Filters the activity log and arranges it into days for the timeline.
/// </summary>
/// <remarks>
/// The log is the one place in the application that can grow without bound, so reading it back is a query
/// rather than a list: what matched, what was left out, and how the entries fall into days are all worked
/// out in one pass here rather than in the page.
/// </remarks>
public interface IActivityQueryService
{
    /// <summary>
    /// Runs a query over a log.
    /// </summary>
    /// <param name="entries">The entries to query, in any order.</param>
    /// <param name="filter">What to show. A null filter shows everything.</param>
    /// <param name="maxEntries">The most entries to return, newest first.</param>
    /// <returns>The matching entries arranged by day, with the counts that describe the result.</returns>
    ActivityQueryResult Query(
        IEnumerable<ActivityEntry> entries,
        ActivityFilter? filter = null,
        int maxEntries = 400);
}