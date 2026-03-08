using System.Data;
using System.Data.Common;
using System.Text;

using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Interfaces;

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
        var entityType = typeof(T);
        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(entityType);
        string tableName = source.TableName;

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
        ValidateTargetSchema(targetConnection, tableName, mappings, options.CreateTableIfMissing);

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
                foreach (var mapping in mappings.Values)
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
            catch (IndexOutOfRangeException)
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

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        // Final progress report
        if (onProgress is not null)
        {
            onProgress(totalImported, totalImported);
        }

        return totalImported;
    }

    // Cache reflected PropertyInfo for DuckDB-specific types to avoid repeated reflection lookups.
    // ConcurrentDictionary handles thread safety; the key is the runtime Type of the DuckDB value.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.PropertyInfo?> s_duckDbDatePropCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.PropertyInfo?> s_duckDbTimePropCache = new();

    /// <summary>
    /// Converts a value from the DuckDB reader to a type suitable for the target database parameter.
    /// </summary>
    private static object ConvertValue(object value, Type targetType)
    {
        if (value is null or DBNull) return DBNull.Value;

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Handle DuckDB-specific types
        var valueType = value.GetType();
        var valueTypeName = valueType.FullName ?? valueType.Name;

        // DuckDB returns DuckDBDateOnly for DATE columns
        if (valueTypeName.Contains("DuckDBDateOnly") && underlyingType == typeof(DateTime))
        {
            var daysProp = s_duckDbDatePropCache.GetOrAdd(valueType, t => t.GetProperty("DaysSinceEpoch"));
            if (daysProp is null)
            {
                throw new InvalidOperationException(
                    $"Cannot convert DuckDB date type '{valueTypeName}': expected a 'DaysSinceEpoch' property " +
                    $"but it was not found. This may indicate an incompatible DuckDB.NET version.");
            }
            var days = (int)daysProp.GetValue(value)!;
            return new DateTime(1970, 1, 1).AddDays(days);
        }

        // DuckDB returns DuckDBTimeOnly for TIME columns
        if (valueTypeName.Contains("DuckDBTimeOnly") && underlyingType == typeof(TimeSpan))
        {
            var ticksProp = s_duckDbTimePropCache.GetOrAdd(valueType, t => t.GetProperty("Ticks"));
            if (ticksProp is null)
            {
                throw new InvalidOperationException(
                    $"Cannot convert DuckDB time type '{valueTypeName}': expected a 'Ticks' property " +
                    $"but it was not found. This may indicate an incompatible DuckDB.NET version.");
            }
            var ticks = (long)ticksProp.GetValue(value)!;
            return new TimeSpan(ticks);
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

    private static void ValidateTargetSchema(DbConnection targetConnection, string tableName, IReadOnlyDictionary<string, ColumnMapping> mappings, bool createTableIfMissing)
    {
        // Check if the table exists in the target by querying it with a WHERE 0=1 (no rows).
        // Different database providers throw different exception types for "table not found":
        //   - SQLite: SqliteException
        //   - PostgreSQL: NpgsqlException / PostgresException
        //   - SQL Server: SqlException
        // We catch DbException (the ADO.NET base class for all provider exceptions) to handle
        // "table not found" errors specifically, while letting non-database errors propagate.
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

            foreach (var mapping in mappings.Values)
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
            // DbException covers all ADO.NET provider-specific exceptions (table not found, etc.)
            if (!createTableIfMissing)
            {
                throw new InvalidOperationException(
                    $"Target table '{tableName}' does not exist and CreateTableIfMissing is false. " +
                    $"Set CreateTableIfMissing = true to auto-create the table, or create it manually before importing.", ex);
            }
            // If createTableIfMissing is true, the table should have been created already.
            // If it still doesn't exist, there was likely a DDL error that will surface on INSERT.
        }
    }
}