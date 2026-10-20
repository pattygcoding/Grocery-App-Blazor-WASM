using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Puts the demo catalogue into a state worth looking at.
/// </summary>
/// <remarks>
/// The seeded catalogue ships with fifty items and no history, which leaves the dashboard with nothing to
/// report. This service simulates a plausible few weeks of trading once, so the trend, velocity, dead
/// stock and reorder figures all have something real to be calculated from.
/// </remarks>
public interface IDemoScenarioService
{
    /// <summary>
    /// Builds the demonstration history if the catalogue is untouched and has no history yet.
    /// </summary>
    /// <param name="nowUtc">The moment the simulated window ends, supplied rather than read from the clock.</param>
    /// <param name="windowDays">Length of the simulated window in days.</param>
    /// <returns>A task that represents the asynchronous operation. The task result says whether it was applied, and why not when it was not.</returns>
    /// <remarks>
    /// Safe to call on every start: it declines once a catalogue has any history, or any item that has been
    /// restocked by hand, because overwriting stock levels the visitor set would be worse than an empty
    /// chart.
    /// </remarks>
    Task<DemoScenarioResult> TryApplyAsync(DateTime nowUtc, int windowDays = DemoHistoryGenerator.DefaultWindowDays);

    /// <summary>
    /// Discards the recorded history and rebuilds the demonstration from the catalogue as it stands.
    /// </summary>
    /// <param name="nowUtc">The moment the simulated window ends.</param>
    /// <param name="windowDays">Length of the simulated window in days.</param>
    /// <returns>A task that represents the asynchronous operation. The task result describes what was generated.</returns>
    /// <remarks>
    /// Used by the demo data reset, where the visitor has explicitly asked for the catalogue to be put back
    /// into a presentable state.
    /// </remarks>
    Task<DemoScenarioResult> RebuildAsync(DateTime nowUtc, int windowDays = DemoHistoryGenerator.DefaultWindowDays);
}
