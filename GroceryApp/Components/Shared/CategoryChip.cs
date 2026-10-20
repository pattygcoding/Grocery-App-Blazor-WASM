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

    /// <summary>
    /// Gets the chart colour class for a category, for example "chart--vegetable".
    /// </summary>
    /// <param name="category">The category to colour.</param>
    /// <returns>The chart class name.</returns>
    /// <remarks>
    /// Kept apart from <see cref="ClassFor"/> rather than reusing it: a chip paints a background, while a
    /// ring segment or a bar needs the ink colour on its own, and the two would fight over the same
    /// property.
    /// </remarks>
    public static string ChartClassFor(Category category) => category switch
    {
        Category.Fruit => "chart--fruit",
        Category.Vegetable => "chart--vegetable",
        Category.Meat => "chart--meat",
        Category.Cheese => "chart--cheese",
        Category.Bread => "chart--bread",
        _ => "chart--neutral"
    };
}
