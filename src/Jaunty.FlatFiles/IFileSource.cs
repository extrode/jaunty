namespace Jaunty.FlatFiles;

/// <summary>
/// Represents a flat file data source that can be registered with an <see cref="IFlatFileDatabase"/>.
/// </summary>
public interface IFileSource
{
    /// <summary>
    /// Gets the logical table name used in SQL queries.
    /// </summary>
    string TableName { get; }

    /// <summary>
    /// Gets the file path pointing to the data file.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets the file format.
    /// </summary>
    FileFormat Format { get; }

    /// <summary>
    /// Gets the entity type associated with this file source.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// Gets whether this source has been promoted from a VIEW to a TABLE (for mutation support).
    /// </summary>
    bool IsPromotedToTable { get; }

    /// <summary>
    /// Gets whether this source was preloaded into memory as a TABLE.
    /// </summary>
    bool IsPreloaded { get; }
}
