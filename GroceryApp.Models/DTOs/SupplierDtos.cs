using GroceryApp.Models;

namespace GroceryApp.Models.DTOs;

/// <summary>
/// What one supplier is worth to the catalogue.
/// </summary>
public class SupplierSummary
{
    /// <summary>The supplier's name, or a placeholder when the items record none.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>True for the group holding items that name no supplier.</summary>
    public bool IsUnassigned => Name == UnassignedName;

    /// <summary>The name the unassigned group is shown under.</summary>
    public const string UnassignedName = "(no supplier)";

    /// <summary>Every item bought from them, archived ones included.</summary>
    public int ItemCount { get; set; }

    /// <summary>How many of those are archived, so the value figures can be read correctly.</summary>
    public int ArchivedCount { get; set; }

    /// <summary>Units on hand across their active items.</summary>
    public int UnitsHeld { get; set; }

    /// <summary>What their share of the stock would sell for.</summary>
    public decimal ValueAtRetail { get; set; }

    /// <summary>What it cost to put that stock on the shelf.</summary>
    public decimal ValueAtCost { get; set; }

    /// <summary>What is left after the cost, across their active items.</summary>
    public decimal PotentialProfit => ValueAtRetail - ValueAtCost;

    /// <summary>Margin across the value held, rather than an average of the items' own margins.</summary>
    public decimal MarginPercent => ValueAtRetail == 0m ? 0m : (ValueAtRetail - ValueAtCost) / ValueAtRetail * 100m;

    /// <summary>How many of their items are at or below the level they are reordered at.</summary>
    public int NeedsOrderingCount { get; set; }

    /// <summary>How many of their items have nothing left.</summary>
    public int OutOfStockCount { get; set; }

    /// <summary>The categories they supply, which is what makes a supplier worth having.</summary>
    public List<Category> Categories { get; set; } = new();

    /// <summary>Share of the catalogue's retail value, as a percentage.</summary>
    public decimal ShareOfCatalogueValue { get; set; }

    /// <summary>Their active items, most valuable first, for the drill-down.</summary>
    public List<GroceryItemDto> Items { get; set; } = new();
}

/// <summary>
/// The catalogue grouped by who it is bought from.
/// </summary>
public class SupplierAnalysis
{
    /// <summary>The suppliers, largest share of the value first.</summary>
    public List<SupplierSummary> Suppliers { get; set; } = new();

    /// <summary>Retail value of everything counted, so each supplier's share has a denominator.</summary>
    public decimal TotalValueAtRetail { get; set; }

    /// <summary>How many items name no supplier, which is the one data problem this page exposes.</summary>
    public int UnassignedItemCount { get; set; }

    /// <summary>Suppliers that would need a phone call today, ordered by how much is missing.</summary>
    public List<SupplierSummary> NeedingOrders { get; set; } = new();
}