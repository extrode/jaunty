#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

namespace Jaunty.Internals.Entity;

/// <summary>
/// Metadata for an entity type, including table name and column mappings.
/// </summary>
public sealed class EntityMetadata
{
    /// <summary>
    /// Gets the table name.
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// Gets the schema name, or <see langword="null"/> if not specified.
    /// </summary>
    public string? SchemaName { get; }

    /// <summary>
    /// Gets all column metadata.
    /// </summary>
    public IReadOnlyList<ColumnMetadata> Columns { get; }

    /// <summary>
    /// Gets the primary key column metadata.
    /// </summary>
    public IReadOnlyList<ColumnMetadata> PrimaryKeys { get; }

    /// <summary>
    /// Gets the non-primary key column metadata.
    /// </summary>
    public IReadOnlyList<ColumnMetadata> NonPrimaryKeyColumns { get; }

    /// <summary>
    /// Gets the non-identity column metadata.
    /// </summary>
    public IReadOnlyList<ColumnMetadata> NonIdentityColumns { get; }

    /// <summary>
    /// Gets a value indicating whether the entity has an identity primary key.
    /// </summary>
    public bool HasIdentityKey => PrimaryKeys.Any(c => c.IsIdentity);

    /// <summary>
    /// Gets the columns used for INSERT operations (excludes identity and computed columns).
    /// </summary>
    public IReadOnlyList<ColumnMetadata> InsertColumns { get; }

    /// <summary>
    /// Gets the columns used for UPDATE operations (excludes primary key, identity, and computed columns).
    /// </summary>
    public IReadOnlyList<ColumnMetadata> UpdateColumns { get; }

    /// <summary>
    /// Gets the columns used for DELETE operations (primary key columns).
    /// </summary>
    public IReadOnlyList<ColumnMetadata> DeleteColumns { get; }

    /// <summary>
    /// Gets a read-only dictionary mapping column names to column metadata.
    /// Backed by a frozen dictionary on net8.0+ for O(1) case-insensitive lookups.
    /// </summary>
    public IReadOnlyDictionary<string, ColumnMetadata> ParameterMap { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityMetadata"/> class.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="schemaName">The schema name, or <see langword="null"/> if not specified.</param>
    /// <param name="columns">The column metadata.</param>
    public EntityMetadata(string tableName, string? schemaName, IEnumerable<ColumnMetadata> columns)
    {
        TableName = tableName;
        SchemaName = schemaName;

        List<ColumnMetadata> colList = columns is List<ColumnMetadata> list ? list : columns.ToList();

        Columns = colList.AsReadOnly();

        var primaryKeys = new List<ColumnMetadata>(colList.Count);
        var nonPrimaryKeys = new List<ColumnMetadata>(colList.Count);
        var nonIdentity = new List<ColumnMetadata>(colList.Count);
        var insertColumns = new List<ColumnMetadata>(colList.Count);
        var updateColumns = new List<ColumnMetadata>(colList.Count);

        for (int i = 0; i < colList.Count; i++)
        {
            ColumnMetadata col = colList[i];

            if (col.IsPrimaryKey)
                primaryKeys.Add(col);
            else
                nonPrimaryKeys.Add(col);

            if (!col.IsIdentity)
                nonIdentity.Add(col);

            if (!col.IsIdentity && !col.IsComputed)
                insertColumns.Add(col);

            if (!col.IsPrimaryKey && !col.IsIdentity && !col.IsComputed)
                updateColumns.Add(col);
        }

        PrimaryKeys = primaryKeys.AsReadOnly();
        NonPrimaryKeyColumns = nonPrimaryKeys.AsReadOnly();
        NonIdentityColumns = nonIdentity.AsReadOnly();

        InsertColumns = insertColumns.AsReadOnly();
        UpdateColumns = updateColumns.AsReadOnly();
        DeleteColumns = primaryKeys.AsReadOnly();

#if NET8_0_OR_GREATER
        ParameterMap = colList.ToFrozenDictionary(c => c.ColumnName, CommonConstants.OrdinalIgnoreCase);
#else
        var parameterMap = new Dictionary<string, ColumnMetadata>(colList.Count, CommonConstants.OrdinalIgnoreCase);
        for (int i = 0; i < colList.Count; i++)
        {
            parameterMap[colList[i].ColumnName] = colList[i];
        }
        ParameterMap = parameterMap;
#endif
    }
}