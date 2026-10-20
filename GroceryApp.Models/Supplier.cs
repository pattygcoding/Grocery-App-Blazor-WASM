using System.ComponentModel.DataAnnotations;

namespace GroceryApp.Models;

/// <summary>
/// A supplier the catalogue is bought from.
/// </summary>
/// <remarks>
/// Items reference a supplier by <see cref="GroceryItem.Supplier"/> name rather than by
/// identifier, so this list is a managed set of known trading partners rather than a parent
/// record: renaming one here does not silently rewrite, or break, the items that mention it.
/// </remarks>
public class Supplier
{
    public int Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Name of the person to contact on orders.</summary>
    [StringLength(100)]
    public string ContactName { get; set; } = string.Empty;

    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [StringLength(50)]
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Working days between placing an order and receiving it. Feeds the reorder point
    /// calculation on the restock worksheet.
    /// </summary>
    [Range(0, 365, ErrorMessage = "Lead time must be between 0 and 365 days")]
    public int LeadTimeDays { get; set; } = 3;

    /// <summary>Order value the supplier requires before they will ship.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Minimum order value cannot be negative")]
    public decimal MinimumOrderValue { get; set; }

    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
