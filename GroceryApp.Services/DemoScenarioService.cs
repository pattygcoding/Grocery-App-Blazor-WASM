using GroceryApp.Data.Repositories;
using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Applies the demonstration history to the catalogue.
/// </summary>
/// <remarks>
/// Every refusal to act is reported rather than swallowed, so a page can explain why the dashboard is
/// empty instead of leaving the visitor to guess.
/// </remarks>
public class DemoScenarioService : IDemoScenarioService
{
    private readonly IGroceryRepository _repository;
    private readonly IActivityService _activity;

    /// <summary>
    /// Initializes a new instance of the DemoScenarioService class.
    /// </summary>
    /// <param name="repository">The catalogue the scenario adjusts.</param>
    /// <param name="activity">The log the generated movements are written to.</param>
    public DemoScenarioService(IGroceryRepository repository, IActivityService activity)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
    }

    /// <inheritdoc />
    public async Task<DemoScenarioResult> TryApplyAsync(
        DateTime nowUtc,
        int windowDays = DemoHistoryGenerator.DefaultWindowDays)
    {
        var items = (await _repository.GetAllAsync()).ToList();

        if (items.Count == 0)
        {
            return NotApplied("The catalogue is empty, so there is no trading history to build.");
        }

        if (await _activity.CountAsync() > 0)
        {
            return NotApplied("This catalogue already has a recorded history, so it is left as it is.");
        }

        // Anything with a restock time has been worked on by hand. Rebuilding would overwrite stock levels
        // the visitor set, which is worse than showing an empty chart.
        if (items.Any(item => item.LastRestockedUtc.HasValue))
        {
            return NotApplied("This catalogue has already been worked on, so its stock levels are left alone.");
        }

        return await BuildAsync(items, nowUtc, windowDays);
    }

    /// <inheritdoc />
    public async Task<DemoScenarioResult> RebuildAsync(
        DateTime nowUtc,
        int windowDays = DemoHistoryGenerator.DefaultWindowDays)
    {
        var items = (await _repository.GetAllAsync()).ToList();

        if (items.Count == 0)
        {
            return NotApplied("The catalogue is empty, so there is no trading history to build.");
        }

        await _activity.ClearAsync();

        return await BuildAsync(items, nowUtc, windowDays);
    }

    /// <summary>
    /// Simulates the window and persists both sides of it.
    /// </summary>
    private async Task<DemoScenarioResult> BuildAsync(
        List<GroceryItem> items,
        DateTime nowUtc,
        int windowDays)
    {
        var window = Math.Clamp(windowDays, 7, 365);
        var entries = DemoHistoryGenerator.Generate(items, nowUtc, window);

        // The catalogue is written first. A catalogue whose stock does not match its history makes every
        // figure on the dashboard wrong, whereas a history that is missing its last few entries only loses
        // a little detail, so the write that can leave that state is the one attempted last.
        var stored = await _repository.ReplaceAllAsync(items);
        await _activity.RecordManyAsync(entries);

        var adjusted = stored.Count(item => item.LastRestockedUtc.HasValue);

        return new DemoScenarioResult
        {
            Applied = true,
            EntriesCreated = entries.Count,
            ItemsAdjusted = adjusted,
            WindowDays = window,
            Summary = $"Simulated {entries.Count} movements across {window} days for {adjusted} items."
        };
    }

    private static DemoScenarioResult NotApplied(string reason) => new()
    {
        Applied = false,
        Summary = reason
    };
}
