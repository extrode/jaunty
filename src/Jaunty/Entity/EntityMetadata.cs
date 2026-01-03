namespace Jaunty.Entity;

internal sealed class EntityMetadata
{
    public string TableName { get; }
    public string? SchemaName { get; }

    public IReadOnlyList<ColumnMetadata> Columns { get; }
    public IReadOnlyList<ColumnMetadata> PrimaryKeys { get; }
    public IReadOnlyList<ColumnMetadata> NonPrimaryKeyColumns { get; }
    public IReadOnlyList<ColumnMetadata> NonIdentityColumns { get; }

    public EntityMetadata(string tableName, string? schemaName, IEnumerable<ColumnMetadata> columns)
    {
        TableName = tableName;
        SchemaName = schemaName;

        var colList = columns is List<ColumnMetadata> list ? list : [.. columns];

        Columns = colList.AsReadOnly();

        var primaryKeys = new List<ColumnMetadata>(colList.Count);
        var nonPrimaryKeys = new List<ColumnMetadata>(colList.Count);
        var nonIdentity = new List<ColumnMetadata>(colList.Count);

        for (int i = 0; i < colList.Count; i++)
        {
            var col = colList[i];

            if (col.IsPrimaryKey)
                primaryKeys.Add(col);
            else
                nonPrimaryKeys.Add(col);

            if (!col.IsIdentity)
                nonIdentity.Add(col);
        }

        PrimaryKeys = primaryKeys.AsReadOnly();
        NonPrimaryKeyColumns = nonPrimaryKeys.AsReadOnly();
        NonIdentityColumns = nonIdentity.AsReadOnly();
    }
}
