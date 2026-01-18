namespace Jaunty.Scaffolding.Abstractions;

/// <summary>
/// Options for filtering which tables and schemas to read.
/// </summary>
public sealed class SchemaReaderOptions
{
    /// <summary>
    /// If specified, only include these tables. Null means include all tables.
    /// </summary>
    public IReadOnlyList<string>? IncludeTables { get; init; }

    /// <summary>
    /// Tables to exclude from scaffolding. Applied after IncludeTables.
    /// </summary>
    public IReadOnlyList<string>? ExcludeTables { get; init; }

    /// <summary>
    /// If specified, only include tables from these schemas. Null means include all schemas.
    /// </summary>
    public IReadOnlyList<string>? IncludeSchemas { get; init; }

    /// <summary>
    /// Whether to read foreign key information. Default is false for performance.
    /// </summary>
    public bool IncludeForeignKeys { get; init; }
}
