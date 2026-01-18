namespace Jaunty.Scaffolding.Schema;

/// <summary>
/// Represents foreign key constraint information.
/// </summary>
public sealed class ForeignKeyInfo
{
    /// <summary>
    /// The name of the foreign key constraint.
    /// </summary>
    public required string ConstraintName { get; init; }

    /// <summary>
    /// The column in the current table that references another table.
    /// </summary>
    public required string ForeignKeyColumn { get; init; }

    /// <summary>
    /// The schema of the referenced table.
    /// </summary>
    public string? ReferencedSchema { get; init; }

    /// <summary>
    /// The name of the referenced table.
    /// </summary>
    public required string ReferencedTable { get; init; }

    /// <summary>
    /// The column in the referenced table.
    /// </summary>
    public required string ReferencedColumn { get; init; }
}
