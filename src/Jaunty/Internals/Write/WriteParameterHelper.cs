using System.Data;
using Jaunty.Internals.Entity;

namespace Jaunty;

public static partial class Jaunty
{
    private static void PrepareInsertParameters(IDbCommand command, EntityMetadata metadata)
    {
        var columns = metadata.InsertColumns;

        for (int i = 0; i < columns.Count; i++)
        {
            ColumnMetadata col = columns[i];

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + col.ColumnName;
            command.Parameters.Add(param);
        }
    }

    private static void PrepareUpdateParameters(IDbCommand command, EntityMetadata metadata)
    {
        var updateColumns = metadata.UpdateColumns;
        var primaryKeys = metadata.PrimaryKeys;

        for (int i = 0; i < updateColumns.Count; i++)
        {
            ColumnMetadata col = updateColumns[i];

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + col.ColumnName;
            command.Parameters.Add(param);
        }

        for (int i = 0; i < primaryKeys.Count; i++)
        {
            ColumnMetadata key = primaryKeys[i];
            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + key.ColumnName;
            command.Parameters.Add(param);
        }
    }

    private static void PrepareDeleteParameters(IDbCommand command, EntityMetadata metadata)
    {
        var deleteColumns = metadata.DeleteColumns;

        for (int i = 0; i < deleteColumns.Count; i++)
        {
            ColumnMetadata key = deleteColumns[i];
            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + key.ColumnName;
            command.Parameters.Add(param);
        }
    }
}
