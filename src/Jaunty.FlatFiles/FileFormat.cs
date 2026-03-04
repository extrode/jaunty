namespace Jaunty.FlatFiles;

/// <summary>
/// Supported flat file formats.
/// </summary>
public enum FileFormat
{
    /// <summary>Comma-separated values.</summary>
    Csv,

    /// <summary>Tab-separated values.</summary>
    Tsv,

    /// <summary>Apache Parquet columnar format.</summary>
    Parquet,

    /// <summary>JSON (array or newline-delimited).</summary>
    Json
}
