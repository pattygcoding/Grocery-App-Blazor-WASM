using System.ComponentModel.DataAnnotations;

namespace GroceryApp.Models;

public class GroceryItem
{
    public int Id { get; set; }

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

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}