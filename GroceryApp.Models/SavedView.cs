using System.ComponentModel.DataAnnotations;

namespace GroceryApp.Models;

/// <summary>
/// A named set of catalogue filters the visitor has saved.
/// </summary>
/// <remarks>
/// The filter state itself lives in the query string, so a saved view is nothing more than that
/// query string plus a label. That keeps the catalogue's filters shareable and bookmarkable
/// whether or not they were ever saved.
/// </remarks>
public class SavedView
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Query string of the catalogue, without a leading '?' (for example "q=cheese&amp;cat=Fruit").</summary>
    public string QueryString { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
