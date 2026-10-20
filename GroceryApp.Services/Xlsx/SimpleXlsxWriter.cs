using System.IO.Compression;
using System.Text;

namespace GroceryApp.Services.Xlsx;

/// <summary>
/// Minimal, dependency free SpreadsheetML (.xlsx) writer.
/// </summary>
/// <remarks>
/// The server version of this application used ClosedXML to build its reports. ClosedXML is
/// not browser friendly (it depends on Open XML SDK plus font libraries), so this writer
/// generates the Open XML package directly with <see cref="ZipArchive"/>. The result opens in
/// Excel, Numbers, LibreOffice and Google Sheets exactly like a ClosedXML generated file, and
/// it keeps workbook generation inside the application, so the export works wherever the app runs.
/// </remarks>
public sealed class SimpleXlsxWriter
{
    private readonly List<XlsxSheet> _sheets = new();

    /// <summary>
    /// Adds a worksheet to the workbook.
    /// </summary>
    /// <param name="name">Worksheet tab name (maximum 31 characters).</param>
    /// <returns>The worksheet so cells can be written to it.</returns>
    public XlsxSheet AddSheet(string name)
    {
        if (_sheets.Any(sheet => string.Equals(sheet.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A worksheet named '{name}' already exists.");
        }

        var worksheet = new XlsxSheet(name);
        _sheets.Add(worksheet);
        return worksheet;
    }

    /// <summary>
    /// Builds the workbook and returns it as the bytes of an .xlsx file.
    /// </summary>
    /// <returns>The workbook contents.</returns>
    public byte[] Build()
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypes());
            WriteEntry(archive, "_rels/.rels", BuildRootRelationships());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbook());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
            WriteEntry(archive, "xl/styles.xml", XlsxXml.StylesXml);

            for (var index = 0; index < _sheets.Count; index++)
            {
                WriteEntry(archive, $"xl/worksheets/sheet{index + 1}.xml", _sheets[index].ToXml());
            }
        }

        return stream.ToArray();
    }

    private string BuildContentTypes()
    {
        var builder = new StringBuilder();
        builder.Append(XlsxXml.Declaration);
        builder.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        builder.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        builder.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        builder.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        builder.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");

        for (var index = 0; index < _sheets.Count; index++)
        {
            builder.Append($"<Override PartName=\"/xl/worksheets/sheet{index + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        }

        builder.Append("</Types>");
        return builder.ToString();
    }

    private static string BuildRootRelationships()
    {
        var builder = new StringBuilder();
        builder.Append(XlsxXml.Declaration);
        builder.Append($"<Relationships xmlns=\"{XlsxXml.PackageRelationshipNamespace}\">");
        builder.Append($"<Relationship Id=\"rId1\" Type=\"{XlsxXml.DocumentRelationshipNamespace}/officeDocument\" Target=\"xl/workbook.xml\"/>");
        builder.Append("</Relationships>");
        return builder.ToString();
    }

    private string BuildWorkbook()
    {
        var builder = new StringBuilder();
        builder.Append(XlsxXml.Declaration);
        builder.Append($"<workbook xmlns=\"{XlsxXml.SpreadsheetNamespace}\" xmlns:r=\"{XlsxXml.DocumentRelationshipNamespace}\">");
        builder.Append("<sheets>");

        for (var index = 0; index < _sheets.Count; index++)
        {
            builder.Append($"<sheet name=\"{XlsxXml.Escape(_sheets[index].Name)}\" sheetId=\"{index + 1}\" r:id=\"rId{index + 1}\"/>");
        }

        builder.Append("</sheets>");
        builder.Append("</workbook>");
        return builder.ToString();
    }

    private string BuildWorkbookRelationships()
    {
        var builder = new StringBuilder();
        builder.Append(XlsxXml.Declaration);
        builder.Append($"<Relationships xmlns=\"{XlsxXml.PackageRelationshipNamespace}\">");

        for (var index = 0; index < _sheets.Count; index++)
        {
            builder.Append($"<Relationship Id=\"rId{index + 1}\" Type=\"{XlsxXml.DocumentRelationshipNamespace}/worksheet\" Target=\"worksheets/sheet{index + 1}.xml\"/>");
        }

        builder.Append($"<Relationship Id=\"rId{_sheets.Count + 1}\" Type=\"{XlsxXml.DocumentRelationshipNamespace}/styles\" Target=\"styles.xml\"/>");
        builder.Append("</Relationships>");
        return builder.ToString();
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);

        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}
