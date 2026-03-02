using System;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Jaunty.Configuration;
using Jaunty.Internals.BulkCopy;

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

    private static readonly PropertyInfo? ColumnMappingsProperty = SqlBulkCopyType?.GetProperty("ColumnMappings");
    private static readonly PropertyInfo? BatchSizeProperty = SqlBulkCopyType?.GetProperty("BatchSize");
    private static readonly PropertyInfo? BulkCopyTimeoutProperty = SqlBulkCopyType?.GetProperty("BulkCopyTimeout");
    private static readonly PropertyInfo? DestinationTableNameProperty = SqlBulkCopyType?.GetProperty("DestinationTableName");
    private static readonly MethodInfo? WriteToServerMethod = SqlBulkCopyType?.GetMethod("WriteToServer", new[] { typeof(IDataReader) });
    private static readonly MethodInfo? WriteToServerAsyncMethod = SqlBulkCopyType?.GetMethod("WriteToServerAsync", new[] { typeof(IDataReader), typeof(CancellationToken) });
    private static readonly MethodInfo? DisposeMethod = SqlBulkCopyType?.GetMethod("Dispose");

    /// <inheritdoc/>
    public bool IsSupported => SqlBulkCopyType != null;

    /// <inheritdoc/>
    public int CopyToServer(IDbConnection connection, string tableName, IDataReader data, BulkCopyOptions options)
    {
        if (SqlBulkCopyType == null)
            throw new InvalidOperationException("SqlBulkCopy is not available. Ensure Microsoft.Data.SqlClient or System.Data.SqlClient is installed.");

        var sqlConnection = connection as DbConnection
            ?? throw new ArgumentException("Connection must be a SqlConnection.", nameof(connection));

        // Create SqlBulkCopy instance with appropriate options
        var bulkCopyOptions = MapBulkCopyOptions(options);
        object? sqlTransaction = options.Transaction;

        var bulkCopy = sqlTransaction != null
            ? Activator.CreateInstance(SqlBulkCopyType, sqlConnection, bulkCopyOptions, sqlTransaction)
            : Activator.CreateInstance(SqlBulkCopyType, sqlConnection, bulkCopyOptions);

        if (bulkCopy == null)
            throw new InvalidOperationException("Failed to create SqlBulkCopy instance.");

        try
        {
            // Configure bulk copy
            BatchSizeProperty?.SetValue(bulkCopy, options.BatchSize);
            BulkCopyTimeoutProperty?.SetValue(bulkCopy, options.Timeout);
            DestinationTableNameProperty?.SetValue(bulkCopy, tableName);

            // Column mappings are handled automatically by SqlBulkCopy when using IDataReader
            // Column names from IDataReader.GetName() are matched to destination columns

            // Execute bulk copy
            WriteToServerMethod?.Invoke(bulkCopy, new object[] { data });

            return GetRowCount(data);
        }
        finally
        {
            DisposeMethod?.Invoke(bulkCopy, null);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> CopyToServerAsync(
        DbConnection connection,
        string tableName,
        IDataReader data,
        BulkCopyOptions options,
        CancellationToken cancellationToken)
    {
        if (SqlBulkCopyType == null)
            throw new InvalidOperationException("SqlBulkCopy is not available. Ensure Microsoft.Data.SqlClient or System.Data.SqlClient is installed.");

        var sqlConnection = connection as SqlConnection
            ?? throw new ArgumentException("Connection must be a SqlConnection.", nameof(connection));

        var bulkCopyOptions = MapBulkCopyOptions(options);
        object? sqlTransaction = options.Transaction;

        var bulkCopy = sqlTransaction != null
            ? Activator.CreateInstance(SqlBulkCopyType, sqlConnection, bulkCopyOptions, sqlTransaction)
            : Activator.CreateInstance(SqlBulkCopyType, sqlConnection, bulkCopyOptions);

        if (bulkCopy == null)
            throw new InvalidOperationException("Failed to create SqlBulkCopy instance.");

        try
        {
            BatchSizeProperty?.SetValue(bulkCopy, options.BatchSize);
            BulkCopyTimeoutProperty?.SetValue(bulkCopy, options.Timeout);
            DestinationTableNameProperty?.SetValue(bulkCopy, tableName);

            if (WriteToServerAsyncMethod != null)
            {
                await (Task)WriteToServerAsyncMethod.Invoke(bulkCopy, new object[] { data, cancellationToken })!;
            }
            else
            {
                // Fallback to sync method if async is not available
                WriteToServerMethod?.Invoke(bulkCopy, new object[] { data });
            }

            return GetRowCount(data);
        }
        finally
        {
            DisposeMethod?.Invoke(bulkCopy, null);
        }
    }

    /// <summary>
    /// Maps BulkCopyOptions to SqlBulkCopyOptions using reflection.
    /// </summary>
    private static object MapBulkCopyOptions(BulkCopyOptions options)
    {
        if (SqlBulkCopyOptionsType == null)
            return 0; // Default options

        var result = 0;

        // Map TableLock option
        if (options.TableLock == TableLockOption.BulkLock)
        {
            var tableLockValue = SqlBulkCopyOptionsType.GetField("TableLock")?.GetValue(null);
            if (tableLockValue != null)
                result |= (int)tableLockValue;
        }

        // Map IdentityMode option
        if (options.IdentityMode == BulkCopyIdentityMode.KeepIdentity)
        {
            var keepIdentityValue = SqlBulkCopyOptionsType.GetField("KeepIdentity")?.GetValue(null);
            if (keepIdentityValue != null)
                result |= (int)keepIdentityValue;
        }

        // Map CheckConstraints option
        if (options.CheckConstraints)
        {
            var checkConstraintsValue = SqlBulkCopyOptionsType.GetField("CheckConstraints")?.GetValue(null);
            if (checkConstraintsValue != null)
                result |= (int)checkConstraintsValue;
        }

        return result;
    }

    /// <summary>
    /// Gets the row count from an IDataReader by consuming it.
    /// This is a best-effort approach since IDataReader doesn't expose count directly.
    /// </summary>
    private static int GetRowCount(IDataReader data)
    {
        // Note: This consumes the data reader, so it should only be called after WriteToServer
        // In practice, the caller should track the count separately
        return -1; // Return -1 to indicate unknown (common pattern for bulk operations)
    }
}
