namespace Jaunty.Internals.Entity;

public sealed class EntityMetadata
{
    public string TableName { get; }
    public string? SchemaName { get; }

    public IReadOnlyList<ColumnMetadata> Columns { get; }
    public IReadOnlyList<ColumnMetadata> PrimaryKeys { get; }
    public IReadOnlyList<ColumnMetadata> NonPrimaryKeyColumns { get; }
    public IReadOnlyList<ColumnMetadata> NonIdentityColumns { get; }

    public bool HasIdentityKey => PrimaryKeys.Any(c => c.IsIdentity);

    public IReadOnlyList<ColumnMetadata> InsertColumns { get; }
    public IReadOnlyList<ColumnMetadata> UpdateColumns { get; }
    public IReadOnlyList<ColumnMetadata> DeleteColumns { get; }

    public Dictionary<string, ColumnMetadata> ParameterMap { get; }

    public EntityMetadata(string tableName, string? schemaName, IEnumerable<ColumnMetadata> columns)
    {
        TableName = tableName;
        SchemaName = schemaName;

        var colList = columns is List<ColumnMetadata> list ? list : columns.ToList();

        Columns = colList.AsReadOnly();

        var primaryKeys = new List<ColumnMetadata>(colList.Count);
        var nonPrimaryKeys = new List<ColumnMetadata>(colList.Count);
        var nonIdentity = new List<ColumnMetadata>(colList.Count);
        var insertColumns = new List<ColumnMetadata>(colList.Count);
        var updateColumns = new List<ColumnMetadata>(colList.Count);

        for (int i = 0; i < colList.Count; i++)
        {
            var col = colList[i];

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

        ParameterMap = new Dictionary<string, ColumnMetadata>(colList.Count, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < colList.Count; i++)
        {
            ParameterMap[colList[i].ColumnName] = colList[i];
        }
    }
}
