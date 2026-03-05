using System.Data;
using System.Data.Common;
using System.Reflection;

namespace Jaunty.FlatFiles.DuckDB.ImportPipeline;

/// <summary>
/// Executes the import pipeline: reads from a DuckDB source and writes to a target database
/// using batched INSERT statements within a transaction.
/// </summary>
internal static class ImportExecutor
{
    /// <summary>
    /// Imports data from a DuckDB source table into a target database connection.
    /// </summary>
    public static async ValueTask<long> ExecuteAsync<T>(
        IDbConnection sourceConnection,
        IFileSource source,
        DbConnection targetConnection,
        ImportOptions options,
        CancellationToken cancellationToken) where T : class, new()
    {
        var entityType = typeof(T);
        var mappings = FlatFileExpressionHelper.GetColumnMappings(entityType);
        var tableName = source.TableName;

        // Resolve the import dialect (explicit > custom registry > auto-detect)
        var dialect = ImportDialectResolver.Resolve(targetConnection, options.Dialect);

        // Ensure target connection is open
        if (targetConnection.State != ConnectionState.Open)
            await targetConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Create table if requested
        if (options.CreateTableIfMissing)
        {
            var ddl = TargetDdlGenerator.GenerateCreateTableSql(entityType, tableName, dialect);
            await using var ddlCmd = targetConnection.CreateCommand();
            ddlCmd.CommandText = ddl;
            await ddlCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Validate schema alignment — check that the target table exists and has compatible columns
        ValidateTargetSchema(targetConnection, tableName, mappings);

        // Read all rows from DuckDB source
        var sourceCmd = (sourceConnection as DbConnection)!.CreateCommand();
        await using (sourceCmd.ConfigureAwait(false))
        {
            sourceCmd.CommandText = $"SELECT * FROM \"{tableName}\"";
            var reader = await sourceCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                // Build the insert SQL template using the dialect
                // Avoid LINQ allocations by using pre-sized lists
                var columnNames = new List<string>(mappings.Count);
                foreach (var mapping in mappings)
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

    private static async ValueTask<long> ImportBatchesAsync(
        DbDataReader reader,
        DbConnection targetConnection,
        string insertSql,
        List<(string ColumnName, PropertyInfo Property)> mappings,
        int batchSize,
        Action<long, long?>? onProgress,
        CancellationToken cancellationToken)
    {
        long totalImported = 0;

        // Build a column index map for the reader (source column name → reader ordinal)
        var readerColumnMap = new int[mappings.Count];
        for (int i = 0; i < mappings.Count; i++)
        {
            try
            {
                readerColumnMap[i] = reader.GetOrdinal(mappings[i].ColumnName);
            }
            catch (IndexOutOfRangeException)
            {
                throw new InvalidOperationException(
                    $"Schema alignment failed: Source does not contain column '{mappings[i].ColumnName}' " +
                    $"required by entity mapping. Available columns: " +
                    string.Join(", ", Enumerable.Range(0, reader.FieldCount).Select(reader.GetName)));
            }
        }

        var transaction = await targetConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var cmd = targetConnection.CreateCommand();
            await using (cmd.ConfigureAwait(false))
            {
                cmd.CommandText = insertSql;
                cmd.Transaction = transaction;

                // Pre-create parameters
                var paramArray = new DbParameter[mappings.Count];
                for (int i = 0; i < mappings.Count; i++)
                {
                    var param = cmd.CreateParameter();
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
                    for (int i = 0; i < mappings.Count; i++)
                    {
                        var value = reader.GetValue(readerColumnMap[i]);
                        paramArray[i].Value = value is DBNull ? DBNull.Value : ConvertValue(value, mappings[i].Property.PropertyType);
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
    /// Converts a value from the DuckDB reader to a type suitable for the target database parameter.
    /// </summary>
    private static object ConvertValue(object value, Type targetType)
    {
        if (value is null || value is DBNull) return DBNull.Value;

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Handle DuckDB-specific types
        var valueType = value.GetType();
        var valueTypeName = valueType.FullName ?? valueType.Name;

        // DuckDB returns DuckDBDateOnly for DATE columns
        if (valueTypeName.Contains("DuckDBDateOnly") && underlyingType == typeof(DateTime))
        {
            var daysProp = valueType.GetProperty("DaysSinceEpoch");
            if (daysProp is not null)
            {
                var days = (int)daysProp.GetValue(value)!;
                return new DateTime(1970, 1, 1).AddDays(days);
            }
        }

        // DuckDB returns DuckDBTimeOnly for TIME columns
        if (valueTypeName.Contains("DuckDBTimeOnly") && underlyingType == typeof(TimeSpan))
        {
            var ticksProp = valueType.GetProperty("Ticks");
            if (ticksProp is not null)
            {
                var ticks = (long)ticksProp.GetValue(value)!;
                return new TimeSpan(ticks);
            }
        }

        // Standard conversions
        if (value.GetType() == underlyingType) return value;

        try
        {
            return Convert.ChangeType(value, underlyingType);
        }
        catch
        {
            // Return as-is and let the ADO.NET provider handle it
            return value;
        }
    }

    private static void ValidateTargetSchema(
        DbConnection targetConnection,
        string tableName,
        List<(string ColumnName, PropertyInfo Property)> mappings)
    {
        // Check if the table exists in the target
        try
        {
            using var cmd = targetConnection.CreateCommand();
            cmd.CommandText = $"SELECT * FROM \"{tableName}\" WHERE 0=1";
            using var reader = cmd.ExecuteReader();

            // Table exists — validate columns
            var targetColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                targetColumns.Add(reader.GetName(i));
            }

            foreach (var (columnName, _) in mappings)
            {
                if (!targetColumns.Contains(columnName))
                {
                    throw new InvalidOperationException(
                        $"Schema alignment failed: Target table '{tableName}' does not contain column '{columnName}' " +
                        $"required by entity mapping. Available columns: {string.Join(", ", targetColumns)}");
                }
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // Table doesn't exist — if CreateTableIfMissing was true, it should have been created already.
            // If not, the INSERT will fail with a clear error anyway.
        }
    }
}
