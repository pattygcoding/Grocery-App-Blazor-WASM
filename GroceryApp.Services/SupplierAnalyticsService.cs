using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <inheritdoc />
public class SupplierAnalyticsService : ISupplierAnalyticsService
{
    /// <inheritdoc />
    public SupplierAnalysis Summarise(IEnumerable<GroceryItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var catalogue = items.ToList();

        var suppliers = catalogue
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Supplier)
                ? SupplierSummary.UnassignedName
                : item.Supplier.Trim())
            .Select(group => Build(group.Key, group.ToList()))
            .ToList();

        var total = suppliers.Sum(supplier => supplier.ValueAtRetail);

        foreach (var supplier in suppliers)
        {
            supplier.ShareOfCatalogueValue = total == 0m ? 0m : supplier.ValueAtRetail / total * 100m;
        }

        var ordered = suppliers
            .OrderByDescending(supplier => supplier.ValueAtRetail)
            .ThenBy(supplier => supplier.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SupplierAnalysis
        {
            Suppliers = ordered,
            TotalValueAtRetail = total,
            UnassignedItemCount = catalogue.Count(item => string.IsNullOrWhiteSpace(item.Supplier) && !item.IsArchived),
            NeedingOrders = ordered
                .Where(supplier => supplier.NeedsOrderingCount > 0)
                .OrderByDescending(supplier => supplier.OutOfStockCount)
                .ThenByDescending(supplier => supplier.NeedsOrderingCount)
                .ToList()
        };
    }

    /// <summary>
    /// Builds one supplier's summary from the items that name them.
    /// </summary>
    /// <param name="name">The supplier's name, or the placeholder for the unassigned group.</param>
    /// <param name="items">The items that belong to them.</param>
    /// <returns>The summary.</returns>
    private static SupplierSummary Build(string name, List<GroceryItemDto> items)
    {
        // Archived items are counted but excluded from every figure about stock, so a supplier's value cannot
        // be inflated by lines that are not in the catalogue any more.
        var active = items.Where(item => !item.IsArchived).ToList();

        return new SupplierSummary
        {
            Name = name,
            ItemCount = items.Count,
            ArchivedCount = items.Count(item => item.IsArchived),
            UnitsHeld = active.Sum(item => item.Stock),
            ValueAtRetail = active.Sum(item => item.StockRetailValue),
            ValueAtCost = active.Sum(item => item.CostToProduce * item.Stock),
            NeedsOrderingCount = active.Count(item => item.Stock <= item.ReorderPoint),
            OutOfStockCount = active.Count(item => item.Stock == 0),
            Categories = active
                .Select(item => item.Category)
                .Distinct()
                .OrderBy(category => category)
                .ToList(),
            Items = active
                .OrderByDescending(item => item.StockRetailValue)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }
}