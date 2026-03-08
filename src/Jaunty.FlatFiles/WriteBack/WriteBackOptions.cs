namespace Jaunty.FlatFiles.WriteBack;

/// <summary>
/// Options for controlling how data is written back to disk.
/// </summary>
public sealed class WriteBackOptions
{
    /// <summary>
    /// Gets or sets the write-back mode. Default: <see cref="WriteBackMode.NewFile"/>.
    /// </summary>
    public WriteBackMode Mode { get; set; } = WriteBackMode.NewFile;

    /// <summary>
    /// Gets or sets the output file path. Required when Mode is <see cref="WriteBackMode.NewFile"/>.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Gets or sets the output format (e.g. <see cref="FileFormats.Csv"/>, <see cref="FileFormats.Parquet"/>).
    /// When null, the format is inferred from the file extension.
    /// </summary>
    public string? OutputFormat { get; set; }

    /// <summary>
    /// Gets or sets whether to include a header row in CSV/TSV output. Default: true.
    /// </summary>
    public bool IncludeHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets the delimiter character for CSV output. Default: ','.
    /// </summary>
    public char Delimiter { get; set; } = ',';
}