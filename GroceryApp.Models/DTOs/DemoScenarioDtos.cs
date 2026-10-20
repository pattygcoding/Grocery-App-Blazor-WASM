namespace GroceryApp.Models.DTOs;

/// <summary>
/// What happened when the demonstration data was set up.
/// </summary>
public class DemoScenarioResult
{
    /// <summary>True when a history was generated, false when it was deliberately left alone.</summary>
    public bool Applied { get; set; }

    /// <summary>Why it was applied, or why it was not, in one sentence.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>How many movements were written to the activity log.</summary>
    public int EntriesCreated { get; set; }

    /// <summary>How many items had their stock level set by the scenario.</summary>
    public int ItemsAdjusted { get; set; }

    /// <summary>Length of the simulated window in days.</summary>
    public int WindowDays { get; set; }
}
