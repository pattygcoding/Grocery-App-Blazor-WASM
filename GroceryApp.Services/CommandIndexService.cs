using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// The palette's contents and its search.
/// </summary>
/// <remarks>
/// Ranking is deliberately simple and explainable: a term matching the start of the title beats one matching
/// the start of a word, which beats one appearing in the middle, which beats one only appearing in the
/// subtitle. Every term has to match something, so a second word narrows the results rather than widening them.
/// </remarks>
public class CommandIndexService : ICommandIndexService
{
    /// <inheritdoc />
    public IReadOnlyList<Command> Build(
        IEnumerable<GroceryItemDto>? items,
        bool canUndo = false,
        bool canRedo = false)
    {
        // Pages and actions come first: an empty palette should be a map of the application rather than a
        // list of whatever happens to be in the catalogue.
        var commands = new List<Command>
        {
            Page("dashboard", "Dashboard", "What the stock is worth and what has been moving"),
            Page("groceries", "Groceries", "The catalogue, with filters and sorting"),
            Page("restock", "Restock", "What to order, and why"),
            Page("activity", "Activity", "Everything that has happened to the catalogue, day by day"),
            Page("suppliers", "Suppliers", "Who the catalogue is bought from"),
            Page("import", "Import", "Load a catalogue from a CSV or JSON file, with a preview first"),
            Page("preferences", "Preferences", "How the catalogue looks and behaves"),
            Page("about", "About", "What this is, and what it has stored in this browser"),
            Page("reports", "Reports", "Profit, stock and price bands, and the Excel export"),
            Page("home", "Home", "The front page and the demo data reset", "/"),
            new()
            {
                Id = "action-new-item",
                Title = "Add an item",
                Subtitle = "Open the form for a new catalogue line",
                Group = "Do",
                Kind = CommandKind.Navigation,
                Href = "/groceries/create"
            }
        };

        if (canUndo)
        {
            commands.Add(new Command
            {
                Id = "action-undo",
                Title = "Undo",
                Subtitle = "Put the catalogue back the way it was",
                Group = "Do",
                Kind = CommandKind.Action,
                ActionKey = "undo"
            });
        }

        if (canRedo)
        {
            commands.Add(new Command
            {
                Id = "action-redo",
                Title = "Redo",
                Subtitle = "Apply the change that was undone",
                Group = "Do",
                Kind = CommandKind.Action,
                ActionKey = "redo"
            });
        }

        commands.Add(new Command
        {
            Id = "action-reset",
            Title = "Restore the demo data",
            Subtitle = "Back to the seeded fifty items, with a fresh history",
            Group = "Do",
            Kind = CommandKind.Action,
            ActionKey = "reset"
        });

        foreach (var item in items ?? Enumerable.Empty<GroceryItemDto>())
        {
            commands.Add(new Command
            {
                Id = $"item-{item.Id}",
                Title = item.Name,
                Subtitle = $"{item.Sku} · {item.Category} · {item.Stock} in stock",
                Group = "Items",
                Kind = CommandKind.Item,
                Href = $"/groceries/{item.Id}"
            });
        }

        return commands;
    }

    /// <inheritdoc />
    public IReadOnlyList<Command> Search(IEnumerable<Command> commands, string? query, int limit = 12)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var all = commands.ToList();
        var take = Math.Max(0, limit);

        if (string.IsNullOrWhiteSpace(query))
        {
            return all.Take(take).ToList();
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return all
            .Select(command => (Command: command, Score: ScoreOf(command, terms)))
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Command.Title, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .Select(candidate => candidate.Command)
            .ToList();
    }

    /// <summary>
    /// Scores one command against the search terms, higher being better and zero meaning no match.
    /// </summary>
    /// <param name="command">The command to score.</param>
    /// <param name="terms">The terms the query was split into.</param>
    /// <returns>The score, or zero when any term matches nothing.</returns>
    private static int ScoreOf(Command command, string[] terms)
    {
        var total = 0;

        foreach (var term in terms)
        {
            var termScore = 0;

            if (command.Title.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            {
                // Typing the beginning of a name is the strongest signal there is.
                termScore = 40;
            }
            else if (StartsAnyWord(command.Title, term))
            {
                termScore = 25;
            }
            else if (command.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                termScore = 15;
            }
            else if (command.Subtitle.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     command.Group.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                termScore = 6;
            }

            // One term that matches nothing means this is not what was asked for, however well the other
            // terms matched.
            if (termScore == 0)
            {
                return 0;
            }

            total += termScore;
        }

        // A slight preference for shorter titles, so a short name outranks a longer one containing it.
        return total - Math.Min(5, command.Title.Length / 10);
    }

    private static bool StartsAnyWord(string text, string term)
    {
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Command Page(string id, string title, string subtitle, string? href = null) => new()
    {
        Id = $"page-{id}",
        Title = title,
        Subtitle = subtitle,
        Group = "Go to",
        Kind = CommandKind.Navigation,
        Href = href ?? $"/{id}"
    };
}
