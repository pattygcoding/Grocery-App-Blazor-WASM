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
}

public class GroceryItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Category Category { get; set; }
    public decimal Price { get; set; }
    public decimal CostToProduce { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedUtc { get; set; }
}