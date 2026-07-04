using System;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Jaunty.Configuration;

namespace Jaunty.Extensions.Reflection.BulkCopy;

/// <summary>
/// SQL Server bulk copy provider using SqlBulkCopy via reflection.
/// Uses reflection to avoid hard dependency on Microsoft.Data.SqlClient or System.Data.SqlClient.
/// </summary>
internal sealed class SqlServerBulkCopyProvider : IBulkCopyProvider
{
    private static readonly Type? SqlBulkCopyType = Type.GetType("Microsoft.Data.SqlClient.SqlBulkCopy, Microsoft.Data.SqlClient")
        ?? Type.GetType("System.Data.SqlClient.SqlBulkCopy, System.Data");

    private static readonly Type? SqlBulkCopyOptionsType = Type.GetType("Microsoft.Data.SqlClient.SqlBulkCopyOptions, Microsoft.Data.SqlClient")
        ?? Type.GetType("System.Data.SqlClient.SqlBulkCopyOptions, System.Data");

    private static readonly PropertyInfo? BatchSizeProperty = SqlBulkCopyType?.GetProperty("BatchSize");
    private static readonly PropertyInfo? BulkCopyTimeoutProperty = SqlBulkCopyType?.GetProperty("BulkCopyTimeout");
    private static readonly PropertyInfo? DestinationTableNameProperty = SqlBulkCopyType?.GetProperty("DestinationTableName");
    private static readonly PropertyInfo? ColumnMappingsProperty = SqlBulkCopyType?.GetProperty("ColumnMappings");
    private static readonly MethodInfo? ColumnMappingsAddMethod = ColumnMappingsProperty?.PropertyType.GetMethod("Add", [typeof(string), typeof(string)]);
    private static readonly MethodInfo? WriteToServerMethod = SqlBulkCopyType?.GetMethod("WriteToServer", [typeof(IDataReader)]);
    private static readonly MethodInfo? WriteToServerAsyncMethod = SqlBulkCopyType?.GetMethod("WriteToServerAsync", [typeof(IDataReader), typeof(CancellationToken)]);

    /// <inheritdoc/>
    public bool IsSupported => SqlBulkCopyType != null;

    /// <inheritdoc/>
    public int CopyToServer(IDbConnection connection, string tableName, IDataReader data, BulkCopyOptions options)
    {
        if (SqlBulkCopyType == null)
            throw new InvalidOperationException("SqlBulkCopy is not available. Ensure Microsoft.Data.SqlClient or System.Data.SqlClient is installed.");

        DbConnection sqlConnection = connection as DbConnection
            ?? throw new ArgumentException("Connection must be a SqlConnection.", nameof(connection));

        // Create SqlBulkCopy — constructor is always (SqlConnection, SqlBulkCopyOptions, SqlTransaction?)
        object? bulkCopyOptions = MapBulkCopyOptions(options);
        object? bulkCopy = Activator.CreateInstance(SqlBulkCopyType, sqlConnection, bulkCopyOptions, options.Transaction) ?? throw new InvalidOperationException("Failed to create SqlBulkCopy instance.");

        try
        {
            BatchSizeProperty?.SetValue(bulkCopy, options.BatchSize);
            BulkCopyTimeoutProperty?.SetValue(bulkCopy, options.Timeout);
            DestinationTableNameProperty?.SetValue(bulkCopy, tableName);
            ApplyColumnMappings(bulkCopy, data);

            WriteToServerMethod?.Invoke(bulkCopy, new object[] { data });

            // SqlBulkCopy doesn't expose row count; return -1 and let the caller use entityList.Count
            return -1;
        }
        finally
        {
            (bulkCopy as IDisposable)?.Dispose();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> CopyToServerAsync(DbConnection connection, string tableName, IDataReader data, BulkCopyOptions options, CancellationToken cancellationToken)
    {
        if (SqlBulkCopyType == null)
            throw new InvalidOperationException("SqlBulkCopy is not available. Ensure Microsoft.Data.SqlClient or System.Data.SqlClient is installed.");

        // Create SqlBulkCopy — constructor is always (SqlConnection, SqlBulkCopyOptions, SqlTransaction?)
        object? bulkCopyOptions = MapBulkCopyOptions(options);
        object? bulkCopy = Activator.CreateInstance(SqlBulkCopyType, connection, bulkCopyOptions, options.Transaction) ?? throw new InvalidOperationException("Failed to create SqlBulkCopy instance.");

        try
        {
            BatchSizeProperty?.SetValue(bulkCopy, options.BatchSize);
            BulkCopyTimeoutProperty?.SetValue(bulkCopy, options.Timeout);
            DestinationTableNameProperty?.SetValue(bulkCopy, tableName);
            ApplyColumnMappings(bulkCopy, data);

            if (WriteToServerAsyncMethod != null)
                await ((Task)WriteToServerAsyncMethod.Invoke(bulkCopy, new object[] { data, cancellationToken })!).ConfigureAwait(false);
            else
                WriteToServerMethod?.Invoke(bulkCopy, [data]);

            // SqlBulkCopy doesn't expose row count; return -1 and let the caller use entityList.Count
            return -1;
        }
        finally
        {
            (bulkCopy as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Maps each source column to the same-named destination column.
    /// SqlBulkCopy maps by ordinal when no mappings are set, which breaks when the
    /// destination table has columns absent from the source (e.g. an IDENTITY key).
    /// </summary>
    private static void ApplyColumnMappings(object bulkCopy, IDataReader data)
    {
        if (ColumnMappingsProperty == null || ColumnMappingsAddMethod == null)
            return;

        object? mappings = ColumnMappingsProperty.GetValue(bulkCopy);
        if (mappings == null)
            return;

        for (int i = 0; i < data.FieldCount; i++)
        {
            string name = data.GetName(i);
            ColumnMappingsAddMethod.Invoke(mappings, [name, name]);
        }
    }

    /// <summary>
    /// Maps BulkCopyOptions to SqlBulkCopyOptions using reflection.
    /// </summary>
    private static object MapBulkCopyOptions(BulkCopyOptions options)
    {
        if (SqlBulkCopyOptionsType == null)
            return 0; // Default options

        int result = 0;

        // Map TableLock option
        if (options.TableLock == TableLockOption.BulkLock)
        {
            object? tableLockValue = SqlBulkCopyOptionsType.GetField("TableLock")?.GetValue(null);
            if (tableLockValue != null)
                result |= (int)tableLockValue;
        }

        // Map IdentityMode option
        if (options.IdentityMode == BulkCopyIdentityMode.KeepIdentity)
        {
            object? keepIdentityValue = SqlBulkCopyOptionsType.GetField("KeepIdentity")?.GetValue(null);
            if (keepIdentityValue != null)
                result |= (int)keepIdentityValue;
        }

        // Map CheckConstraints option
        if (options.CheckConstraints)
        {
            object? checkConstraintsValue = SqlBulkCopyOptionsType.GetField("CheckConstraints")?.GetValue(null);
            if (checkConstraintsValue != null)
                result |= (int)checkConstraintsValue;
        }

        // A boxed Int32 does not bind to the SqlBulkCopyOptions constructor parameter
        // (Activator throws MissingMethodException); box the actual enum value instead.
        return Enum.ToObject(SqlBulkCopyOptionsType, result);
    }

}