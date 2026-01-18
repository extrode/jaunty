namespace Jaunty.Scaffolding.Schema;

/// <summary>
/// Represents primary key constraint information.
/// </summary>
public sealed class PrimaryKeyInfo
{
    /// <summary>
    /// The name of the primary key constraint.
    /// </summary>
    public required string ConstraintName { get; init; }

    /// <summary>
    /// The columns that make up the primary key, in order.
    /// </summary>
    public required IReadOnlyList<string> Columns { get; init; }
}
