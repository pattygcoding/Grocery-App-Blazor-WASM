using System.ComponentModel.DataAnnotations;

namespace GroceryApp.Models.DTOs;

public class CreateGroceryItemDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Category Category { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Cost to produce must be greater than 0")]
    public decimal CostToProduce { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative")]
    public int Stock { get; set; } = 10;

    /// <summary>Optional SKU. Generated from the category and the identifier when left blank.</summary>
    [StringLength(32)]
    public string Sku { get; set; } = string.Empty;

    [StringLength(100)]
    public string Supplier { get; set; } = string.Empty;

    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Each;

    [Range(0, int.MaxValue, ErrorMessage = "Reorder point cannot be negative")]
    public int ReorderPoint { get; set; } = 5;

    [Range(0, 100, ErrorMessage = "Target margin must be between 0 and 100")]
    public decimal TargetMarginPercent { get; set; } = 40m;

    /// <summary>Free-form labels, entered as a comma separated list in the editor.</summary>
    public List<string> Tags { get; set; } = new();

    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;
}

public class UpdateGroceryItemDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Category Category { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Cost to produce must be greater than 0")]
    public decimal CostToProduce { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative")]
    public int Stock { get; set; }

    /// <summary>Optional SKU. Generated from the category and the identifier when left blank.</summary>
    [StringLength(32)]
    public string Sku { get; set; } = string.Empty;

    [StringLength(100)]
    public string Supplier { get; set; } = string.Empty;

    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Each;

    [Range(0, int.MaxValue, ErrorMessage = "Reorder point cannot be negative")]
    public int ReorderPoint { get; set; } = 5;

    [Range(0, 100, ErrorMessage = "Target margin must be between 0 and 100")]
    public decimal TargetMarginPercent { get; set; } = 40m;

    /// <summary>Free-form labels, entered as a comma separated list in the editor.</summary>
    public List<string> Tags { get; set; } = new();

    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;
}

public class GroceryItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Category Category { get; set; }
    public decimal Price { get; set; }
    public decimal CostToProduce { get; set; }
    public int Stock { get; set; }

    /// <summary>Stock keeping unit code, always populated once the item is stored.</summary>
    public string Sku { get; set; } = string.Empty;

    public string Supplier { get; set; } = string.Empty;

    public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Each;

    public int ReorderPoint { get; set; }

    public decimal TargetMarginPercent { get; set; }

    public List<string> Tags { get; set; } = new();

    public string Notes { get; set; } = string.Empty;

    public bool IsArchived { get; set; }

    public DateTime? LastRestockedUtc { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>Profit per unit: price minus cost to produce.</summary>
    public decimal Margin => Price - CostToProduce;

    /// <summary>
    /// Margin as a percentage of the price. Zero when the price is zero rather than throwing,
    /// because the editor binds to partially filled forms.
    /// </summary>
    public decimal MarginPercent => Price == 0m ? 0m : Margin / Price * 100m;

    /// <summary>What the units on hand cost to produce.</summary>
    public decimal StockCostValue => Stock * CostToProduce;

    /// <summary>What the units on hand would sell for.</summary>
    public decimal StockRetailValue => Stock * Price;

    /// <summary>Profit tied up in the units on hand.</summary>
    public decimal StockPotentialProfit => Stock * Margin;

    /// <summary>True when the item's actual margin falls short of the margin it is meant to hit.</summary>
    public bool IsBelowTargetMargin => MarginPercent < TargetMarginPercent;

    /// <summary>
    /// Price that would achieve <see cref="TargetMarginPercent"/> at the current cost, or null
    /// when the target is 100% or more and no finite price can reach it.
    /// </summary>
    public decimal? PriceForTargetMargin => TargetMarginPercent >= 100m
        ? null
        : decimal.Round(CostToProduce / (1m - TargetMarginPercent / 100m), 2, MidpointRounding.AwayFromZero);
}