using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Jaunty.Configuration;
using Jaunty.Internals.BulkCopy;

namespace Jaunty.Extensions.Reflection.BulkCopy;

/// <summary>
/// PostgreSQL bulk copy provider using NpgsqlBinaryImporter via reflection.
/// Uses reflection to avoid hard dependency on Npgsql.
/// </summary>
internal sealed class PostgreSqlBulkCopyProvider : IBulkCopyProvider
{
    private static readonly Type? NpgsqlConnectionType = Type.GetType("Npgsql.NpgsqlConnection, Npgsql")
        ?? Type.GetType("Npgsql.NpgsqlConnection, Npgsql.NetStandard");

    private static readonly Type? NpgsqlBinaryImporterType = Type.GetType("Npgsql.NpgsqlBinaryImporter, Npgsql")
        ?? Type.GetType("Npgsql.NpgsqlBinaryImporter, Npgsql.NetStandard");

    private static readonly MethodInfo? BeginBinaryImportMethod = NpgsqlConnectionType?.GetMethod("BeginBinaryImport", new[] { typeof(string) });
    private static readonly MethodInfo? StartRowMethod = NpgsqlBinaryImporterType?.GetMethod("StartRow");
    private static readonly MethodInfo? WriteNullMethod = NpgsqlBinaryImporterType?.GetMethod("WriteNull");
    private static readonly MethodInfo? CompleteMethod = NpgsqlBinaryImporterType?.GetMethod("Complete", Type.EmptyTypes);
    private static readonly MethodInfo? DisposeMethod = NpgsqlBinaryImporterType?.GetMethod("Dispose");

    // NpgsqlBinaryImporter.Write<T>(T value) is generic — we need to use MakeGenericMethod per type
    private static readonly MethodInfo? WriteGenericMethod = FindWriteGenericMethod();

    // Async methods available in Npgsql 6+
    private static readonly MethodInfo? BeginBinaryImportAsyncMethod = FindAsyncMethod("BeginBinaryImportAsync");
    private static readonly MethodInfo? StartRowAsyncMethod = NpgsqlBinaryImporterType?.GetMethod("StartRowAsync", new[] { typeof(CancellationToken) });
    private static readonly MethodInfo? WriteNullAsyncMethod = NpgsqlBinaryImporterType?.GetMethod("WriteNullAsync", new[] { typeof(CancellationToken) });
    private static readonly MethodInfo? CompleteAsyncMethod = NpgsqlBinaryImporterType?.GetMethod("CompleteAsync", new[] { typeof(CancellationToken) });
    private static readonly MethodInfo? DisposeAsyncMethod = NpgsqlBinaryImporterType?.GetMethod("DisposeAsync");

    /// <inheritdoc/>
    public bool IsSupported => NpgsqlConnectionType != null && NpgsqlBinaryImporterType != null;

    /// <inheritdoc/>
    public int CopyToServer(IDbConnection connection, string tableName, IDataReader data, BulkCopyOptions options)
    {
        if (NpgsqlConnectionType == null || NpgsqlBinaryImporterType == null)
            throw new InvalidOperationException("NpgsqlBinaryImporter is not available. Ensure Npgsql is installed.");

        // Build COPY command
        var copyCommand = BuildCopyCommand(tableName, data);

        // Begin binary import — must be called on the actual NpgsqlConnection
        var importer = BeginBinaryImportMethod?.Invoke(connection, new object[] { copyCommand });

        if (importer == null)
            throw new InvalidOperationException("Failed to begin binary import.");

        try
        {
            int rowCount = 0;
            int columnCount = data.FieldCount;

            while (data.Read())
            {
                StartRowMethod?.Invoke(importer, null);

                for (int i = 0; i < columnCount; i++)
                {
                    var value = data.GetValue(i);
                    if (value is DBNull)
                    {
                        WriteNullMethod?.Invoke(importer, null);
                    }
                    else
                    {
                        // Use Write<T> with the actual runtime type to avoid boxing/type issues
                        var writeMethod = WriteGenericMethod?.MakeGenericMethod(value.GetType());
                        writeMethod?.Invoke(importer, new[] { value });
                    }
                }

                rowCount++;
            }

            CompleteMethod?.Invoke(importer, null);

            return rowCount;
        }
        finally
        {
            // Always dispose the importer (rolls back on error, cleans up on success)
            DisposeMethod?.Invoke(importer, null);
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
        if (NpgsqlConnectionType == null || NpgsqlBinaryImporterType == null)
            throw new InvalidOperationException("NpgsqlBinaryImporter is not available. Ensure Npgsql is installed.");

        // If native async methods are not available, fall back to sync
        if (BeginBinaryImportAsyncMethod == null || StartRowAsyncMethod == null)
            return CopyToServer(connection, tableName, data, options);

        var copyCommand = BuildCopyCommand(tableName, data);

        // BeginBinaryImportAsync returns Task<NpgsqlBinaryImporter>
        var importerTask = BeginBinaryImportAsyncMethod.Invoke(connection, new object[] { copyCommand, cancellationToken }) as Task;
        if (importerTask == null)
            throw new InvalidOperationException("Failed to begin async binary import.");

        await importerTask.ConfigureAwait(false);

        // Get the result from the completed Task<T>
        var resultProperty = importerTask.GetType().GetProperty("Result");
        var importer = resultProperty?.GetValue(importerTask);

        if (importer == null)
            throw new InvalidOperationException("Failed to get binary importer from async result.");

        try
        {
            int rowCount = 0;
            int columnCount = data.FieldCount;

            while (data.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var startTask = StartRowAsyncMethod.Invoke(importer, new object[] { cancellationToken }) as Task;
                if (startTask != null) await startTask.ConfigureAwait(false);

                for (int i = 0; i < columnCount; i++)
                {
                    var value = data.GetValue(i);
                    if (value is DBNull)
                    {
                        var nullTask = WriteNullAsyncMethod?.Invoke(importer, new object[] { cancellationToken }) as Task;
                        if (nullTask != null) await nullTask.ConfigureAwait(false);
                    }
                    else
                    {
                        // Use Write<T> with the actual runtime type
                        var writeMethod = WriteGenericMethod?.MakeGenericMethod(value.GetType());
                        writeMethod?.Invoke(importer, new[] { value });
                    }
                }

                rowCount++;
            }

            if (CompleteAsyncMethod != null)
            {
                var completeTask = CompleteAsyncMethod.Invoke(importer, new object[] { cancellationToken }) as Task;
                if (completeTask != null) await completeTask.ConfigureAwait(false);
            }
            else
            {
                CompleteMethod?.Invoke(importer, null);
            }

            return rowCount;
        }
        finally
        {
            if (DisposeAsyncMethod != null)
            {
                var disposeResult = DisposeAsyncMethod.Invoke(importer, null);
                if (disposeResult is ValueTask valueTask)
                    await valueTask.ConfigureAwait(false);
            }
            else
            {
                DisposeMethod?.Invoke(importer, null);
            }
        }
    }

    /// <summary>
    /// Builds the COPY command for PostgreSQL.
    /// </summary>
    private static string BuildCopyCommand(string tableName, IDataReader data)
    {
        var columnNames = new List<string>();
        for (int i = 0; i < data.FieldCount; i++)
        {
            columnNames.Add($"\"{data.GetName(i)}\"");
        }

        var columns = string.Join(", ", columnNames);
        return $"COPY {tableName} ({columns}) FROM STDIN BINARY";
    }

    /// <summary>
    /// Finds the generic Write&lt;T&gt;(T value) method on NpgsqlBinaryImporter.
    /// </summary>
    private static MethodInfo? FindWriteGenericMethod()
    {
        if (NpgsqlBinaryImporterType == null) return null;

        // Look for Write<T>(T value) — a generic method with exactly one parameter
        foreach (var method in NpgsqlBinaryImporterType.GetMethods())
        {
            if (method.Name == "Write" && method.IsGenericMethodDefinition)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 1)
                    return method;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds an async method on NpgsqlConnection by name.
    /// </summary>
    private static MethodInfo? FindAsyncMethod(string methodName)
    {
        if (NpgsqlConnectionType == null) return null;

        // Look for the async overload that takes (string, CancellationToken)
        return NpgsqlConnectionType.GetMethod(methodName, new[] { typeof(string), typeof(CancellationToken) });
    }
}