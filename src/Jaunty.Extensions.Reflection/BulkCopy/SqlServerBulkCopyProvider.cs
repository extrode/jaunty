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

    private static readonly Type? SqlConnectionType = Type.GetType("Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient")
        ?? Type.GetType("System.Data.SqlClient.SqlConnection, System.Data");

    private static readonly Type? SqlTransactionType = Type.GetType("Microsoft.Data.SqlClient.SqlTransaction, Microsoft.Data.SqlClient")
        ?? Type.GetType("System.Data.SqlClient.SqlTransaction, System.Data");

    private static readonly PropertyInfo? RowsCopiedProperty = SqlBulkCopyType?.GetProperty("RowsCopied");
    private static readonly PropertyInfo? BatchSizeProperty = SqlBulkCopyType?.GetProperty("BatchSize");
    private static readonly PropertyInfo? BulkCopyTimeoutProperty = SqlBulkCopyType?.GetProperty("BulkCopyTimeout");
    private static readonly PropertyInfo? DestinationTableNameProperty = SqlBulkCopyType?.GetProperty("DestinationTableName");
    private static readonly PropertyInfo? ColumnMappingsProperty = SqlBulkCopyType?.GetProperty("ColumnMappings");

    // AUD-R25: BulkCopyOptions.EnableStreaming is public, defaults to true and is documented as
    // "rows are streamed to the database without buffering", but no provider read it - setting it
    // to false changed nothing. On SQL Server it maps directly onto SqlBulkCopy.EnableStreaming,
    // which this provider never set despite reflecting over five other SqlBulkCopy properties.
    private static readonly PropertyInfo? EnableStreamingProperty = SqlBulkCopyType?.GetProperty("EnableStreaming");
    private static readonly MethodInfo? ColumnMappingsAddMethod = ColumnMappingsProperty?.PropertyType.GetMethod("Add", [typeof(string), typeof(string)]);
    private static readonly MethodInfo? WriteToServerMethod = SqlBulkCopyType?.GetMethod("WriteToServer", [typeof(IDataReader)]);
    private static readonly MethodInfo? WriteToServerAsyncMethod = SqlBulkCopyType?.GetMethod("WriteToServerAsync", [typeof(IDataReader), typeof(CancellationToken)]);

    /// <inheritdoc/>
    public bool IsSupported => SqlBulkCopyType != null;

    /// <inheritdoc/>
    public int CopyToServer(IDbConnection connection, string? schemaName, string tableName, IDataReader data, BulkCopyOptions options)
    {
        if (SqlBulkCopyType == null)
            throw new InvalidOperationException("SqlBulkCopy is not available. Ensure Microsoft.Data.SqlClient or System.Data.SqlClient is installed.");

        if (SqlConnectionType == null || !SqlConnectionType.IsInstanceOfType(connection))
            throw new ArgumentException("Connection must be a SqlConnection.", nameof(connection));

        if (WriteToServerMethod == null)
            throw new InvalidOperationException("SqlBulkCopy.WriteToServer could not be resolved via reflection.");

        ValidateTransaction(options.Transaction);

        DbConnection sqlConnection = (DbConnection)connection;

        // Create SqlBulkCopy — constructor is always (SqlConnection, SqlBulkCopyOptions, SqlTransaction?)
        object? bulkCopyOptions = MapBulkCopyOptions(options);
        object? bulkCopy = Activator.CreateInstance(SqlBulkCopyType, sqlConnection, bulkCopyOptions, options.Transaction) ?? throw new InvalidOperationException("Failed to create SqlBulkCopy instance.");

        try
        {
            BatchSizeProperty?.SetValue(bulkCopy, options.BatchSize);
            BulkCopyTimeoutProperty?.SetValue(bulkCopy, options.Timeout);
            EnableStreamingProperty?.SetValue(bulkCopy, options.EnableStreaming);
            DestinationTableNameProperty?.SetValue(bulkCopy, QualifyTableName(schemaName, tableName));
            ApplyColumnMappings(bulkCopy, data);

            WriteToServerMethod.Invoke(bulkCopy, new object[] { data });

            return RowsCopiedProperty != null ? (int)RowsCopiedProperty.GetValue(bulkCopy)! : -1;
        }
        finally
        {
            (bulkCopy as IDisposable)?.Dispose();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> CopyToServerAsync(DbConnection connection, string? schemaName, string tableName, IDataReader data, BulkCopyOptions options, CancellationToken cancellationToken)
    {
        if (SqlBulkCopyType == null)
            throw new InvalidOperationException("SqlBulkCopy is not available. Ensure Microsoft.Data.SqlClient or System.Data.SqlClient is installed.");

        if (SqlConnectionType == null || !SqlConnectionType.IsInstanceOfType(connection))
            throw new ArgumentException("Connection must be a SqlConnection.", nameof(connection));

        if (WriteToServerAsyncMethod == null && WriteToServerMethod == null)
            throw new InvalidOperationException("SqlBulkCopy.WriteToServer/WriteToServerAsync could not be resolved via reflection.");

        ValidateTransaction(options.Transaction);

        // Create SqlBulkCopy — constructor is always (SqlConnection, SqlBulkCopyOptions, SqlTransaction?)
        object? bulkCopyOptions = MapBulkCopyOptions(options);
        object? bulkCopy = Activator.CreateInstance(SqlBulkCopyType, connection, bulkCopyOptions, options.Transaction) ?? throw new InvalidOperationException("Failed to create SqlBulkCopy instance.");

        try
        {
            BatchSizeProperty?.SetValue(bulkCopy, options.BatchSize);
            BulkCopyTimeoutProperty?.SetValue(bulkCopy, options.Timeout);
            EnableStreamingProperty?.SetValue(bulkCopy, options.EnableStreaming);
            DestinationTableNameProperty?.SetValue(bulkCopy, QualifyTableName(schemaName, tableName));
            ApplyColumnMappings(bulkCopy, data);

            if (WriteToServerAsyncMethod != null)
                await ((Task)WriteToServerAsyncMethod.Invoke(bulkCopy, new object[] { data, cancellationToken })!).ConfigureAwait(false);
            else
                WriteToServerMethod!.Invoke(bulkCopy, [data]);

            return RowsCopiedProperty != null ? (int)RowsCopiedProperty.GetValue(bulkCopy)! : -1;
        }
        finally
        {
            (bulkCopy as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Validates that an external transaction is a SqlTransaction before it reaches
    /// <see cref="Activator.CreateInstance(Type, object[])"/>. Without this a transaction from a
    /// different provider surfaces as an opaque reflection MissingMethodException/
    /// TargetInvocationException instead of naming the actual problem, unlike the
    /// connection-type mismatch a few lines above.
    /// </summary>
    private static void ValidateTransaction(IDbTransaction? transaction)
    {
        if (transaction is null)
            return;

        // If the SqlTransaction type itself can't be resolved, SqlBulkCopyType would have been
        // null too and the caller has already thrown - but stay silent rather than reject a
        // legitimate transaction on a type we simply couldn't look up.
        if (SqlTransactionType != null && !SqlTransactionType.IsInstanceOfType(transaction))
            throw new ArgumentException(
                $"BulkCopyOptions.Transaction must be a SqlTransaction, but was {transaction.GetType().FullName}.",
                nameof(transaction));
    }

    /// <summary>
    /// Combines schema and table into the "schema.table" form SqlBulkCopy.DestinationTableName
    /// accepts natively. Validated (not escaped) the same way MySqlBulkCopyProvider and
    /// PostgreSqlBulkCopyProvider validate their table/schema names, so a malformed or malicious
    /// name is rejected up front rather than passed through to SqlBulkCopy's own resolution.
    /// </summary>
    private static string QualifyTableName(string? schemaName, string tableName)
    {
        global::Jaunty.Dialects.SqlIdentifierValidator.Validate(tableName, nameof(tableName), global::Jaunty.Dialects.SqlIdentifierFlavor.SqlServer);
        if (schemaName is null || schemaName.Length == 0)
            return tableName;

        global::Jaunty.Dialects.SqlIdentifierValidator.Validate(schemaName, nameof(schemaName), global::Jaunty.Dialects.SqlIdentifierFlavor.SqlServer);
        return $"{schemaName}.{tableName}";
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
            throw new InvalidOperationException("SqlBulkCopyOptions could not be resolved via reflection.");

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