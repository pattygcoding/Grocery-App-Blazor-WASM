namespace GroceryApp.Models.DTOs;

/// <summary>What running a command actually does.</summary>
public enum CommandKind
{
    /// <summary>Goes to one of the application's own pages.</summary>
    Navigation,

    /// <summary>Opens one catalogue item.</summary>
    Item,

    /// <summary>Does something to the catalogue rather than going anywhere.</summary>
    Action
}

/// <summary>
/// One thing the command palette can offer.
/// </summary>
/// <remarks>
/// Deliberately data rather than a delegate: it keeps the index buildable and searchable without a browser,
/// and lets the same list be grouped, ordered and tested as plain values. The component turns the command
/// it is given back into navigation or a named action.
/// </remarks>
public class Command
{
    /// <summary>Stable identifier, used to keep the selected row steady while the results change.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>What the visitor sees first.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Second line: an item's code and category, or where a page sits.</summary>
    public string Subtitle { get; set; } = string.Empty;

    /// <summary>Heading the command is listed under.</summary>
    public string Group { get; set; } = string.Empty;

    public CommandKind Kind { get; set; }

    /// <summary>Where a navigation or item command goes.</summary>
    public string? Href { get; set; }

    /// <summary>Which action an action command is, for example "undo" or "reset".</summary>
    public string? ActionKey { get; set; }
}
