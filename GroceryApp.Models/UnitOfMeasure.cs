namespace GroceryApp.Models;

/// <summary>
/// Unit that an item's price and stock count refer to.
/// </summary>
/// <remarks>
/// Kept as an enum rather than free text so the unit can be rendered consistently, compared in
/// bulk operations and included in the price advisor's output without parsing.
/// </remarks>
public enum UnitOfMeasure
{
    /// <summary>Counted individually (a loaf, a jar, a carton).</summary>
    Each,

    Kilogram,
    Gram,
    Litre,
    Millilitre,

    /// <summary>Sold as a sealed multipack.</summary>
    Pack,

    /// <summary>Sold by the single loaf.</summary>
    Loaf,

    /// <summary>Sold as a bundle of stems.</summary>
    Bunch,

    /// <summary>Sold in half-dozen or dozen lots.</summary>
    Dozen
}
