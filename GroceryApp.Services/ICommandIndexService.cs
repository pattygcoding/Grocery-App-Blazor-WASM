using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Builds the list of things the command palette offers, and searches it.
/// </summary>
/// <remarks>
/// Kept out of the component so the ranking can be reasoned about and tested on its own: what a palette
/// gets wrong is almost always the order it puts results in, not the markup it draws them with.
/// </remarks>
public interface ICommandIndexService
{
    /// <summary>
    /// Builds the palette's commands from the application's pages and a catalogue.
    /// </summary>
    /// <param name="items">The catalogue to offer as item commands.</param>
    /// <param name="canUndo">Whether an undo is currently possible, which decides whether it is offered.</param>
    /// <param name="canRedo">Whether a redo is currently possible.</param>
    /// <returns>The commands, pages and actions first, then the items.</returns>
    IReadOnlyList<Command> Build(
        IEnumerable<GroceryItemDto>? items,
        bool canUndo = false,
        bool canRedo = false);

    /// <summary>
    /// Searches the commands for a query.
    /// </summary>
    /// <param name="commands">The commands to search.</param>
    /// <param name="query">What was typed. Empty returns the commands in their natural order.</param>
    /// <param name="limit">How many results to return at most.</param>
    /// <returns>The best matches first, or an empty list when nothing matches.</returns>
    IReadOnlyList<Command> Search(IEnumerable<Command> commands, string? query, int limit = 12);
}
