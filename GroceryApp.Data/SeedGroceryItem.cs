namespace GroceryApp.Data;

public class SeedGroceryItem
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal CostToProduce { get; set; }
    public int Stock { get; set; }
}