namespace Jaunty.Scaffolding.Schema;

/// <summary>
/// Represents the complete schema of a database.
/// </summary>
public sealed class DatabaseSchema
{
    /// <summary>
    /// The name of the database.
    /// </summary>
    public string DatabaseName { get; init; } = string.Empty;

    /// <summary>
    /// All tables in the database.
    /// </summary>
    public required IReadOnlyList<TableSchema> Tables { get; init; }
}