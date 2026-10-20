namespace GroceryApp.Models.DTOs;

/// <summary>
/// One key in browser storage, with what it is holding.
/// </summary>
public class StorageFootprint
{
    /// <summary>The storage key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>What the key is for, in the words the diagnostics page uses.</summary>
    public string What { get; set; } = string.Empty;

    /// <summary>Size of the stored payload in characters.</summary>
    public int Characters { get; set; }

    /// <summary>True when the key is holding nothing, which is not an error but is worth showing.</summary>
    public bool IsEmpty => Characters == 0;

    /// <summary>
    /// Roughly what the key occupies in bytes. Storage is measured in characters and browsers count
    /// UTF-16 code units, so two bytes each is the closest honest figure without reading the quota API.
    /// </summary>
    public int Bytes => Characters * 2;
}

/// <summary>
/// What the application currently has stored, and what state it is in.
/// </summary>
public class DiagnosticsReport
{
    public List<StorageFootprint> Storage { get; set; } = new();

    public int TotalBytes { get; set; }

    public int CatalogueCount { get; set; }

    public int ArchivedCount { get; set; }

    public int ActivityEntries { get; set; }

    public int GeneratedEntries { get; set; }

    public DateTime? EarliestEntryUtc { get; set; }

    public DateTime? LatestEntryUtc { get; set; }

    /// <summary>How many undo steps are available right now.</summary>
    public int UndoDepth { get; set; }

    /// <summary>What the next undo would put back, or null.</summary>
    public string? NextUndoLabel { get; set; }

    /// <summary>How many recorded entries the log is allowed to hold before it starts dropping the oldest.</summary>
    public int ActivityLogLimit { get; set; }

    /// <summary>The theme the browser has resolved, as stored by the pre-paint script.</summary>
    public string Theme { get; set; } = "system";

    /// <summary>The preferences in force.</summary>
    public UserPreferences Preferences { get; set; } = new();

    /// <summary>True when the log has reached its cap, so the oldest entries are being dropped.</summary>
    public bool LogIsFull => ActivityLogLimit > 0 && ActivityEntries >= ActivityLogLimit;
}