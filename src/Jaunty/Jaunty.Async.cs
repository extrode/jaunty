using System.Data;
using System.Data.Common;

using Jaunty.Entity;
using Jaunty.Enums;
using Jaunty.Internal.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    internal static async Task<T> QueryScalarCoreAsync<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, CancellationToken cancellationToken)
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options.Transaction, options.CommandTimeout, async reader =>
        {
            if (reader is DbDataReader dbReader)
            {
                return !await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false) || await dbReader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false) ? default!
                    : await dbReader.GetFieldValueAsync<T>(0, cancellationToken).ConfigureAwait(false);
            }

            // Fallback for non-DbDataReader
            if (!reader.Read() || reader.IsDBNull(0)) return default!;
            var obj = reader.GetValue(0);
            return (T)Convert.ChangeType(obj, typeof(T));
        }, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<List<T>> QueryCoreAsync<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode, CancellationToken cancellationToken) where T : new()
    {
        return await ExecuteReaderAsync(connection, sql, parameters, options.Transaction, options.CommandTimeout, async reader =>
        {
            var results = new List<T>();
            var setters = MetadataCache<T>.GetSetters(reader, mode);

            if (reader is DbDataReader dbReader)
            {
                while (await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var entity = new T();

                    for (int i = 0; i < setters.Length; i++)
                        setters[i].Set(entity, reader);

                    results.Add(entity);
                }
            }
            else
            {
                // Fallback for non-DbDataReader
                while (reader.Read())
                {
                    var entity = new T();

                    for (int i = 0; i < setters.Length; i++)
                        setters[i].Set(entity, reader);

                    results.Add(entity);
                }
            }

            return results;
        }, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<TResult> ExecuteReaderAsync<TResult>(IDbConnection connection, string sql, object? parameters,
        IDbTransaction? transaction, int? commandTimeout, Func<IDataReader, Task<TResult>> handler, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException(nameof(sql));
#endif
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (connection is DbConnection dbConnection)
            {
                if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

                using var command = dbConnection.CreateCommand();
                command.CommandText = sql;

                if (transaction is not null)
                    command.Transaction = transaction as DbTransaction;

                if (commandTimeout.HasValue)
                    command.CommandTimeout = commandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

                using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                return await handler(reader).ConfigureAwait(false);
            }
            else
            {
                // Fallback for non-DbConnection - use sync methods
                if (wasClosed) connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = sql;

                if (transaction is not null)
                    command.Transaction = transaction;

                if (commandTimeout.HasValue)
                    command.CommandTimeout = commandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

                using var reader = command.ExecuteReader();
                return await handler(reader).ConfigureAwait(false);
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }
}
