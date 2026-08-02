using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Parameters;
using Jaunty.Internals.Write;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Performs an upsert (INSERT or UPDATE) operation on an entity.
    /// </summary>
    /// <typeparam name="T">The entity type to upsert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the upsert against.</param>
    /// <param name="entity">
    /// The entity instance to upsert. The primary key property determines whether to insert or update:
    /// <list type="bullet">
    /// <item><description>If a row with the primary key exists, it is <strong>updated</strong></description></item>
    /// <item><description>If no row with the primary key exists, a new row is <strong>inserted</strong></description></item>
    /// </list>
    /// </param>
    /// <returns>
    /// The number of affected rows. Typically <c>1</c> for either insert or update operations.
    /// </returns>
    /// <remarks>
    /// <para>
    /// An upsert (also known as MERGE or INSERT ... ON CONFLICT) is a database operation that combines 
    /// INSERT and UPDATE. It's useful when you want to ensure a record exists with specific values, 
    /// regardless of whether it already exists.
    /// </para>
    /// <para>
    /// <strong>Requirements:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>The entity must have a primary key defined</description></item>
    /// <item><description>The database dialect must support upsert operations (e.g., SQL Server MERGE, PostgreSQL INSERT ... ON CONFLICT)</description></item>
    /// </list>
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
    /// // Upsert a product - inserts if Id doesn't exist, updates if it does
    /// var product = new Product { Id = 1, Name = "Widget", Price = 19.99m };
    /// int affected = connection.Upsert(product);
    /// 
    /// // First call: inserts the product (affected = 1)
    /// // Second call with same Id: updates the product (affected = 1)
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the entity has no primary key, if the database dialect doesn't support upsert operations, 
    /// or if no upsertable columns are found.
    /// </exception>
    /// <seealso cref="Upsert{T}(IDbConnection, T, CommandOptions)"/>
    /// <seealso cref="UpsertAsync{T}(IDbConnection, T, CancellationToken)"/>
    /// <seealso cref="Insert{T}(IDbConnection, T)"/>
    /// <seealso cref="Update{T}(IDbConnection, T)"/>
    public static int Upsert<T>(this IDbConnection connection, T entity) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
        return UpsertCore(connection, entity, default);
    }

    /// <summary>
    /// Performs an upsert (INSERT or UPDATE) operation on an entity with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to upsert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the upsert against.</param>
    /// <param name="entity">The entity instance to upsert.</param>
    /// <param name="options">
    /// Command options for configuring the upsert execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>
    /// The number of affected rows. Typically <c>1</c> for either insert or update operations.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the upsert within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Upsert with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = new Product { Id = 1, Name = "Widget", Price = 19.99m };
    /// int affected = connection.Upsert(product, CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// 
    /// // Upsert with timeout
    /// var product = new Product { Id = 1, Name = "Widget", Price = 19.99m };
    /// int affected = connection.Upsert(
    ///     product, 
    ///     CommandOptions.WithTimeout(30));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the entity has no primary key, if the database dialect doesn't support upsert operations, 
    /// or if no upsertable columns are found.
    /// </exception>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="Upsert{T}(IDbConnection, T)"/>
    /// <seealso cref="UpsertAsync{T}(IDbConnection, T, CommandOptions, CancellationToken)"/>
    public static int Upsert<T>(this IDbConnection connection, T entity, CommandOptions options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
        return UpsertCore(connection, entity, options);
    }

    private static int UpsertCore<T>(IDbConnection connection, T entity, CommandOptions options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (!cached.SupportsUpsert)
            throw new InvalidOperationException($"The database dialect does not support upsert operations.");

        if (string.IsNullOrEmpty(cached.UpsertSql))
            throw new InvalidOperationException($"Cannot upsert entity of type '{typeof(T).Name}': No primary key found or no upsertable columns.");

        // AUD-R26: Upsert implemented its execution inline in this file and so was the one
        // single-entity write that never reached the interceptor pipeline, unlike Insert/Update/
        // Delete which all route through their *Core.cs equivalents. It is fully buffered, so the
        // streaming exemption in ICommandInterceptor's remarks does not apply to it.
        return CommandObservation.Execute(
            cached.UpsertSql,
            entity,
            connection,
            options.CommandType,
            () => UpsertCoreDirect(connection, entity, cached, options));
    }

    private static int UpsertCoreDirect<T>(IDbConnection connection, T entity, CachedCrudSql cached, CommandOptions options) where T : new()
    {
        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.UpsertSql;

            // A DbConnection's IDbCommand.Transaction setter is DbCommand's explicit interface
            // implementation, which casts to DbTransaction internally - assigning a non-DbTransaction
            // IDbTransaction through it throws an opaque InvalidCastException. Validate via
            // AsyncTransactionValidator first (mirroring GetByIdSimpleCoreDirect) so an incompatible
            // transaction gets Jaunty's clear ArgumentException instead.
            if (options.Transaction is not null)
            {
                command.Transaction = connection is DbConnection
                    ? AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
                    : options.Transaction;
            }

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            // Bind parameters from entity properties (all non-identity, non-computed columns)
            BindUpsertParameters(command, entity, cached.Metadata);

            CommandObservation.Log(command.CommandText, entity);

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static void BindUpsertParameters<T>(IDbCommand command, T entity, EntityMetadata metadata) where T : new()
    {
        IReadOnlyList<ColumnMetadata> insertColumns = metadata.InsertColumns;
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;

        var addedParams = new HashSet<string>(CommonConstants.OrdinalIgnoreCase);

        for (int i = 0; i < insertColumns.Count; i++)
        {
            ColumnMetadata col = insertColumns[i];

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + col.ColumnName;
            param.Value = ParameterBinder.ApplyTypeHandlerIfNeeded(GetColumnValue(col, entity), col.Property, col.EnumStorageOverride) ?? DBNull.Value;
            command.Parameters.Add(param);
            addedParams.Add(col.ColumnName);
        }

        for (int i = 0; i < primaryKeys.Count; i++)
        {
            ColumnMetadata col = primaryKeys[i];

            if (addedParams.Contains(col.ColumnName))
                continue;

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@" + col.ColumnName;
            param.Value = ParameterBinder.ApplyTypeHandlerIfNeeded(GetColumnValue(col, entity), col.Property, col.EnumStorageOverride) ?? DBNull.Value;
            command.Parameters.Add(param);
        }
    }

    private static object? GetColumnValue<T>(ColumnMetadata col, T entity)
        => col.Getter is { } getter ? getter(entity!) : col.Property!.GetValue(entity);
}