using System.Globalization;
using System.Text.Json;
using GroceryApp.Models;
using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <inheritdoc />
public class InventoryImportService : IInventoryImportService
{
    /// <summary>
    /// The header names a file may use, mapped onto the field they mean.
    /// </summary>
    /// <remarks>
    /// Compared with the punctuation and case taken out, so "Cost To Produce", "cost_to_produce" and
    /// "costtoproduce" are the same column. A file exported from another system rarely uses these names
    /// exactly, and refusing it over a dash would be a poor reason to refuse it.
    /// </remarks>
    private static readonly Dictionary<string, string> Fields = new(StringComparer.Ordinal)
    {
        ["name"] = "name",
        ["item"] = "name",
        ["itemname"] = "name",
        ["sku"] = "sku",
        ["code"] = "sku",
        ["category"] = "category",
        ["supplier"] = "supplier",
        ["unit"] = "unit",
        ["price"] = "price",
        ["unitprice"] = "price",
        ["cost"] = "cost",
        ["costtoproduce"] = "cost",
        ["stock"] = "stock",
        ["quantity"] = "stock",
        ["qty"] = "stock",
        ["reorderpoint"] = "reorderpoint",
        ["reorder"] = "reorderpoint",
        ["targetmargin"] = "targetmargin",
        ["targetmarginpercent"] = "targetmargin",
        ["margin"] = "targetmargin",
        ["tags"] = "tags",
        ["notes"] = "notes",
        ["note"] = "notes"
    };

    private readonly IGroceryService _groceries;
    private readonly ICatalogueHistoryService _history;
    private readonly IActivityService _activity;

    /// <summary>
    /// Initializes a new instance of the InventoryImportService class.
    /// </summary>
    /// <param name="groceries">The service the rows are written through, so a row obeys the same rules as the form.</param>
    /// <param name="history">The undo stack the whole import becomes one step of.</param>
    /// <param name="activity">The log the import is recorded in.</param>
    public InventoryImportService(
        IGroceryService groceries,
        ICatalogueHistoryService history,
        IActivityService activity)
    {
        _groceries = groceries ?? throw new ArgumentNullException(nameof(groceries));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
    }

    /// <inheritdoc />
    public ImportPreview Preview(string? text, ImportFormat format, IEnumerable<GroceryItemDto>? existing)
    {
        var preview = new ImportPreview { Format = format };

        if (string.IsNullOrWhiteSpace(text))
        {
            preview.Error = "There is nothing to read. Paste some rows, or choose a file.";

            return preview;
        }

        List<Dictionary<string, string>> records;

        try
        {
            records = format == ImportFormat.Json ? ReadJson(text) : ReadCsv(text);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            preview.Error = $"That could not be read as {format.ToString().ToUpperInvariant()}: {ex.Message}";

            return preview;
        }

        if (records.Count == 0)
        {
            preview.Error = "No rows were found. A CSV needs a header row naming the columns.";

            return preview;
        }

        var catalogue = (existing ?? Enumerable.Empty<GroceryItemDto>()).ToList();

        for (var index = 0; index < records.Count; index++)
        {
            preview.Rows.Add(Judge(records[index], index + 1, catalogue));
        }

        return preview;
    }

    /// <inheritdoc />
    public async Task<ImportResult> ApplyAsync(ImportPreview preview, bool updateExisting)
    {
        ArgumentNullException.ThrowIfNull(preview);

        if (!preview.IsReadable)
        {
            return default;
        }

        var writing = preview.Rows
            .Where(row => row.Values is not null && row.WouldWrite)
            .Where(row => row.Status == ImportStatus.New || updateExisting)
            .ToList();

        if (writing.Count == 0)
        {
            return default;
        }

        var created = 0;
        var updated = 0;

        // The whole import is one step: the row writes below are each step-worthy changes of their own, and
        // the history service treats a change made inside a change as part of the outer one.
        await _history.RunAsync($"Importing {writing.Count} rows", async () =>
        {
            foreach (var row in writing)
            {
                if (row.Status == ImportStatus.New)
                {
                    await _groceries.CreateGroceryAsync(row.Values!);
                    created++;
                }
                else if (await _groceries.UpdateGroceryAsync(row.MatchedItemId, AsUpdate(row.Values!)) is not null)
                {
                    updated++;
                }
            }
        });

        await _activity.RecordCatalogueAsync(
            ActivityKind.Imported,
            $"Imported a file: {created} added, {updated} updated");

        return new ImportResult(created, updated);
    }

    /// <inheritdoc />
    public string Sample(ImportFormat format) => format == ImportFormat.Json
        ? """[{"name":"Conference Pear","category":"Fruit","price":1.35,"costToProduce":0.62,"stock":24,"supplier":"Wye Valley Produce"}]"""
        : "name,category,price,costToProduce,stock,supplier\nConference Pear,Fruit,1.35,0.62,24,Wye Valley Produce";

    /// <summary>
    /// Reads a JSON array of objects into records.
    /// </summary>
    /// <remarks>
    /// Walked as a document rather than deserialized into a type: a WebAssembly build is trimmed, and
    /// reflection based JSON does not survive that, which is the same reason the stored payloads use
    /// generated serializer metadata.
    /// </remarks>
    private static List<Dictionary<string, string>> ReadJson(string text)
    {
        var records = new List<Dictionary<string, string>>();

        using var document = JsonDocument.Parse(text);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("the top level of a catalogue must be an array of items");
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("every item must be an object of fields");
            }

            var record = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var property in element.EnumerateObject())
            {
                var field = Canonical(property.Name);

                if (field is not null)
                {
                    record[field] = Value(property.Value);
                }
            }

            records.Add(record);
        }

        return records;
    }

    /// <summary>
    /// Reads comma separated text into records, using the first row as the header.
    /// </summary>
    private static List<Dictionary<string, string>> ReadCsv(string text)
    {
        var rows = ParseDelimited(text);

        if (rows.Count == 0)
        {
            return new List<Dictionary<string, string>>();
        }

        // The header decides which column is which, so a file with its columns in a different order, or with
        // extra columns, is read the same way.
        var header = rows[0].Select(Canonical).ToList();
        var records = new List<Dictionary<string, string>>();

        foreach (var row in rows.Skip(1))
        {
            var record = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var index = 0; index < header.Count && index < row.Count; index++)
            {
                if (header[index] is { } field)
                {
                    record[field] = row[index];
                }
            }

            records.Add(record);
        }

        return records;
    }

    /// <summary>
    /// Splits delimited text into rows of fields, honouring quotes.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <returns>The rows, with blank lines removed.</returns>
    /// <remarks>
    /// A supplier called "Wye Valley, Ltd" is the reason this cannot be a Split(',') call. A doubled quote
    /// inside a quoted field is one quote, which is the convention every spreadsheet writes.
    /// </remarks>
    private static List<List<string>> ParseDelimited(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    quoted = true;
                    break;

                case ',':
                    row.Add(field.ToString().Trim());
                    field.Clear();
                    break;

                case '\r':
                    break;

                case '\n':
                    row.Add(field.ToString().Trim());
                    field.Clear();

                    if (row.Any(value => value.Length > 0))
                    {
                        rows.Add(row);
                    }

                    row = new List<string>();
                    break;

                default:
                    field.Append(character);
                    break;
            }
        }

        row.Add(field.ToString().Trim());

        if (row.Any(value => value.Length > 0))
        {
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Reads one record into the values that would be written, or refuses it.
    /// </summary>
    /// <param name="record">The record's fields.</param>
    /// <param name="row">The row being judged, for its reason.</param>
    /// <returns>The values, or null when the row is not usable.</returns>
    private static CreateGroceryItemDto? ReadValues(Dictionary<string, string> record, ImportRow row)
    {
        var name = Read(record, "name");

        if (string.IsNullOrWhiteSpace(name))
        {
            row.Reason = "No name";

            return null;
        }

        if (!TryReadCategory(record, row, out var category) ||
            !TryReadDecimal(record, "price", row, mustBePositive: true, out var price) ||
            !TryReadDecimal(record, "cost", row, mustBePositive: true, out var cost) ||
            !TryReadInt(record, "stock", row, out var stock) ||
            !TryReadInt(record, "reorderpoint", row, out var reorderPoint) ||
            !TryReadDecimal(record, "targetmargin", row, mustBePositive: false, out var targetMargin))
        {
            return null;
        }

        var unit = UnitOfMeasure.Each;

        var unitText = Read(record, "unit");

        if (!string.IsNullOrWhiteSpace(unitText) && !Enum.TryParse(unitText, ignoreCase: true, out unit))
        {
            row.Reason = $"'{unitText}' is not a unit the catalogue knows";

            return null;
        }

        return new CreateGroceryItemDto
        {
            Name = name,
            Sku = Read(record, "sku"),
            Category = category,
            Supplier = Read(record, "supplier"),
            Unit = unit,
            Price = price,
            CostToProduce = cost,
            Stock = stock,

            // A column that is not there leaves the catalogue's own default rather than a zero: a file that
            // only carries stock should not silently clear a reorder point or a target margin.
            ReorderPoint = record.ContainsKey("reorderpoint") ? reorderPoint : 5,
            TargetMarginPercent = record.ContainsKey("targetmargin") ? targetMargin : 40m,
            Tags = Split(Read(record, "tags")),
            Notes = Read(record, "notes")
        };
    }

    /// <summary>
    /// Decides what would happen to one record.
    /// </summary>
    /// <param name="record">The record's fields, keyed by canonical field name.</param>
    /// <param name="lineNumber">Where it came from, for the report.</param>
    /// <param name="catalogue">The catalogue to judge it against.</param>
    /// <returns>The row, judged.</returns>
    private static ImportRow Judge(
        Dictionary<string, string> record,
        int lineNumber,
        List<GroceryItemDto> catalogue)
    {
        var name = Read(record, "name");
        var sku = Read(record, "sku");

        var row = new ImportRow
        {
            LineNumber = lineNumber,
            Name = string.IsNullOrWhiteSpace(name) ? sku : name,
            Sku = sku
        };

        var values = ReadValues(record, row);

        if (values is null)
        {
            row.Status = ImportStatus.Invalid;

            return row;
        }

        row.Values = values;

        // Matched by code first, then by name: a code is what a catalogue uses to identify a line, and a name
        // is what a person writing a spreadsheet uses.
        var match = FindMatch(catalogue, values);

        if (match is null)
        {
            row.Status = ImportStatus.New;

            return row;
        }

        row.MatchedItemId = match.Id;
        row.Status = Differs(match, values) ? ImportStatus.Update : ImportStatus.Unchanged;
        row.Reason = row.Status == ImportStatus.Update
            ? $"Changes the {DescribeDifference(match, values)} of {match.Name}"
            : $"Already in the catalogue as {match.Name}";

        return row;
    }

    /// <summary>
    /// Finds the catalogue item a row refers to, by code first and then by name.
    /// </summary>
    /// <param name="catalogue">The catalogue to search.</param>
    /// <param name="values">The values the row would write.</param>
    /// <returns>The matching item, or null when the catalogue has none.</returns>
    private static GroceryItemDto? FindMatch(List<GroceryItemDto> catalogue, CreateGroceryItemDto values)
    {
        if (!string.IsNullOrWhiteSpace(values.Sku))
        {
            var bySku = catalogue.FirstOrDefault(item =>
                string.Equals(item.Sku, values.Sku, StringComparison.OrdinalIgnoreCase));

            if (bySku is not null)
            {
                return bySku;
            }
        }

        // Two items with the same name and no code cannot be told apart, so the first is taken as the one the
        // row means rather than guessing at something cleverer.
        return catalogue.FirstOrDefault(item =>
            string.Equals(item.Name, values.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Decides whether a row would actually change the item it matches.
    /// </summary>
    /// <param name="item">The item the row matches.</param>
    /// <param name="values">The values the row would write.</param>
    /// <returns>True when something about the item would be different afterwards.</returns>
    /// <remarks>
    /// Only the fields a file can carry are compared. An empty code in the file does not count as a change,
    /// because the catalogue generates one and the file simply has nothing to say about it.
    /// </remarks>
    private static bool Differs(GroceryItemDto item, CreateGroceryItemDto values) =>
        item.Price != values.Price ||
        item.CostToProduce != values.CostToProduce ||
        item.Stock != values.Stock ||
        item.Category != values.Category ||
        item.Unit != values.Unit ||
        !string.Equals(item.Supplier, values.Supplier, StringComparison.Ordinal) ||
        !string.Equals(item.Notes, values.Notes, StringComparison.Ordinal) ||
        (values.Sku.Length > 0 && !string.Equals(item.Sku, values.Sku, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Names the first thing about a row that differs, which is what somebody needs to know before accepting a
    /// file that would change their catalogue.
    /// </summary>
    /// <param name="item">The item the row matches.</param>
    /// <param name="values">The values the row would write.</param>
    /// <returns>A short description of the change.</returns>
    private static string DescribeDifference(GroceryItemDto item, CreateGroceryItemDto values)
    {
        if (item.Price != values.Price)
        {
            return $"price from {item.Price:C2} to {values.Price:C2}";
        }

        if (item.Stock != values.Stock)
        {
            return $"stock from {item.Stock} to {values.Stock}";
        }

        if (item.CostToProduce != values.CostToProduce)
        {
            return $"cost from {item.CostToProduce:C2} to {values.CostToProduce:C2}";
        }

        if (item.Category != values.Category)
        {
            return $"category from {item.Category} to {values.Category}";
        }

        return "details";
    }

    /// <summary>Presents values read for a new row as the update the same values would make to an existing one.</summary>
    /// <param name="values">The values read from the row.</param>
    /// <returns>The update to write.</returns>
    private static UpdateGroceryItemDto AsUpdate(CreateGroceryItemDto values) => new()
    {
        Name = values.Name,
        Sku = values.Sku,
        Category = values.Category,
        Supplier = values.Supplier,
        Unit = values.Unit,
        Price = values.Price,
        CostToProduce = values.CostToProduce,
        Stock = values.Stock,
        ReorderPoint = values.ReorderPoint,
        TargetMarginPercent = values.TargetMarginPercent,
        Tags = values.Tags,
        Notes = values.Notes
    };

    /// <summary>
    /// Maps a header or JSON property onto the field it means, or null when the application has no use for it.
    /// </summary>
    private static string? Canonical(string name)
    {
        var normalised = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        return normalised.Length == 0 ? null : Fields.GetValueOrDefault(normalised);
    }

    /// <summary>Reads a JSON value as the text a CSV column would have carried.</summary>
    private static string Value(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => element.ToString()
    };

    /// <summary>Splits a tag column on separators other than a comma, so a quoted list is never needed.</summary>
    private static List<string> Split(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static string Read(Dictionary<string, string> record, string field) =>
        record.TryGetValue(field, out var value) ? value : string.Empty;

    private static bool TryReadCategory(Dictionary<string, string> record, ImportRow row, out Category category)
    {
        var text = Read(record, "category");

        if (string.IsNullOrWhiteSpace(text))
        {
            row.Reason = "No category";
            category = default;

            return false;
        }

        if (!Enum.TryParse(text, ignoreCase: true, out category))
        {
            row.Reason = $"'{text}' is not one of the five categories";

            return false;
        }

        return true;
    }

    private static bool TryReadDecimal(
        Dictionary<string, string> record,
        string field,
        ImportRow row,
        bool mustBePositive,
        out decimal value)
    {
        value = 0m;

        var text = Read(record, field);

        if (string.IsNullOrWhiteSpace(text))
        {
            // Only price and cost are required, and both are checked as positive below, so an absent column is
            // the same failure as a zero in it. Anything else keeps the catalogue's own default.
            if (!mustBePositive)
            {
                return true;
            }

            row.Reason = $"No {field}";

            return false;
        }

        // Invariant, with the currency symbol and the thousands separators taken out: a file exported from a
        // spreadsheet may carry either.
        var cleaned = new string(text.Where(character => char.IsDigit(character) || character is '.' or '-').ToArray());

        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            row.Reason = $"'{text}' is not a number";

            return false;
        }

        if (mustBePositive && value <= 0m)
        {
            row.Reason = $"{char.ToUpperInvariant(field[0])}{field[1..]} must be greater than zero";

            return false;
        }

        if (!mustBePositive && (value < 0m || value > 100m))
        {
            row.Reason = "The target margin must be between 0 and 100";

            return false;
        }

        return true;
    }

    private static bool TryReadInt(Dictionary<string, string> record, string field, ImportRow row, out int value)
    {
        value = 0;

        var text = Read(record, field);

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < 0)
        {
            row.Reason = $"'{text}' is not a whole number of units";

            return false;
        }

        return true;
    }
}
