namespace Jaunty.FlatFiles.Core;

/// <summary>
/// Well-known flat file format identifiers. Custom formats can use any string value.
/// </summary>
public static class FileFormats
{
    /// <summary>Comma-separated values.</summary>
    public const string Csv = "CSV";

    /// <summary>Tab-separated values.</summary>
    public const string Tsv = "TSV";

    /// <summary>Apache Parquet columnar format.</summary>
    public const string Parquet = "PARQUET";

    /// <summary>JSON (array or newline-delimited).</summary>
    public const string Json = "JSON";

    /// <summary>Microsoft Excel (.xlsx) format.</summary>
    public const string Excel = "XLSX";

    /// <summary>Delta Lake table format.</summary>
    public const string DeltaLake = "DELTA";

    /// <summary>Apache Iceberg table format.</summary>
    public const string Iceberg = "ICEBERG";
}