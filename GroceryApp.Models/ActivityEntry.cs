using System.Text.Json.Serialization;

namespace GroceryApp.Models;

/// <summary>
/// What happened to the catalogue, used for the activity timeline and for the stock movement
/// history that the velocity and days-of-cover calculations read.
/// </summary>
public enum ActivityKind
{
    Created,
    Updated,
    Deleted,
    Archived,
    Restored,
    StockAdjusted,
    BulkRestock,
    PriceChanged,
    Imported,
    Reset,
    Undone,
    Redone
}

/// <summary>
/// One entry in the catalogue's append-only activity log.
/// </summary>
/// <remarks>
/// The log is the only place the application keeps history: the catalogue itself is a current
/// snapshot. Entries are written by the service layer whenever a mutation succeeds, and are
/// capped and pruned so browser storage does not grow without bound.
/// </remarks>
public class ActivityEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public ActivityKind Kind { get; set; }

    /// <summary>Identifier of the item the entry is about, or 0 for catalogue-wide entries.</summary>
    public int ItemId { get; set; }

    /// <summary>
    /// Name of the item at the time of the entry. Stored rather than looked up so the timeline
    /// still reads correctly after an item is renamed or deleted.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Change in units on hand. Positive when stock was added, negative when removed.</summary>
    public int StockDelta { get; set; }

    /// <summary>Human readable detail, for example the new price or the name of the bulk action.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>
    /// True when the entry represents a change in units on hand. Stock movements are what the
    /// sparkline, velocity and days-of-cover figures are built from.
    /// </summary>
    [JsonIgnore]
    public bool IsStockMovement => StockDelta != 0;

    /// <summary>
    /// True for entries generated to give the demo a history rather than recorded from a real
    /// edit. The UI labels these so the seeded trend is never presented as the visitor's own data.
    /// </summary>
    public bool IsSeeded { get; set; }
}
