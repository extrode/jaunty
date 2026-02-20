using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Interfaces;
using Jaunty.Internals;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

namespace Jaunty;

public static partial class Jaunty
{
    #region Delete By Entity Core

    private static int DeleteByEntityCore<T>(IDbConnection connection, T entity, CommandOptions options) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (!cached.HasPrimaryKey)
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': No primary key found.");

        if (string.IsNullOrEmpty(cached.DeleteSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': Delete SQL could not be generated.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.DeleteSql;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

                        // Bind parameters from entity properties using compiled delegate
                        WriteParameterCache<T>.DeleteBinder(command, entity);
            
                        Configuration.JauntyConfig.Logger?.Invoke(command.CommandText, entity);
            
                        return command.ExecuteNonQuery();
            
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static async ValueTask<int> DeleteByEntityCoreAsync<T>(DbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (!cached.HasPrimaryKey)
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': No primary key found.");

        if (string.IsNullOrEmpty(cached.DeleteSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': Delete SQL could not be generated.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            await using DbCommand command = connection.CreateCommand();
#else
            using DbCommand command = connection.CreateCommand();
#endif
            command.CommandText = cached.DeleteSql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

                        // Bind parameters from entity properties using compiled delegate
                        WriteParameterCache<T>.DeleteBinder(command, entity);
            
                        Configuration.JauntyConfig.Logger?.Invoke(command.CommandText, entity);
            
                        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            
        }
        finally
        {
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

    #endregion

    #region Delete By ID Core

    private static int DeleteByIdCore<T>(IDbConnection connection, object id, CommandOptions options) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (!cached.HasSinglePrimaryKey)
            throw new InvalidOperationException($"Cannot delete by ID for type '{typeof(T).Name}': Entity must have exactly one primary key. Use Delete(entity) for composite keys.");

        if (string.IsNullOrEmpty(cached.DeleteByIdSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': Delete by ID SQL could not be generated.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.DeleteByIdSql;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@Id";
            param.Value = id;
            command.Parameters.Add(param);

            Configuration.JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static async ValueTask<int> DeleteByIdCoreAsync<T>(DbConnection connection, object id, CommandOptions options, CancellationToken cancellationToken) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (!cached.HasSinglePrimaryKey)
            throw new InvalidOperationException($"Cannot delete by ID for type '{typeof(T).Name}': Entity must have exactly one primary key. Use DeleteAsync(entity) for composite keys.");

        if (string.IsNullOrEmpty(cached.DeleteByIdSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': Delete by ID SQL could not be generated.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            await using DbCommand command = connection.CreateCommand();
#else
            using DbCommand command = connection.CreateCommand();
#endif
            command.CommandText = cached.DeleteByIdSql;

            if (options.Transaction is not null)
                command.Transaction = (DbTransaction)options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            DbParameter param = command.CreateParameter();
            param.ParameterName = "@Id";
            param.Value = id;
            command.Parameters.Add(param);

            Configuration.JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
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

    #endregion

    #region Delete by IEntity<T> Core

    private static int DeleteByIdCore<T, TId>(IDbConnection connection, TId id, CommandOptions options) where T : IEntity<TId>
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);
        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = cached.DeleteByIdSql;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = "@Id";
            param.Value = id;
            command.Parameters.Add(param);

            Configuration.JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static async ValueTask<int> DeleteByIdCoreAsync<T, TId>(DbConnection connection, object id, CommandOptions options, CancellationToken cancellationToken) where T : IEntity<TId>
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);
        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            await using DbCommand command = connection.CreateCommand();
#else
            using DbCommand command = connection.CreateCommand();
#endif
            command.CommandText = cached.DeleteByIdSql;

            if (options.Transaction is not null)
                command.Transaction = (DbTransaction)options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            DbParameter param = command.CreateParameter();
            param.ParameterName = "@Id";
            param.Value = id;
            command.Parameters.Add(param);

            Configuration.JauntyConfig.Logger?.Invoke(command.CommandText, new { Id = id });

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
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

    #endregion
}

