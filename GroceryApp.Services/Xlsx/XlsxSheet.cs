using System.Globalization;
using System.Text;

namespace GroceryApp.Services.Xlsx;

/// <summary>
/// A single worksheet. Cells are addressed with one based row and column indexes
/// (row 1/column 1 is cell A1), mirroring the closedXML API used by the server version.
/// </summary>
public sealed class XlsxSheet
{
    private const int MaximumSheetNameLength = 31;
    private const double MinimumColumnWidth = 10d;
    private const double MaximumColumnWidth = 42d;
    private const double NumericColumnWidth = 16d;

    private readonly SortedDictionary<int, SortedDictionary<int, XlsxCell>> _rows = new();
    private readonly Dictionary<int, double> _columnWidths = new();
    private readonly List<string> _mergedRanges = new();

    internal XlsxSheet(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A worksheet name is required.", nameof(name));
        }

        Name = name.Length > MaximumSheetNameLength ? name[..MaximumSheetNameLength] : name;
    }

    /// <summary>
    /// Gets the worksheet tab name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Writes a text cell.
    /// </summary>
    /// <param name="row">One based row index.</param>
    /// <param name="column">One based column index.</param>
    /// <param name="value">The text to write.</param>
    /// <param name="style">Style identifier from <see cref="XlsxStyle"/>.</param>
    public void SetText(int row, int column, string? value, int style = XlsxStyle.Default)
    {
        var text = value ?? string.Empty;
        SetCell(row, column, new XlsxCell { IsNumber = false, Text = text, Style = style });
        TrackColumnWidth(column, text.Length, isNumber: false);
    }

    /// <summary>
    /// Writes a numeric (or currency/percentage formatted) cell.
    /// </summary>
    /// <param name="row">One based row index.</param>
    /// <param name="column">One based column index.</param>
    /// <param name="value">The numeric value to write.</param>
    /// <param name="style">Style identifier from <see cref="XlsxStyle"/>.</param>
    public void SetNumber(int row, int column, decimal value, int style = XlsxStyle.Default)
    {
        SetCell(row, column, new XlsxCell { IsNumber = true, Number = value, Style = style });
        TrackColumnWidth(column, 0, isNumber: true);
    }

    /// <summary>
    /// Merges a range of cells, for example "A1:C1".
    /// </summary>
    /// <param name="firstCell">Top left cell of the range.</param>
    /// <param name="lastCell">Bottom right cell of the range.</param>
    public void Merge(string firstCell, string lastCell)
    {
        _mergedRanges.Add($"{firstCell}:{lastCell}");
    }

    /// <summary>
    /// Renders the worksheet as SpreadsheetML.
    /// </summary>
    internal string ToXml()
    {
        var builder = new StringBuilder();
        builder.Append(XlsxXml.Declaration);
        builder.Append($"<worksheet xmlns=\"{XlsxXml.SpreadsheetNamespace}\">");

        if (_columnWidths.Count > 0)
        {
            builder.Append("<cols>");
            foreach (var column in _columnWidths.Keys.OrderBy(key => key))
            {
                var width = _columnWidths[column].ToString(CultureInfo.InvariantCulture);
                builder.Append($"<col min=\"{column}\" max=\"{column}\" width=\"{width}\" customWidth=\"1\"/>");
            }

            builder.Append("</cols>");
        }

        builder.Append("<sheetData>");
        foreach (var row in _rows)
        {
            builder.Append($"<row r=\"{row.Key}\">");

            foreach (var cell in row.Value)
            {
                var reference = XlsxXml.ColumnName(cell.Key) + row.Key.ToString(CultureInfo.InvariantCulture);

                if (cell.Value.IsNumber)
                {
                    builder.Append($"<c r=\"{reference}\" s=\"{cell.Value.Style}\"><v>{XlsxXml.Number(cell.Value.Number)}</v></c>");
                }
                else
                {
                    builder.Append($"<c r=\"{reference}\" s=\"{cell.Value.Style}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{XlsxXml.Escape(cell.Value.Text)}</t></is></c>");
                }
            }

            builder.Append("</row>");
        }

        builder.Append("</sheetData>");

        if (_mergedRanges.Count > 0)
        {
            builder.Append($"<mergeCells count=\"{_mergedRanges.Count}\">");
            foreach (var range in _mergedRanges)
            {
                builder.Append($"<mergeCell ref=\"{range}\"/>");
            }

            builder.Append("</mergeCells>");
        }

        builder.Append("</worksheet>");
        return builder.ToString();
    }

    private void SetCell(int row, int column, XlsxCell cell)
    {
        if (row < 1) throw new ArgumentOutOfRangeException(nameof(row), row, "Row indexes are one based.");
        if (column < 1) throw new ArgumentOutOfRangeException(nameof(column), column, "Column indexes are one based.");

        if (!_rows.TryGetValue(row, out var cells))
        {
            cells = new SortedDictionary<int, XlsxCell>();
            _rows[row] = cells;
        }

        cells[column] = cell;
    }

    private void TrackColumnWidth(int column, int textLength, bool isNumber)
    {
        var width = isNumber
            ? NumericColumnWidth
            : Math.Clamp(textLength + 2.5d, MinimumColumnWidth, MaximumColumnWidth);

        if (!_columnWidths.TryGetValue(column, out var current) || width > current)
        {
            _columnWidths[column] = width;
        }
    }

    private sealed class XlsxCell
    {
        public bool IsNumber { get; init; }

        public string Text { get; init; } = string.Empty;

        public decimal Number { get; init; }

        public int Style { get; init; }
    }
}
