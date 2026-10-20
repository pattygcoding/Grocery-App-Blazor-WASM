using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Reads a pasted or uploaded catalogue, says what applying it would do, and applies it on request.
/// </summary>
/// <remarks>
/// Splitting "say what it would do" from "do it" is the whole point: a file can be wrong in ways the entry
/// form cannot be, and an import can change every item at once. The preview is therefore the product, and the
/// apply is a second, deliberate step.
/// </remarks>
public interface IInventoryImportService
{
    /// <summary>
    /// Reads text and judges every row against the current catalogue, without writing anything.
    /// </summary>
    /// <param name="text">The pasted or uploaded text.</param>
    /// <param name="format">How to read it.</param>
    /// <param name="existing">The catalogue to judge the rows against.</param>
    /// <returns>What would happen, row by row, or the reason nothing could be read.</returns>
    ImportPreview Preview(string? text, ImportFormat format, IEnumerable<GroceryItemDto>? existing);

    /// <summary>
    /// Writes the readable rows of a preview, as a single step the visitor can undo.
    /// </summary>
    /// <param name="preview">A preview produced by <see cref="Preview"/>.</param>
    /// <param name="updateExisting">
    /// Whether rows matching an existing item should change it. When false they are left alone.
    /// </param>
    /// <returns>How many items were created and how many were changed.</returns>
    Task<ImportResult> ApplyAsync(ImportPreview preview, bool updateExisting);

    /// <summary>
    /// A small example of the format, for the empty state to show.
    /// </summary>
    string Sample(ImportFormat format);
}

/// <summary>What an applied import did.</summary>
/// <param name="Created">How many items were added.</param>
/// <param name="Updated">How many existing items were changed.</param>
public readonly record struct ImportResult(int Created, int Updated)
{
    public int Total => Created + Updated;
}
