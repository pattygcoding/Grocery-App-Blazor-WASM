namespace GroceryApp.Models;

/// <summary>How tightly the catalogue tables are packed.</summary>
public enum TableDensity
{
    Comfortable,
    Compact
}

/// <summary>
/// Visitor preferences that are not part of the catalogue.
/// </summary>
/// <remarks>
/// Stored separately from the catalogue so resetting the demo data never resets the way the
/// application looks or behaves.
/// </remarks>
public class UserPreferences
{
    /// <summary>Route the sidebar brand and the post-reset redirect land on.</summary>
    public string LandingPage { get; set; } = "/";

    public TableDensity Density { get; set; } = TableDensity.Comfortable;

    /// <summary>
    /// When true, destructive actions ask for confirmation first. Bulk deletes always confirm,
    /// because a single click there can remove many rows at once.
    /// </summary>
    public bool ConfirmBeforeDelete { get; set; } = true;

    /// <summary>
    /// Visitor-level request to suppress the interface's own transitions, on top of the operating
    /// system's reduced-motion setting.
    /// </summary>
    public bool ReduceMotion { get; set; }

    /// <summary>Whether archived items are included in the catalogue and reports by default.</summary>
    public bool ShowArchived { get; set; }
}
