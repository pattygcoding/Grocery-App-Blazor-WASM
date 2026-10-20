using System.Globalization;
using System.Text;

namespace GroceryApp.Services.Xlsx;

/// <summary>
/// Low level helpers and the shared styles part used by the generated workbooks.
/// </summary>
internal static class XlsxXml
{
    public const string Declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

    public const string SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public const string DocumentRelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public const string PackageRelationshipNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>
    /// Converts a one based column index into its Excel column name (1 =&gt; A, 27 =&gt; AA).
    /// </summary>
    public static string ColumnName(int oneBasedColumn)
    {
        if (oneBasedColumn < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(oneBasedColumn), oneBasedColumn, "Column indexes are one based.");
        }

        var builder = new StringBuilder();
        var value = oneBasedColumn;

        while (value > 0)
        {
            var remainder = (value - 1) % 26;
            builder.Insert(0, (char)('A' + remainder));
            value = (value - 1) / 26;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Escapes a value so it can be written into an XML text node or attribute.
    /// </summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            switch (character)
            {
                case '&': builder.Append("&amp;"); break;
                case '<': builder.Append("&lt;"); break;
                case '>': builder.Append("&gt;"); break;
                case '"': builder.Append("&quot;"); break;
                case '\'': builder.Append("&apos;"); break;
                default: builder.Append(character); break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Formats a numeric cell value using the invariant culture, as required by the file format.
    /// </summary>
    public static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The styles part: number formats, fonts, fills and the cell format table referenced
    /// by <see cref="XlsxStyle"/>.
    /// </summary>
    public static string StylesXml => Declaration + $@"
<styleSheet xmlns='{SpreadsheetNamespace}'>
<numFmts count='2'><numFmt numFmtId='164' formatCode='&quot;$&quot;#,##0.00'/><numFmt numFmtId='165' formatCode='0.0%'/></numFmts>
<fonts count='4'>
<font><sz val='11'/><color theme='1'/><name val='Calibri'/><family val='2'/></font>
<font><b/><sz val='11'/><color theme='1'/><name val='Calibri'/><family val='2'/></font>
<font><b/><sz val='16'/><color theme='1'/><name val='Calibri'/><family val='2'/></font>
<font><i/><sz val='11'/><color theme='1'/><name val='Calibri'/><family val='2'/></font>
</fonts>
<fills count='9'>
<fill><patternFill patternType='none'/></fill>
<fill><patternFill patternType='gray125'/></fill>
<fill><patternFill patternType='solid'><fgColor rgb='FFDDEBF7'/><bgColor indexed='64'/></patternFill></fill>
<fill><patternFill patternType='solid'><fgColor rgb='FFE2EFDA'/><bgColor indexed='64'/></patternFill></fill>
<fill><patternFill patternType='solid'><fgColor rgb='FFFFF2CC'/><bgColor indexed='64'/></patternFill></fill>
<fill><patternFill patternType='solid'><fgColor rgb='FFF8CBAD'/><bgColor indexed='64'/></patternFill></fill>
<fill><patternFill patternType='solid'><fgColor rgb='FFDAEEF3'/><bgColor indexed='64'/></patternFill></fill>
<fill><patternFill patternType='solid'><fgColor rgb='FFFCE4E4'/><bgColor indexed='64'/></patternFill></fill>
<fill><patternFill patternType='solid'><fgColor rgb='FFFFFFFF'/><bgColor indexed='64'/></patternFill></fill>
</fills>
<borders count='1'><border><left/><right/><top/><bottom/><diagonal/></border></borders>
<cellStyleXfs count='1'><xf numFmtId='0' fontId='0' fillId='0' borderId='0'/></cellStyleXfs>
<cellXfs count='18'>
<xf numFmtId='0' fontId='0' fillId='0' borderId='0' xfId='0'/>
<xf numFmtId='0' fontId='2' fillId='0' borderId='0' xfId='0' applyFont='1'/>
<xf numFmtId='0' fontId='1' fillId='0' borderId='0' xfId='0' applyFont='1'/>
<xf numFmtId='0' fontId='1' fillId='2' borderId='0' xfId='0' applyFont='1' applyFill='1'/>
<xf numFmtId='0' fontId='1' fillId='3' borderId='0' xfId='0' applyFont='1' applyFill='1'/>
<xf numFmtId='0' fontId='1' fillId='4' borderId='0' xfId='0' applyFont='1' applyFill='1'/>
<xf numFmtId='0' fontId='1' fillId='5' borderId='0' xfId='0' applyFont='1' applyFill='1'/>
<xf numFmtId='164' fontId='0' fillId='0' borderId='0' xfId='0' applyNumberFormat='1'/>
<xf numFmtId='165' fontId='0' fillId='0' borderId='0' xfId='0' applyNumberFormat='1'/>
<xf numFmtId='0' fontId='0' fillId='3' borderId='0' xfId='0' applyFill='1'/>
<xf numFmtId='164' fontId='0' fillId='3' borderId='0' xfId='0' applyNumberFormat='1' applyFill='1'/>
<xf numFmtId='164' fontId='0' fillId='2' borderId='0' xfId='0' applyNumberFormat='1' applyFill='1'/>
<xf numFmtId='0' fontId='0' fillId='4' borderId='0' xfId='0' applyFill='1'/>
<xf numFmtId='0' fontId='0' fillId='6' borderId='0' xfId='0' applyFill='1'/>
<xf numFmtId='164' fontId='0' fillId='4' borderId='0' xfId='0' applyNumberFormat='1' applyFill='1'/>
<xf numFmtId='164' fontId='0' fillId='7' borderId='0' xfId='0' applyNumberFormat='1' applyFill='1'/>
<xf numFmtId='0' fontId='3' fillId='0' borderId='0' xfId='0' applyFont='1'/>
<xf numFmtId='0' fontId='0' fillId='7' borderId='0' xfId='0' applyFill='1'/>
</cellXfs>
<cellStyles count='1'><cellStyle name='Normal' xfId='0' builtinId='0'/></cellStyles>
</styleSheet>";
}
