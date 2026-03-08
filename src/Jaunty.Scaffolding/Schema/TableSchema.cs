namespace Jaunty.Scaffolding.Schema;

/// <summary>
/// Represents a database table's schema information.
/// </summary>
public sealed class TableSchema
{
    /// <summary>
    /// The schema name (e.g., "dbo" for SQL Server, "public" for PostgreSQL).
    /// Empty string for databases that don't support schemas (SQLite, MySQL).
    /// </summary>
    public string SchemaName { get; init; } = string.Empty;

    /// <summary>
    /// The name of the table.
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// The columns in this table.
    /// </summary>
    public required IReadOnlyList<ColumnSchema> Columns { get; init; }

    /// <summary>
    /// Primary key information, or null if no primary key exists.
    /// </summary>
    public PrimaryKeyInfo? PrimaryKey { get; init; }

    /// <summary>
    /// Foreign key constraints on this table.
    /// </summary>
    public IReadOnlyList<ForeignKeyInfo> ForeignKeys { get; init; } = [];
}