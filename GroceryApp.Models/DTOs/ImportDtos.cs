namespace GroceryApp.Models.DTOs;

/// <summary>How a pasted or uploaded catalogue is written.</summary>
public enum ImportFormat
{
    /// <summary>Comma separated, with a header row naming the columns.</summary>
    Csv,

    /// <summary>A JSON array of objects, with the same field names the catalogue uses.</summary>
    Json
}

/// <summary>What would happen to one row if the import were applied.</summary>
public enum ImportStatus
{
    /// <summary>An item the catalogue does not have.</summary>
    New,

    /// <summary>An item the catalogue already has, which the row would change.</summary>
    Update,

    /// <summary>An item the catalogue already has, which the row would not change.</summary>
    Unchanged,

    /// <summary>A row that cannot be read, with the reason recorded against it.</summary>
    Invalid
}

/// <summary>
/// One row of an import, read and judged but not yet written.
/// </summary>
public class ImportRow
{
    /// <summary>Where the row came from in the file, counting from the first line of data.</summary>
    public int LineNumber { get; set; }

    /// <summary>The name the row carries, or the raw text when it could not be read.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The code the row carries, when it has one.</summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>What would happen to it.</summary>
    public ImportStatus Status { get; set; }

    /// <summary>Why it would not be written, or what would change. Empty for a clean new row.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>The identifier of the catalogue item the row matches, or 0.</summary>
    public int MatchedItemId { get; set; }

    /// <summary>The values that would be written, when the row is usable.</summary>
    public CreateGroceryItemDto? Values { get; set; }

    /// <summary>True when the row would be written.</summary>
    public bool WouldWrite => Status is ImportStatus.New or ImportStatus.Update;
}

/// <summary>
/// What an import would do, before any of it is done.
/// </summary>
/// <remarks>
/// The preview exists because an import is the one action in the application that can change everything at
/// once, and because a file is the one input that can be wrong in ways the form cannot be. Nothing is written
/// until this has been shown and accepted.
/// </remarks>
public class ImportPreview
{
    public ImportFormat Format { get; set; }

    /// <summary>Every row that was read, in file order.</summary>
    public List<ImportRow> Rows { get; set; } = new();

    /// <summary>Set when the file could not be read at all, in which case there are no rows.</summary>
    public string? Error { get; set; }

    /// <summary>True when the text was read, whether or not every row in it was usable.</summary>
    public bool IsReadable => Error is null;

    public int NewCount => Rows.Count(row => row.Status == ImportStatus.New);

    public int UpdateCount => Rows.Count(row => row.Status == ImportStatus.Update);

    public int UnchangedCount => Rows.Count(row => row.Status == ImportStatus.Unchanged);

    public int InvalidCount => Rows.Count(row => row.Status == ImportStatus.Invalid);

    /// <summary>How many rows would be written: the new ones plus the updates that were asked for.</summary>
    public int WritableCount(bool updateExisting) =>
        NewCount + (updateExisting ? UpdateCount : 0);

    /// <summary>The rows worth showing in full, which is every row that is not simply unchanged.</summary>
    public List<ImportRow> NotableRows => Rows.Where(row => row.Status != ImportStatus.Unchanged).ToList();

    /// <summary>True when at least one row would be written whichever option is chosen.</summary>
    public bool HasWrites => NewCount > 0 || UpdateCount > 0;

    /// <summary>
    /// A sentence describing what the file would do, for the line above the table.
    /// </summary>
    /// <remarks>
    /// Written out rather than left as four numbers side by side, because the four numbers mean quite
    /// different things and the sentence is where that gets said.
    /// </remarks>
    public string Summary
    {
        get
        {
            if (!IsReadable)
            {
                return Error ?? string.Empty;
            }

            var parts = new List<string>();

            if (NewCount > 0)
            {
                parts.Add($"{NewCount} {(NewCount == 1 ? "row" : "rows")} would be added");
            }

            if (UpdateCount > 0)
            {
                parts.Add($"{UpdateCount} would change {(UpdateCount == 1 ? "an item" : "items")} already in the catalogue");
            }

            if (UnchangedCount > 0)
            {
                parts.Add($"{UnchangedCount} already match and would be left alone");
            }

            if (InvalidCount > 0)
            {
                parts.Add($"{InvalidCount} cannot be read");
            }

            return parts.Count == 0 ? "The file holds no rows." : string.Join(", ", parts) + ".";
        }
    }
}