namespace Jaunty.FlatFiles;

/// <summary>
/// Specifies how primary key conflicts are handled during import.
/// </summary>
public enum ConflictStrategy
{
    /// <summary>
    /// Throw an exception on primary key conflict. This is the default.
    /// </summary>
    Error,

    /// <summary>
    /// Skip conflicting rows and continue importing the remaining rows.
    /// </summary>
    Skip,

    /// <summary>
    /// Update existing rows when a primary key conflict occurs (upsert).
    /// </summary>
    Upsert
}
