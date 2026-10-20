using GroceryApp.Models;

namespace GroceryApp.Components.Shared;

/// <summary>
/// Maps a <see cref="Category"/> to its chip style so the colour mapping lives in one place
/// instead of being repeated across the pages.
/// </summary>
internal static class CategoryChip
{
    /// <summary>
    /// Gets the chip class for a category, for example "chip--vegetable".
    /// </summary>
    /// <param name="category">The category to style.</param>
    /// <returns>The chip class name.</returns>
    public static string ClassFor(Category category) => category switch
    {
        Category.Fruit => "chip--fruit",
        Category.Vegetable => "chip--vegetable",
        Category.Meat => "chip--meat",
        Category.Cheese => "chip--cheese",
        Category.Bread => "chip--bread",
        _ => "chip--neutral"
    };
}
