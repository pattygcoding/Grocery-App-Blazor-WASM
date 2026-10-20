namespace GroceryApp.Services.Xlsx;

/// <summary>
/// Identifiers for the cell styles defined by <see cref="SimpleXlsxWriter"/>.
/// Each value maps to an entry in the cellXfs table of the generated styles part.
/// </summary>
public static class XlsxStyle
{
    /// <summary>Default, unformatted cell.</summary>
    public const int Default = 0;

    /// <summary>Bold 16pt heading.</summary>
    public const int Title = 1;

    /// <summary>Bold label used for the metric names.</summary>
    public const int BoldLabel = 2;

    /// <summary>Bold column header on a light blue fill.</summary>
    public const int HeaderBlue = 3;

    /// <summary>Bold column header on a light green fill.</summary>
    public const int HeaderGreen = 4;

    /// <summary>Bold column header on a light yellow fill.</summary>
    public const int HeaderYellow = 5;

    /// <summary>Bold column header on a light coral fill.</summary>
    public const int HeaderCoral = 6;

    /// <summary>Currency value ($#,##0.00).</summary>
    public const int Currency = 7;

    /// <summary>Percentage value (0.0%).</summary>
    public const int Percent = 8;

    /// <summary>Light green fill (high stock).</summary>
    public const int GreenFill = 9;

    /// <summary>Light green fill with currency formatting.</summary>
    public const int GreenFillCurrency = 10;

    /// <summary>Light blue fill with currency formatting.</summary>
    public const int BlueFillCurrency = 11;

    /// <summary>Light yellow fill (items in stock).</summary>
    public const int YellowFill = 12;

    /// <summary>Light cyan fill (product categories).</summary>
    public const int CyanFill = 13;

    /// <summary>Light yellow fill with currency formatting.</summary>
    public const int YellowFillCurrency = 14;

    /// <summary>Light pink fill with currency formatting (out of stock).</summary>
    public const int PinkFillCurrency = 15;

    /// <summary>Italic note text.</summary>
    public const int Italic = 16;

    /// <summary>Light pink fill.</summary>
    public const int PinkFill = 17;
}
