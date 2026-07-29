using System.Data;
using System.Data.Common;
using System.Text;

using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Interfaces;
using System.Globalization;

namespace Jaunty.FlatFiles.DuckDB.Internals.Import;

/// <summary>
/// Executes the import pipeline: reads from a DuckDB source and writes to a target database
/// using batched INSERT statements within a transaction.
/// </summary>
internal static class ImportExecutor
{
    /// <summary>
    /// Imports data from a DuckDB source table into a target database connection.
    /// </summary>
    public static async ValueTask<long> ExecuteAsync<T>(IDbConnection sourceConnection, IFileSource source, DbConnection targetConnection, ImportOptions options, CancellationToken cancellationToken) where T : class, new()
    {
        Type entityType = typeof(T);
        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(entityType);
        string tableName = source.TableName;

        // Resolve the import dialect (explicit > custom registry > auto-detect)
        IImportDialect dialect = ImportDialectResolver.Resolve(targetConnection, options.Dialect);

        // Ensure target connection is open
        if (targetConnection.State != ConnectionState.Open)
            await targetConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Create table if requested
        if (options.CreateTableIfMissing)
        {
            var ddl = TargetDdlGenerator.GenerateCreateTableSql(entityType, tableName, dialect);
            DbCommand ddlCmd = targetConnection.CreateCommand();
            await using var ddlCmdDisposer = ddlCmd.ConfigureAwait(false);
            ddlCmd.CommandText = ddl;
            await ddlCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Validate schema alignment — check that the target table exists and has compatible columns
        await ValidateTargetSchemaAsync(targetConnection, dialect, tableName, mappings, options.CreateTableIfMissing, cancellationToken).ConfigureAwait(false);

        // Read all rows from DuckDB source
        DbCommand sourceCmd = (sourceConnection as DbConnection)!.CreateCommand();
        await using (sourceCmd.ConfigureAwait(false))
        {
            sourceCmd.CommandText = $"SELECT * FROM \"{tableName.Replace("\"", "\"\"")}\"";
            DbDataReader reader = await sourceCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                // Build the insert SQL template using the dialect
                // Avoid LINQ allocations by using pre-sized lists
                var columnNames = new List<string>(mappings.Count);
                foreach (ColumnMapping mapping in mappings.Values)
                    columnNames.Add(mapping.ColumnName);

                var parameterNames = new List<string>(mappings.Count);
                for (int i = 0; i < mappings.Count; i++)
                    parameterNames.Add($"@p{i}");

                var keyColumnName = TargetDdlGenerator.GetKeyColumnName(entityType);
                var insertSql = dialect.GenerateInsertSql(tableName, columnNames, parameterNames, options.OnConflict, keyColumnName);

                // Import in batches within a transaction
                return await ImportBatchesAsync(
                    reader, targetConnection, insertSql, mappings,
                    options.BatchSize, options.OnProgress, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask<long> ImportBatchesAsync(DbDataReader reader, DbConnection targetConnection, string insertSql,
        IReadOnlyDictionary<string, ColumnMapping> mappings, int batchSize, Action<long, long?>? onProgress, CancellationToken cancellationToken)
    {
        long totalImported = 0;

        // Build a column index map for the reader (source column name → reader ordinal)
        var readerColumnMap = new int[mappings.Count];
        var mappingList = mappings.Values.ToList();
        for (int i = 0; i < mappingList.Count; i++)
        {
            try
            {
                readerColumnMap[i] = reader.GetOrdinal(mappingList[i].ColumnName);
            }
            // AUD-R26: this used to catch IndexOutOfRangeException only - which is what ADO.NET's
            // contract specifies, but not what the one provider this reader ever comes from throws.
            // DuckDB.NET raises DuckDBException ("Column with name x was not found."), which derives
            // from DbException, so the catch never fired and the message below was dead code.
            // Both are caught now: the ADO.NET-contract type for any other reader, and DbException
            // for DuckDB's.
            catch (Exception ex) when (ex is IndexOutOfRangeException or DbException)
            {
                // Build available columns list without LINQ allocation
                var availableColumns = new StringBuilder();
                for (int j = 0; j < reader.FieldCount; j++)
                {
                    if (j > 0) availableColumns.Append(", ");
                    availableColumns.Append(reader.GetName(j));
                }

                throw new InvalidOperationException(
                    $"Schema alignment failed: Source does not contain column '{mappingList[i].ColumnName}' " +
                    $"required by entity mapping. Available columns: {availableColumns}");
            }
        }

        DbTransaction transaction = await targetConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // Prefer ADO.NET command batching (DbBatch) when the target provider supports it, so a
            // batch of rows is sent as a single round-trip instead of one ExecuteNonQueryAsync per
            // row. Providers that don't support DbBatch fall back to the single-command path below.
            totalImported = targetConnection.CanCreateBatch
                ? await ImportUsingDbBatchAsync(reader, targetConnection, transaction, insertSql, mappingList, readerColumnMap, batchSize, onProgress, cancellationToken).ConfigureAwait(false)
                : await ImportUsingSingleCommandAsync(reader, targetConnection, transaction, insertSql, mappingList, readerColumnMap, batchSize, onProgress, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        // Final progress report
        if (onProgress is not null)
        {
            onProgress(totalImported, totalImported);
        }

        return totalImported;
    }

    /// <summary>
    /// Imports rows one <see cref="DbCommand"/> execution per row, using a single prepared,
    /// reused command. Used when the target provider does not support <see cref="DbBatch"/>.
    /// </summary>
    private static async ValueTask<long> ImportUsingSingleCommandAsync(
        DbDataReader reader, DbConnection targetConnection, DbTransaction transaction, string insertSql,
        List<ColumnMapping> mappingList, int[] readerColumnMap, int batchSize, Action<long, long?>? onProgress,
        CancellationToken cancellationToken)
    {
        long totalImported = 0;

        DbCommand cmd = targetConnection.CreateCommand();
        await using (cmd.ConfigureAwait(false))
        {
            cmd.CommandText = insertSql;
            cmd.Transaction = transaction;

            // Pre-create parameters
            var paramArray = new DbParameter[mappingList.Count];
            for (int i = 0; i < mappingList.Count; i++)
            {
                DbParameter param = cmd.CreateParameter();
                param.ParameterName = $"@p{i}";
                cmd.Parameters.Add(param);
                paramArray[i] = param;
            }

            // Prepare the command for better performance
            cmd.Prepare();

            int batchCount = 0;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Set parameter values from reader
                for (int i = 0; i < mappingList.Count; i++)
                {
                    var value = reader.GetValue(readerColumnMap[i]);
                    paramArray[i].Value = value is DBNull ? DBNull.Value : ConvertValue(value, mappingList[i].PropertyType);
                }

                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                totalImported++;
                batchCount++;

                // Report progress per batch
                if (batchCount >= batchSize)
                {
                    onProgress?.Invoke(totalImported, null);
                    batchCount = 0;
                }
            }
        }

        return totalImported;
    }

    /// <summary>
    /// Imports rows using <see cref="DbBatch"/>: up to <paramref name="batchSize"/> single-row
    /// INSERT commands are grouped into one <see cref="DbBatch"/> and executed as a single
    /// round-trip, instead of one <c>ExecuteNonQueryAsync</c> call per row.
    /// </summary>
    private static async ValueTask<long> ImportUsingDbBatchAsync(
        DbDataReader reader, DbConnection targetConnection, DbTransaction transaction, string insertSql,
        List<ColumnMapping> mappingList, int[] readerColumnMap, int batchSize, Action<long, long?>? onProgress,
        CancellationToken cancellationToken)
    {
        long totalImported = 0;
        int rowsBuffered = 0;

        // DbBatchCommand does not expose its own parameter factory, so a throwaway DbCommand on
        // the same connection is used purely to manufacture provider-correct DbParameter instances
        // (it is never executed).
        DbCommand parameterFactory = targetConnection.CreateCommand();
        await using var parameterFactoryDisposer = parameterFactory.ConfigureAwait(false);

        DbBatch batch = targetConnection.CreateBatch();
        await using (batch.ConfigureAwait(false))
        {
            batch.Transaction = transaction;

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                DbBatchCommand batchCommand = batch.CreateBatchCommand();
                batchCommand.CommandText = insertSql;

                for (int i = 0; i < mappingList.Count; i++)
                {
                    var value = reader.GetValue(readerColumnMap[i]);
                    DbParameter param = parameterFactory.CreateParameter();
                    param.ParameterName = $"@p{i}";
                    param.Value = value is DBNull ? DBNull.Value : ConvertValue(value, mappingList[i].PropertyType);
                    batchCommand.Parameters.Add(param);
                }

                batch.BatchCommands.Add(batchCommand);
                rowsBuffered++;

                if (rowsBuffered >= batchSize)
                {
                    await batch.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    totalImported += rowsBuffered;
                    onProgress?.Invoke(totalImported, null);

                    batch.BatchCommands.Clear();
                    rowsBuffered = 0;
                }
            }

            if (rowsBuffered > 0)
            {
                await batch.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                totalImported += rowsBuffered;
                onProgress?.Invoke(totalImported, null);
            }
        }

        return totalImported;
    }

    /// <summary>
    /// Converts a value from the DuckDB reader to a type suitable for the target database parameter.
    /// </summary>
    private static object ConvertValue(object value, Type targetType)
    {
        if (value is null or DBNull) return DBNull.Value;

        // AUD-R26: the DuckDB.NET DateOnly/TimeOnly special cases and the InvariantCulture
        // Convert.ChangeType that used to live here now live in ReaderValueConverter, shared with
        // DuckDbRead/DuckDbReadAsync - which had never had them, so a TimeSpan property could be
        // imported but not read back.
        //
        // convertEnums: false, and the fallback deliberately differs from the read path's. The
        // destination here is an ADO.NET parameter, not a typed CLR property, so the provider may
        // accept a representation this converter does not recognise - and for enums specifically the
        // raw value IS the right thing to bind. Boxing the enum breaks PostgreSQL (Npgsql rejects an
        // unmapped enum CLR type) and silently rewrites data on SQLite and SQL Server, which infer
        // the parameter type from Type.GetTypeCode and see an enum as its underlying integer: a text
        // column that received "Closed" would start receiving 1.
        return ReaderValueConverter.TryConvert(value, targetType, convertEnums: false, out object? converted)
            ? converted!
            : value;
    }

    /// <summary>
    /// Quotes an identifier destined for the *target* database. Every other identifier-emitting
    /// path in the import pipeline (GenerateInsertSql/GenerateCreateTableSql/GenerateMergeSql)
    /// goes through the resolved dialect; this one used to hardcode double quotes, which
    /// contradicts SqlServerImportDialect's own rationale for using [brackets] - double quotes
    /// only work when QUOTED_IDENTIFIER is ON. Dialects that don't opt into
    /// <see cref="IQuotedIdentifierDialect"/> keep the previous SQL-standard behaviour.
    /// </summary>
    private static string QuoteTargetIdentifier(IImportDialect dialect, string identifier) =>
        dialect is IQuotedIdentifierDialect quoting
            ? quoting.QuoteIdentifier(identifier)
            : $"\"{identifier.Replace("\"", "\"\"")}\"";

    private static async ValueTask ValidateTargetSchemaAsync(DbConnection targetConnection, IImportDialect dialect, string tableName, IReadOnlyDictionary<string, ColumnMapping> mappings, bool createTableIfMissing, CancellationToken cancellationToken)
    {
        // Check if the table exists in the target by querying it with a WHERE 0=1 (no rows).
        // Different database providers throw different exception types for "table not found":
        //   - SQLite: SqliteException
        //   - PostgreSQL: NpgsqlException / PostgresException
        //   - SQL Server: SqlException
        // DbException is the ADO.NET base class they all derive from, and there is no portable code
        // that means "table not found" - SqliteErrorCode 1, SQLSTATE 42P01 and error 208 are three
        // unrelated encodings - so the catch below stays broad by necessity.
        //
        // AUD-R26-066: what changed is that it no longer *claims* to know why it failed. A
        // permission denial, a dropped connection, a login failure or a syntax error in the quoted
        // name all land in the same handler, and the old code reported every one of them as
        // "Target table 'x' does not exist and CreateTableIfMissing is false" - untrue for all four -
        // or, when createTableIfMissing was true, discarded the exception entirely and let the
        // import fail later somewhere less informative.
        try
        {
            DbCommand cmd = targetConnection.CreateCommand();
            await using var cmdDisposer = cmd.ConfigureAwait(false);
            cmd.CommandText = $"SELECT * FROM {QuoteTargetIdentifier(dialect, tableName)} WHERE 0=1";
            DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);

            // Table exists — validate columns
            var targetColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                targetColumns.Add(reader.GetName(i));
            }

            foreach (ColumnMapping mapping in mappings.Values)
            {
                if (!targetColumns.Contains(mapping.ColumnName))
                {
                    throw new InvalidOperationException(
                        $"Schema alignment failed: Target table '{tableName}' does not contain column '{mapping.ColumnName}' " +
                        $"required by entity mapping. Available columns: {string.Join(", ", targetColumns)}");
                }
            }
        }
        catch (DbException ex)
        {
            if (!createTableIfMissing)
            {
                throw new InvalidOperationException(
                    $"Could not read the schema of target table '{tableName}': {ex.Message} " +
                    "The most likely cause is that the table does not exist and CreateTableIfMissing " +
                    "is false - set CreateTableIfMissing = true to auto-create it, or create it " +
                    "manually before importing. If the table does exist, see the inner exception: " +
                    "a permission denial, a broken connection or an unquotable table name reaches " +
                    "this handler the same way.", ex);
            }

            // createTableIfMissing is true, so the CREATE TABLE ran a few lines above this call and
            // the probe should have succeeded. That it did not is information, and it used to be
            // thrown away - the catch body was a comment and nothing else, and the import went on to
            // fail during INSERT with whatever the provider said there instead. Surface the real
            // cause at the point it was detected.
            throw new InvalidOperationException(
                $"Target table '{tableName}' could not be read back after CreateTableIfMissing " +
                $"created it: {ex.Message} See the inner exception - the CREATE TABLE may have " +
                "failed, or the table may exist but be unreadable by this connection.", ex);
        }
    }
}
