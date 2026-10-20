using System.Globalization;
using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Reads and writes an <see cref="InventoryFilter"/> as a query string.
/// </summary>
/// <remarks>
/// <para>
/// Filters live in the address bar rather than in component state, so a narrowed catalogue can be
/// bookmarked, shared and reached with the back button. A saved view is then nothing more than a label
/// attached to one of these strings.
/// </para>
/// <para>
/// Parsing is deliberately forgiving: an unknown column name, a category that no longer exists or a
/// price that is not a number is dropped rather than thrown, because these strings arrive from links and
/// from storage written by earlier builds. Defaults are left out when writing, so an unfiltered
/// catalogue has a clean address.
/// </para>
/// <para>
/// List values are comma separated, which means a tag or supplier name cannot itself contain a comma.
/// </para>
/// </remarks>
public static class InventoryFilterQuery
{
    /// <summary>
    /// Writes a filter as a query string, without a leading question mark.
    /// </summary>
    /// <param name="filter">The filter to write.</param>
    /// <returns>The query string, or an empty string when the filter matches everything.</returns>
    public static string ToQueryString(InventoryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            parts.Add($"q={Escape(filter.SearchText.Trim())}");
        }

        if (filter.Categories.Count > 0)
        {
            parts.Add($"cat={EscapeList(filter.Categories.Select(category => category.ToString()))}");
        }

        if (filter.Suppliers.Count > 0)
        {
            parts.Add($"sup={EscapeList(filter.Suppliers)}");
        }

        if (filter.Tags.Count > 0)
        {
            parts.Add($"tag={EscapeList(filter.Tags)}");
        }

        if (filter.MinimumPrice.HasValue)
        {
            parts.Add($"min={Number(filter.MinimumPrice.Value)}");
        }

        if (filter.MaximumPrice.HasValue)
        {
            parts.Add($"max={Number(filter.MaximumPrice.Value)}");
        }

        if (filter.MaximumMarginPercent.HasValue)
        {
            parts.Add($"margin={Number(filter.MaximumMarginPercent.Value)}");
        }

        if (filter.Stock != StockFilter.Any)
        {
            parts.Add($"stock={filter.Stock}");
        }

        if (filter.AbcClass.HasValue)
        {
            parts.Add($"abc={filter.AbcClass.Value}");
        }

        if (filter.IncludeArchived)
        {
            parts.Add("archived=1");
        }

        if (filter.FavouritesOnly)
        {
            parts.Add("fav=1");
        }

        if (filter.SortBy != InventorySortField.Name)
        {
            parts.Add($"sort={filter.SortBy}");
        }

        if (filter.Direction != SortDirection.Ascending)
        {
            parts.Add("dir=desc");
        }

        return string.Join("&", parts);
    }

    /// <summary>
    /// Reads a filter from a query string.
    /// </summary>
    /// <param name="queryString">The query string, with or without a leading question mark.</param>
    /// <returns>The parsed filter. Anything unreadable is left at its default.</returns>
    public static InventoryFilter Parse(string? queryString)
    {
        var filter = new InventoryFilter();

        if (string.IsNullOrWhiteSpace(queryString))
        {
            return filter;
        }

        foreach (var pair in queryString.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');

            if (separator < 0)
            {
                continue;
            }

            var key = pair[..separator].Trim().ToLowerInvariant();

            // Values are kept escaped until each one is needed: list values are split on commas first,
            // so a comma inside a supplier name or tag stays part of that name instead of splitting it
            // into two entries.
            var rawValue = pair[(separator + 1)..];

            switch (key)
            {
                case "q":
                    filter.SearchText = Unescape(rawValue);
                    break;
                case "cat":
                    filter.Categories = ParseCategories(rawValue);
                    break;
                case "sup":
                    filter.Suppliers = Split(rawValue);
                    break;
                case "tag":
                    filter.Tags = Split(rawValue);
                    break;
                case "min":
                    filter.MinimumPrice = ParseDecimal(rawValue);
                    break;
                case "max":
                    filter.MaximumPrice = ParseDecimal(rawValue);
                    break;
                case "margin":
                    filter.MaximumMarginPercent = ParseDecimal(rawValue);
                    break;
                case "stock":
                    filter.Stock = ParseEnum(Unescape(rawValue), StockFilter.Any);
                    break;
                case "abc":
                    filter.AbcClass = Enum.TryParse<AbcClass>(Unescape(rawValue), ignoreCase: true, out var band)
                        ? band
                        : null;
                    break;
                case "archived":
                    filter.IncludeArchived = ParseSwitch(rawValue);
                    break;
                case "fav":
                    filter.FavouritesOnly = ParseSwitch(rawValue);
                    break;
                case "sort":
                    filter.SortBy = ParseEnum(Unescape(rawValue), InventorySortField.Name);
                    break;
                case "dir":
                    filter.Direction = ParseDirection(rawValue);
                    break;
            }
        }

        return filter;
    }

    private static List<string> Split(string rawValue) =>
        rawValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(Unescape)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToList();

    private static List<Category> ParseCategories(string rawValue)
    {
        var categories = new List<Category>();

        foreach (var part in Split(rawValue))
        {
            // Enum.TryParse treats a comma as a flags separator, so "Fruit,Cheese" parses successfully as
            // the combination of the two rather than failing. A part carrying a comma is therefore not a
            // category name, however plausible it looks to TryParse.
            if (part.Contains(','))
            {
                continue;
            }

            if (Enum.TryParse<Category>(part, ignoreCase: true, out var category))
            {
                categories.Add(category);
            }
        }

        return categories;
    }

    private static decimal? ParseDecimal(string rawValue) =>
        decimal.TryParse(Unescape(rawValue), NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static SortDirection ParseDirection(string rawValue) =>
        Unescape(rawValue).Equals("desc", StringComparison.OrdinalIgnoreCase)
            ? SortDirection.Descending
            : SortDirection.Ascending;

    private static bool ParseSwitch(string rawValue)
    {
        var value = Unescape(rawValue);

        return value == "1" ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Escapes each entry of a list before joining, so an entry that itself contains a comma stays one
    /// entry. Escaping the joined string instead would hide the separators along with the commas.
    /// </summary>
    private static string EscapeList(IEnumerable<string> values) =>
        string.Join(",", values.Select(Escape));

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string Unescape(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            // A malformed escape sequence has come in from a hand-edited link. Showing the raw text is a
            // better answer than failing the whole page over one character.
            return value;
        }
    }
}