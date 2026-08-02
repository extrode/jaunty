using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Jaunty.Configuration;

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

    // NpgsqlBinaryImporter.WriteAsync<T>(T value, CancellationToken) — the async counterpart to
    // WriteGenericMethod above, required for the async copy path (see CopyToServerAsync).
    private static readonly MethodInfo? WriteAsyncGenericMethod = FindWriteAsyncGenericMethod();

    // Per-type Write<T>/WriteAsync<T> MethodInfo cache — MakeGenericMethod is expensive
    // (comparable to a dictionary lookup plus JIT bookkeeping) and was previously called once
    // per non-null cell in every row, which works against the entire point of using the native
    // binary COPY path for bulk-insert performance. Built once per column/type combination.
    private static readonly ConcurrentDictionary<Type, MethodInfo> WriteMethodCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> WriteAsyncMethodCache = new();

    // Async methods available in Npgsql 6+
    private static readonly MethodInfo? BeginBinaryImportAsyncMethod = FindAsyncMethod("BeginBinaryImportAsync");
    private static readonly MethodInfo? StartRowAsyncMethod = NpgsqlBinaryImporterType?.GetMethod("StartRowAsync", new[] { typeof(CancellationToken) });
    private static readonly MethodInfo? WriteNullAsyncMethod = NpgsqlBinaryImporterType?.GetMethod("WriteNullAsync", new[] { typeof(CancellationToken) });
    private static readonly MethodInfo? CompleteAsyncMethod = NpgsqlBinaryImporterType?.GetMethod("CompleteAsync", new[] { typeof(CancellationToken) });
    private static readonly MethodInfo? DisposeAsyncMethod = NpgsqlBinaryImporterType?.GetMethod("DisposeAsync");

    /// <inheritdoc/>
    public bool IsSupported => NpgsqlConnectionType != null && NpgsqlBinaryImporterType != null;

    /// <inheritdoc/>
    public int CopyToServer(IDbConnection connection, string? schemaName, string tableName, IDataReader data, BulkCopyOptions options)
    {
        if (NpgsqlConnectionType == null || NpgsqlBinaryImporterType == null)
            throw new InvalidOperationException("NpgsqlBinaryImporter is not available. Ensure Npgsql is installed.");

        if (!NpgsqlConnectionType.IsInstanceOfType(connection))
            throw new ArgumentException("Connection must be an NpgsqlConnection.", nameof(connection));

        if (StartRowMethod == null || WriteGenericMethod == null || CompleteMethod == null || WriteNullMethod == null)
            throw new InvalidOperationException("NpgsqlBinaryImporter members could not be resolved via reflection.");

        ValidateTransaction(connection, options.Transaction);

        MethodInfo writeGenericMethod = WriteGenericMethod;

        // NpgsqlBinaryImporter has no per-import timeout/batch-size/check-constraints/table-lock
        // controls, and NpgsqlConnection.CommandTimeout has no public setter (it's derived from
        // the connection string), so unlike SqlServerBulkCopyProvider/MySqlBulkCopyProvider there
        // is no reflection-accessible way to honor any of those BulkCopyOptions for Postgres.
        //
        // options.EnableStreaming is likewise not applicable rather than merely unimplemented: the
        // binary COPY protocol has no buffered mode, so this import always streams and false cannot
        // be honored. Documented on BulkCopyOptions.EnableStreaming (AUD-R25).
        //
        // AUD-R26-061: options.IdentityMode, options.CheckConstraints and options.TableLock are
        // not read here either, and the reasons differ from the ones above. CheckConstraints and
        // TableLock are not expressible: this provider has no per-import constraint control or
        // table-lock hint, and approximating the latter with a separate LOCK statement would carry
        // different transactional semantics than the flag implies. IdentityMode is inert on every
        // provider, not just this one - EntityDataReader streams EntityMetadata.InsertColumns,
        // which excludes identity columns, so the identity value never reaches any bulk copy at
        // all. Documented per provider on BulkCopyOptions; pinned by BulkCopyIdentityModeTests.

        // Build COPY command
        var copyCommand = BuildCopyCommand(schemaName, tableName, data);

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
                StartRowMethod.Invoke(importer, null);

                for (int i = 0; i < columnCount; i++)
                {
                    var value = data.GetValue(i);
                    if (value is DBNull)
                    {
                        WriteNullMethod.Invoke(importer, null);
                    }
                    else
                    {
                        // Use Write<T> with the actual runtime type to avoid boxing/type issues
                        MethodInfo writeMethod = WriteMethodCache.GetOrAdd(value.GetType(), t => writeGenericMethod.MakeGenericMethod(t));
                        writeMethod.Invoke(importer, new[] { value });
                    }
                }

                rowCount++;
            }

            CompleteMethod.Invoke(importer, null);

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
        string? schemaName,
        string tableName,
        IDataReader data,
        BulkCopyOptions options,
        CancellationToken cancellationToken)
    {
        if (NpgsqlConnectionType == null || NpgsqlBinaryImporterType == null)
            throw new InvalidOperationException("NpgsqlBinaryImporter is not available. Ensure Npgsql is installed.");

        if (!NpgsqlConnectionType.IsInstanceOfType(connection))
            throw new ArgumentException("Connection must be an NpgsqlConnection.", nameof(connection));

        // If native async methods are not available, fall back to sync
        if (BeginBinaryImportAsyncMethod == null || StartRowAsyncMethod == null || WriteAsyncGenericMethod == null)
            return CopyToServer(connection, schemaName, tableName, data, options);

        MethodInfo writeAsyncGenericMethod = WriteAsyncGenericMethod;

        if (WriteNullAsyncMethod == null)
            throw new InvalidOperationException("NpgsqlBinaryImporter.WriteNullAsync could not be resolved via reflection.");

        if (CompleteAsyncMethod == null && CompleteMethod == null)
            throw new InvalidOperationException("NpgsqlBinaryImporter.Complete/CompleteAsync could not be resolved via reflection.");

        ValidateTransaction(connection, options.Transaction);

        // NpgsqlBinaryImporter has no per-import timeout/batch-size/check-constraints/table-lock
        // controls, and NpgsqlConnection.CommandTimeout has no public setter (it's derived from
        // the connection string), so unlike SqlServerBulkCopyProvider/MySqlBulkCopyProvider there
        // is no reflection-accessible way to honor any of those BulkCopyOptions for Postgres.
        //
        // options.EnableStreaming is likewise not applicable rather than merely unimplemented: the
        // binary COPY protocol has no buffered mode, so this import always streams and false cannot
        // be honored. Documented on BulkCopyOptions.EnableStreaming (AUD-R25).
        //
        // AUD-R26-061: options.IdentityMode, options.CheckConstraints and options.TableLock are
        // not read here either, and the reasons differ from the ones above. CheckConstraints and
        // TableLock are not expressible: this provider has no per-import constraint control or
        // table-lock hint, and approximating the latter with a separate LOCK statement would carry
        // different transactional semantics than the flag implies. IdentityMode is inert on every
        // provider, not just this one - EntityDataReader streams EntityMetadata.InsertColumns,
        // which excludes identity columns, so the identity value never reaches any bulk copy at
        // all. Documented per provider on BulkCopyOptions; pinned by BulkCopyIdentityModeTests.

        var copyCommand = BuildCopyCommand(schemaName, tableName, data);

        // BeginBinaryImportAsync returns Task<NpgsqlBinaryImporter>
        if (BeginBinaryImportAsyncMethod.Invoke(connection, new object[] { copyCommand, cancellationToken }) is not Task importerTask)
            throw new InvalidOperationException("Failed to begin async binary import.");

        await importerTask.ConfigureAwait(false);

        // Get the result from the completed Task<T>

        PropertyInfo? resultProperty = importerTask.GetType().GetProperty("Result");
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

                if (StartRowAsyncMethod.Invoke(importer, new object[] { cancellationToken }) is Task startTask) await startTask.ConfigureAwait(false);

                for (int i = 0; i < columnCount; i++)
                {
                    var value = data.GetValue(i);
                    if (value is DBNull)
                    {
                        if (WriteNullAsyncMethod.Invoke(importer, new object[] { cancellationToken }) is Task nullTask) await nullTask.ConfigureAwait(false);
                    }
                    else
                    {
                        // Use WriteAsync<T> with the actual runtime type. Calling the
                        // synchronous Write<T> here (as opposed to WriteAsync<T>) corrupts
                        // NpgsqlBinaryImporter's internal state machine when mixed with the
                        // async StartRowAsync/WriteNullAsync/CompleteAsync calls around it,
                        // and hangs indefinitely against a real server instead of throwing
                        // (AUD-R11 batch-06: caught via coverage testing - this path had no
                        // test at all before, sync or async, so the deadlock was undetected).
                        MethodInfo writeMethod = WriteAsyncMethodCache.GetOrAdd(value.GetType(), t => writeAsyncGenericMethod.MakeGenericMethod(t));
                        if (writeMethod.Invoke(importer, new[] { value, cancellationToken }) is Task writeTask)
                            await writeTask.ConfigureAwait(false);
                    }
                }

                rowCount++;
            }

            if (CompleteAsyncMethod != null)
            {
                // CompleteAsync returns ValueTask<ulong> (a struct), not Task - awaiting only on
                // an "is Task" match silently dropped this await entirely, invoking Complete but
                // never waiting for it to finish before the importer got disposed below (AUD-R11
                // batch-06: this fire-and-forget completion was the actual cause of the hang
                // caught by coverage testing, on top of the separate sync-Write-in-async-path bug).
                object? completeResult = CompleteAsyncMethod.Invoke(importer, new object[] { cancellationToken });
                if (completeResult is ValueTask<ulong> completeValueTask)
                    await completeValueTask.ConfigureAwait(false);
                else if (completeResult is Task completeTask)
                    await completeTask.ConfigureAwait(false);
            }
            else
            {
                CompleteMethod!.Invoke(importer, null);
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
    /// AUD-R32-008. This provider cannot enlist a transaction explicitly: Npgsql's binary COPY
    /// always runs on whatever transaction is already ambient on the connection, and
    /// <c>BeginBinaryImport</c> takes no transaction argument. That makes silence the dangerous
    /// option - <see cref="IBulkCopyProvider"/> documents that a supplied transaction is enlisted,
    /// and a caller passing one belonging to a different connection would have been ignored
    /// without a word, copying outside the transaction they thought they were in. SqlServer's
    /// provider rejects a mismatched transaction rather than proceeding; this does the same for
    /// the one mismatch that is detectable here.
    /// </summary>
    private static void ValidateTransaction(IDbConnection connection, IDbTransaction? transaction)
    {
        if (transaction is null)
            return;

        if (!ReferenceEquals(transaction.Connection, connection))
            throw new ArgumentException(
                "BulkCopyOptions.Transaction must belong to the connection the copy runs on. " +
                "PostgreSQL's binary COPY joins the connection's ambient transaction and cannot " +
                "enlist a different one, so a mismatch would silently copy outside it.",
                nameof(transaction));
    }

    /// <summary>
    /// Builds the COPY command for PostgreSQL.
    /// </summary>
    private static string BuildCopyCommand(string? schemaName, string tableName, IDataReader data)
    {
        global::Jaunty.Dialects.SqlIdentifierValidator.Validate(tableName, nameof(tableName));

        string qualifiedTableName;
        if (schemaName is null || schemaName.Length == 0)
        {
            qualifiedTableName = $"\"{tableName}\"";
        }
        else
        {
            global::Jaunty.Dialects.SqlIdentifierValidator.Validate(schemaName, nameof(schemaName));
            qualifiedTableName = $"\"{schemaName}\".\"{tableName}\"";
        }

        var columnNames = new List<string>();
        for (int i = 0; i < data.FieldCount; i++)
        {
            string columnName = data.GetName(i);
            global::Jaunty.Dialects.SqlIdentifierValidator.Validate(columnName, nameof(data));
            columnNames.Add($"\"{columnName}\"");
        }

        var columns = string.Join(", ", columnNames);
        return $"COPY {qualifiedTableName} ({columns}) FROM STDIN BINARY";
    }

    /// <summary>
    /// Finds the generic Write&lt;T&gt;(T value) method on NpgsqlBinaryImporter.
    /// </summary>
    private static MethodInfo? FindWriteGenericMethod()
    {
        if (NpgsqlBinaryImporterType == null) return null;

        // Look for Write<T>(T value) — a generic method with exactly one parameter

        foreach (MethodInfo? method in NpgsqlBinaryImporterType.GetMethods())
        {
            if (method.Name == "Write" && method.IsGenericMethodDefinition)
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1)
                    return method;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the generic WriteAsync&lt;T&gt;(T value, CancellationToken) method on NpgsqlBinaryImporter.
    /// </summary>
    private static MethodInfo? FindWriteAsyncGenericMethod()
    {
        if (NpgsqlBinaryImporterType == null) return null;

        // Look for WriteAsync<T>(T value, CancellationToken cancellationToken) specifically —
        // NpgsqlBinaryImporter also has WriteAsync<T>(T, NpgsqlDbType, CancellationToken) and
        // WriteAsync<T>(T, string, CancellationToken) overloads with 3 parameters, so the
        // 2-parameter check below is what disambiguates the plain overload from those.

        foreach (MethodInfo? method in NpgsqlBinaryImporterType.GetMethods())
        {
            if (method.Name == "WriteAsync" && method.IsGenericMethodDefinition)
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[1].ParameterType == typeof(CancellationToken))
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