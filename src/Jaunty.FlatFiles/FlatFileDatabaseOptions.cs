namespace Jaunty.FlatFiles;

/// <summary>
/// Configuration options for creating a flat file database instance.
/// </summary>
public sealed class FlatFileDatabaseOptions
{
    /// <summary>
    /// Gets or sets the path to the database file. Use ":memory:" for in-memory databases.
    /// Default: ":memory:".
    /// </summary>
    public string DatabasePath { get; set; } = ":memory:";

    /// <summary>
    /// Gets or sets whether to open the connection immediately on creation.
    /// Default: true.
    /// </summary>
    public bool AutoOpen { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to register the dialect with <c>SqlDialectFactory</c>.
    /// Default: true.
    /// </summary>
    public bool RegisterDialect { get; set; } = true;

    /// <summary>
    /// Gets the file sources to register on creation.
    /// </summary>
    public List<IFileSource> Sources { get; } = new();
}
