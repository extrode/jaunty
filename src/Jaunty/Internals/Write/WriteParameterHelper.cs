using System.Data;
using Jaunty.Internals.Entity;

namespace Jaunty;

public static partial class Jaunty
{
    private static void PrepareInsertParameters(IDbCommand command, EntityMetadata metadata)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.NonIdentityColumns;

        for (int i = 0; i < columns.Count; i++)
        {
            ColumnMetadata col = columns[i];
            if (col.IsComputed)
                continue;

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + col.Property.Name;
            command.Parameters.Add(param);
        }
    }

    private static void PrepareUpdateParameters(IDbCommand command, EntityMetadata metadata)
    {
        IReadOnlyList<ColumnMetadata> allColumns = metadata.Columns;
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;

        // SET clause parameters
        for (int i = 0; i < allColumns.Count; i++)
        {
            ColumnMetadata col = allColumns[i];
            if (col.IsPrimaryKey || col.IsIdentity || col.IsComputed)
                continue;

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + col.Property.Name;
            command.Parameters.Add(param);
        }

        // WHERE clause parameters (primary keys)
        for (int i = 0; i < primaryKeys.Count; i++)
        {
            ColumnMetadata key = primaryKeys[i];
            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + key.Property.Name;
            command.Parameters.Add(param);
        }
    }

    private static void PrepareDeleteParameters(IDbCommand command, EntityMetadata metadata)
    {
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;

        for (int i = 0; i < primaryKeys.Count; i++)
        {
            ColumnMetadata key = primaryKeys[i];
            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + key.Property.Name;
            command.Parameters.Add(param);
        }
    }
}
