namespace Jaunty.Scaffolding.Schema;

/// <summary>
/// Represents a database column's schema information.
/// </summary>
public sealed class ColumnSchema
{
    /// <summary>
    /// The name of the column in the database.
    /// </summary>
    public required string ColumnName { get; init; }

    /// <summary>
    /// The raw database type (e.g., "int", "varchar", "nvarchar(100)").
    /// </summary>
    public required string DataType { get; init; }

    /// <summary>
    /// Whether the column allows NULL values.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Whether the column is part of the primary key.
    /// </summary>
    public bool IsPrimaryKey { get; init; }

    /// <summary>
    /// Whether the column is an identity/auto-increment column.
    /// </summary>
    public bool IsIdentity { get; init; }

    /// <summary>
    /// Whether the column is a computed column.
    /// </summary>
    public bool IsComputed { get; init; }

    /// <summary>
    /// Maximum length for string/binary types, or null if not applicable.
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// Precision for numeric types, or null if not applicable.
    /// </summary>
    public int? Precision { get; init; }

    /// <summary>
    /// Scale for numeric types, or null if not applicable.
    /// </summary>
    public int? Scale { get; init; }

    /// <summary>
    /// Default value expression, or null if none.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// The ordinal position of the column in the table (1-based).
    /// </summary>
    public int OrdinalPosition { get; init; }

    /// <summary>
    /// The full raw database-reported column type, including type modifiers/display width
    /// where the provider exposes them (e.g. MySQL's COLUMN_TYPE "tinyint(1)", "bit(1)").
    /// Null when the schema reader doesn't provide this level of detail.
    /// </summary>
    public string? ColumnType { get; init; }
}