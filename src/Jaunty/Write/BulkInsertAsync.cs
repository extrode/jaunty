using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously inserts multiple entities into the database in a single transaction.
    /// </summary>
    /// <typeparam name="T">The entity type to insert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk insert against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entities">The collection of entities to insert.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows inserted.</returns>
    /// <remarks>
    /// <para>
    /// This method performs a bulk insert operation within a transaction. All entities are inserted 
    /// atomically - if any insert fails, the entire operation is rolled back.
    /// </para>
    /// <para>
    /// <strong>Foreign key constraints are enforced.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Product
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    ///     public decimal Price { get; set; }
    /// }
    /// 
    /// // Async bulk insert multiple products
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Name = "Widget", Price = 19.99m },
    ///     new Product { Name = "Gadget", Price = 29.99m },
    ///     new Product { Name = "Gizmo", Price = 39.99m }
    /// };
    /// 
    /// int inserted = await connection.BulkInsertAsync(products);
    /// Console.WriteLine($"Inserted {inserted} products");
    /// </code>
    /// </example>
    /// <seealso cref="BulkInsertAsync{T}(IDbConnection, IEnumerable{T}, CommandOptions, CancellationToken)"/>
    /// <seealso cref="BulkInsert{T}(IDbConnection, IEnumerable{T})"/>
    public static ValueTask<int> BulkInsertAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : BulkInsertAsync(dbConnection, entities, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously inserts multiple entities into the database in a single transaction with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to insert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk insert against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entities">The collection of entities to insert.</param>
    /// <param name="options">
    /// Command options for configuring the bulk insert execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows inserted.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Foreign key constraints are enforced.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async bulk insert with transaction
    /// using var tx = connection.BeginTransaction();
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Name = "Widget", Price = 19.99m },
    ///     new Product { Name = "Gadget", Price = 29.99m }
    /// };
    /// 
    /// int inserted = await connection.BulkInsertAsync(
    ///     products, 
    ///     CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="BulkInsertAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/>
    public static ValueTask<int> BulkInsertAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : BulkInsertCoreAsync(dbConnection, entities, options, ignoreConstraints: false, cancellationToken);
    }

    /// <summary>
    /// Asynchronously inserts multiple entities into the database, bypassing foreign key constraint checks.
    /// </summary>
    /// <typeparam name="T">The entity type to insert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk insert against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entities">The collection of entities to insert.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows inserted.</returns>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This method temporarily disables referential integrity. Use only for migrations,
    /// data imports, or scenarios where you explicitly don't need FK validation.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async bulk insert without FK checks
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1, Name = "Widget", CategoryId = 999 },
    ///     new Product { Id = 2, Name = "Gadget", CategoryId = 999 }
    /// };
    /// 
    /// int inserted = await connection.BulkInsertIgnoreConstraintsAsync(products);
    /// </code>
    /// </example>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support foreign key toggling (e.g., SQL Server).
    /// </exception>
    /// <seealso cref="BulkInsertIgnoreConstraintsAsync{T}(IDbConnection, IEnumerable{T}, CommandOptions, CancellationToken)"/>
    /// <seealso cref="BulkInsertAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/>
    public static ValueTask<int> BulkInsertIgnoreConstraintsAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : BulkInsertIgnoreConstraintsAsync(dbConnection, entities, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously inserts multiple entities into the database, bypassing foreign key constraint checks, with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to insert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk insert against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entities">The collection of entities to insert.</param>
    /// <param name="options">
    /// Command options for configuring the bulk insert execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows inserted.</returns>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This method temporarily disables referential integrity. Use with caution.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async bulk insert without FK checks with transaction
    /// using var tx = connection.BeginTransaction();
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1, Name = "Widget", CategoryId = 999 },
    ///     new Product { Id = 2, Name = "Gadget", CategoryId = 999 }
    /// };
    /// 
    /// int inserted = await connection.BulkInsertIgnoreConstraintsAsync(
    ///     products, 
    ///     CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support foreign key toggling.
    /// </exception>
    /// <seealso cref="BulkInsertIgnoreConstraintsAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<int> BulkInsertIgnoreConstraintsAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : BulkInsertCoreAsync(dbConnection, entities, options, ignoreConstraints: true, cancellationToken);
    }

    private static async ValueTask<int> BulkInsertCoreAsync<T>(DbConnection connection, IEnumerable<T> entities, CommandOptions options, bool ignoreConstraints, CancellationToken cancellationToken) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif

        var entityList = entities as IList<T> ?? entities.ToList();
        if (entityList.Count == 0)
            return 0;

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.InsertSql))
            throw new InvalidOperationException($"Cannot insert entity of type '{typeof(T).Name}': No insertable columns found.");

        ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);

        if (ignoreConstraints && !dialect.SupportsForeignKeyToggle)
            throw new NotSupportedException(
                $"The database provider ({connection.GetType().Name}) does not support session-level foreign key toggling. " +
                "Use BulkInsertAsync instead, or disable constraints manually before calling this method.");

        bool wasClosed = connection.State == ConnectionState.Closed;
        DbTransaction? transaction = options.Transaction as DbTransaction;
        bool ownTransaction = transaction is null;

        try
        {
            if (wasClosed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (ownTransaction)
            {
#if NET8_0_OR_GREATER
                transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
#else
                transaction = connection.BeginTransaction();
#endif
            }

            if (ignoreConstraints)
            {
#if NET8_0_OR_GREATER
                await using var fkOffCmd = connection.CreateCommand();
#else
                using var fkOffCmd = connection.CreateCommand();
#endif
                fkOffCmd.Transaction = transaction;
                fkOffCmd.CommandText = dialect.GetDisableForeignKeyChecksSql()!;
                await fkOffCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            int totalInserted = 0;

            try
            {
                // PostgreSQL Fast Path: COPY (Binary Import)
                if (connection.GetType().Name == "NpgsqlConnection" && !ignoreConstraints)
                {
                    return await ExecutePostgreSqlBinaryImportAsync(connection, entityList, cached, options, cancellationToken);
                }

                // SQL Server Fast Path: SqlBulkCopy
                if (connection.GetType().Name == "SqlConnection" && !ignoreConstraints)
                {
                    return await ExecuteSqlServerBulkInsertAsync(connection, entityList, cached, options, cancellationToken);
                }

#if NET8_0_OR_GREATER
                await using var command = connection.CreateCommand();
#else
                using var command = connection.CreateCommand();
#endif
                command.Transaction = transaction;
                command.CommandText = cached.InsertSql;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                PrepareInsertParameters(command, cached.Metadata);

                var valueSetter = WriteParameterCache<T>.InsertValueSetter;
                var pCollection = command.Parameters;

                foreach (var entity in entityList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    valueSetter(pCollection, entity);
                    totalInserted += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                if (ignoreConstraints)
                {
#if NET8_0_OR_GREATER
                    await using var fkOnCmd = connection.CreateCommand();
#else
                    using var fkOnCmd = connection.CreateCommand();
#endif
                    fkOnCmd.Transaction = transaction;
                    fkOnCmd.CommandText = dialect.GetEnableForeignKeyChecksSql()!;
                    await fkOnCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                if (ownTransaction)
                {
#if NET8_0_OR_GREATER
                    await transaction!.CommitAsync(cancellationToken).ConfigureAwait(false);
#else
                    transaction!.Commit();
#endif
                }

                return totalInserted;
            }
            catch
            {
                if (ignoreConstraints)
                {
                    try
                    {
#if NET8_0_OR_GREATER
                        await using var fkOnCmd = connection.CreateCommand();
#else
                        using var fkOnCmd = connection.CreateCommand();
#endif
                        fkOnCmd.Transaction = transaction;
                        fkOnCmd.CommandText = dialect.GetEnableForeignKeyChecksSql()!;
                        await fkOnCmd.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch { /* Best effort */ }
                }

                if (ownTransaction)
                {
                    try
                    {
#if NET8_0_OR_GREATER
                        await transaction!.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
#else
                        transaction?.Rollback();
#endif
                    }
                    catch { /* Best effort */ }
                }

                throw;
            }
        }
        finally
        {
            if (ownTransaction)
            {
#if NET8_0_OR_GREATER
                if (transaction is not null)
                    await transaction.DisposeAsync().ConfigureAwait(false);
#else
                transaction?.Dispose();
#endif
            }

            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
            }
        }
    }

    private static async ValueTask<int> ExecutePostgreSqlBinaryImportAsync<T>(DbConnection connection, IList<T> entities, CachedCrudSql cached, CommandOptions options, CancellationToken cancellationToken) where T : class, new()
    {
        var metadata = cached.Metadata;
        var columns = metadata.NonIdentityColumns;
        var columnNames = string.Join(", ", columns.Select(c => $"\"{c.ColumnName}\""));
        var tableName = string.IsNullOrEmpty(metadata.SchemaName) 
            ? $"\"{metadata.TableName}\"" 
            : $"\"{metadata.SchemaName}\".\"{metadata.TableName}\"";
        var copySql = $"COPY {tableName} ({columnNames}) FROM STDIN (FORMAT BINARY)";

        // Reflection to call Npgsql methods without direct dependency
        var beginBinaryImportMethod = connection.GetType().GetMethod("BeginBinaryImport", [typeof(string)]);
        if (beginBinaryImportMethod == null) return -1; // Fallback to standard loop if method not found

        var writer = beginBinaryImportMethod.Invoke(connection, [copySql]);
        if (writer == null) return -1;

        var writerType = writer.GetType();
        var writeAsyncMethod = writerType.GetMethod("WriteAsync");
        var startRowAsyncMethod = writerType.GetMethod("StartRowAsync");
        var completeAsyncMethod = writerType.GetMethod("CompleteAsync");
        var disposeAsyncMethod = writerType.GetMethod("DisposeAsync");

        if (writeAsyncMethod == null || startRowAsyncMethod == null || completeAsyncMethod == null) return -1;

        var properties = MetadataCache<T>.Properties;
        var writeColumns = new List<PropertyContext<T>>();
        foreach (var col in columns)
        {
            var prop = properties.FirstOrDefault(p => p.Property == col.Property);
            writeColumns.Add(prop);
        }

        try
        {
            foreach (var entity in entities)
            {
                await (Task)startRowAsyncMethod.Invoke(writer, [cancellationToken])!;
                foreach (var col in writeColumns)
                {
                    var value = col.Getter(entity);
                    await (Task)writeAsyncMethod.MakeGenericMethod(value?.GetType() ?? typeof(object))
                        .Invoke(writer, [value, cancellationToken])!;
                }
            }

            return (int)await (Task<ulong>)completeAsyncMethod.Invoke(writer, [cancellationToken])!;
        }
        finally
        {
            if (disposeAsyncMethod != null)
                await (ValueTask)disposeAsyncMethod.Invoke(writer, [])!;
        }
    }

    private static async ValueTask<int> ExecuteSqlServerBulkInsertAsync<T>(DbConnection connection, IList<T> entities, CachedCrudSql cached, CommandOptions options, CancellationToken cancellationToken) where T : class, new()
    {
        var metadata = cached.Metadata;
        var columns = metadata.NonIdentityColumns;
        var tableName = string.IsNullOrEmpty(metadata.SchemaName) 
            ? $"[{metadata.TableName}]" 
            : $"[{metadata.SchemaName}].[{metadata.TableName}]";

        // Create DataTable for SqlBulkCopy
        var dt = new DataTable();
        var properties = MetadataCache<T>.Properties;
        var writeColumns = new List<PropertyContext<T>>();
        
        foreach (var col in columns)
        {
            var prop = properties.FirstOrDefault(p => p.Property == col.Property);
            writeColumns.Add(prop);
            dt.Columns.Add(col.ColumnName, Nullable.GetUnderlyingType(prop.Property.PropertyType) ?? prop.Property.PropertyType);
        }

        foreach (var entity in entities)
        {
            var row = dt.NewRow();
            for (int i = 0; i < writeColumns.Count; i++)
            {
                row[i] = writeColumns[i].Getter(entity) ?? DBNull.Value;
            }
            dt.Rows.Add(row);
        }

        // Reflection to call SqlBulkCopy methods
        var assembly = connection.GetType().Assembly;
        var bulkCopyType = assembly.GetType("Microsoft.Data.SqlClient.SqlBulkCopy") 
                          ?? assembly.GetType("System.Data.SqlClient.SqlBulkCopy");
        
        if (bulkCopyType == null) return -1;

        var optionsType = assembly.GetType("Microsoft.Data.SqlClient.SqlBulkCopyOptions")
                         ?? assembly.GetType("System.Data.SqlClient.SqlBulkCopyOptions");
        
        // Default options: KeepIdentity | CheckConstraints
        object bulkOptions = optionsType != null ? Enum.ToObject(optionsType, 0) : 0;

        var bulkCopy = Activator.CreateInstance(bulkCopyType, connection, bulkOptions, options.Transaction)!;
        try
        {
            bulkCopyType.GetProperty("DestinationTableName")!.SetValue(bulkCopy, tableName);
            if (options.CommandTimeout.HasValue)
                bulkCopyType.GetProperty("BulkCopyTimeout")!.SetValue(bulkCopy, options.CommandTimeout.Value);

            var writeToServerAsyncMethod = bulkCopyType.GetMethod("WriteToServerAsync", [typeof(DataTable), typeof(CancellationToken)]);
            if (writeToServerAsyncMethod == null) return -1;

            await (Task)writeToServerAsyncMethod.Invoke(bulkCopy, [dt, cancellationToken])!;
            
            return entities.Count;
        }
        finally
        {
            if (bulkCopy is IDisposable disposable) disposable.Dispose();
        }
    }
}

