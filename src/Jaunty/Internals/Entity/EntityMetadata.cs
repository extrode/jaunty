#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

namespace Jaunty.Internals.Entity;

/// <summary>
/// Metadata for an entity type, including table name and column mappings.
/// </summary>
internal sealed class EntityMetadata
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

        // AUD-R35-114 (round-35 batch 04b). This used to adopt the caller's list by reference when it
        // already was a List<ColumnMetadata>, and Columns is a ReadOnlyCollection *view* over it while
        // PrimaryKeys, NonPrimaryKeyColumns, NonIdentityColumns, InsertColumns, UpdateColumns,
        // DeleteColumns and ParameterMap are all snapshots taken below. A caller mutating its list
        // afterwards therefore desynchronised Columns from every derived collection and slipped past
        // ThrowIfDuplicateColumnNames, which runs once here. Metadata is built once per entity type
        // and cached, so the copy costs one allocation per type and buys immutability outright.
        var colList = new List<ColumnMetadata>(columns);

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

        ThrowIfDuplicateColumnNames(tableName, colList);

#if NET8_0_OR_GREATER
        ParameterMap = colList.ToFrozenDictionary(c => c.ColumnName, CommonConstants.OrdinalIgnoreCase);
#else
        var parameterMap = new Dictionary<string, ColumnMetadata>(colList.Count, CommonConstants.OrdinalIgnoreCase);
        for (int i = 0; i < colList.Count; i++)
            parameterMap[colList[i].ColumnName] = colList[i];
        ParameterMap = parameterMap;
#endif
    }

    /// <summary>
    /// Rejects two properties mapping onto one column, naming both of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26. Two properties sharing a column name was already rejected - it is a duplicate key
    /// in <see cref="ParameterMap"/> - but by whichever dictionary happened to build it, so the
    /// caller got <c>"An item with the same key has already been added. Key: status"</c> wrapped in
    /// a <see cref="TypeInitializationException"/>, naming neither the entity nor either property.
    /// The audit that raised this expected a silent last-wins collapse instead; measuring showed
    /// the rejection was already there and it was only the diagnostic that was missing.
    /// </para>
    /// <para>
    /// Checked explicitly rather than left to the dictionary so that both target frameworks report
    /// the same thing - the netstandard2.0 branch previously hand-copied .NET's own wording to keep
    /// that parity, which is the cross-TFM consistency rule from round 3.
    /// </para>
    /// </remarks>
    private static void ThrowIfDuplicateColumnNames(string tableName, List<ColumnMetadata> colList)
    {
        var seen = new Dictionary<string, string>(colList.Count, CommonConstants.OrdinalIgnoreCase);

        for (int i = 0; i < colList.Count; i++)
        {
            ColumnMetadata col = colList[i];

            if (seen.TryGetValue(col.ColumnName, out string? firstProperty))
            {
                throw new ArgumentException(
                    $"Entity for table '{tableName}' maps more than one property to column '{col.ColumnName}': " +
                    $"'{firstProperty}' and '{col.PropertyName}'. Column names are matched case-insensitively, " +
                    "so two properties cannot share one. Give each its own [Column(\"...\")] name, or remove " +
                    "the duplicate mapping.",
                    "columns");
            }

            seen[col.ColumnName] = col.PropertyName;
        }
    }
}