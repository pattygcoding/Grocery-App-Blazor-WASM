using System.ComponentModel.DataAnnotations;

namespace GroceryApp.Models;

/// <summary>
/// A single catalogue line: what the item is, what it costs, what it sells for and how much of
/// it is on hand.
/// </summary>
public class GroceryItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Stock keeping unit code. Generated from the category and the identifier whenever an item
    /// is stored without one, so every line has a stable, human readable handle.
    /// </summary>
    [StringLength(32)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    public Category Category { get; set; }

    /// <summary>
    /// Name of the supplier the item is bought from.
    /// </summary>
    /// <remarks>
    /// Deliberately free text rather than a foreign key. The catalogue lives in the visitor's
    /// browser, so there is no database to enforce a relationship, and an item has to stay valid
    /// when its supplier is renamed or removed.
    /// </remarks>
    [StringLength(100)]
    public string Supplier { get; set; } = string.Empty;

    /// <summary>Unit that <see cref="Price"/> and <see cref="Stock"/> refer to.</summary>
    [Required]
    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Each;

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Cost to produce must be greater than 0")]
    public decimal CostToProduce { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative")]
    public int Stock { get; set; } = 10;

    /// <summary>
    /// Stock level at or below which the item is treated as needing a reorder. Drives the restock
    /// worksheet and the low stock highlighting.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Reorder point cannot be negative")]
    public int ReorderPoint { get; set; } = 5;

    /// <summary>
    /// Margin the item is meant to achieve, as a percentage of its price. The price advisor
    /// compares the item's actual margin against this figure.
    /// </summary>
    [Range(0, 100, ErrorMessage = "Target margin must be between 0 and 100")]
    public decimal TargetMarginPercent { get; set; } = 40m;

    /// <summary>Free-form labels used for search, saved views and bulk tagging.</summary>
    public List<string> Tags { get; set; } = new();

    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Archived items stay in storage - so their stock and their history survive - but are hidden
    /// from the catalogue, the reports and the dashboards unless archived items are asked for.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// When stock was last added to this line. Used for the velocity and days-of-cover
    /// calculations, and left alone by ordinary edits.
    /// </summary>
    public DateTime? LastRestockedUtc { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}