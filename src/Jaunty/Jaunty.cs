using System.Data;
using System.Data.Common;

using Jaunty.Enums;
using Jaunty.Internal.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    private static T QueryScalarCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options)
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (reader is DbDataReader dbReader)
                return !dbReader.Read() || dbReader.IsDBNull(0) ? default! : dbReader.GetFieldValue<T>(0);

            if (!reader.Read() || reader.IsDBNull(0)) return default!;

            var obj = reader.GetValue(0);
            return (T)Convert.ChangeType(obj, typeof(T));
        });
    }

    private static List<T> QueryCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<T>();
            var map = DrDispatcher.Resolve(reader, options, mode);

            while (reader.Read())
                results.Add(map(reader));

            return results;
        });
    }

    private static IEnumerable<T> QueryStreamCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using var reader = command.ExecuteReader();
            var map = DrDispatcher.Resolve(reader, options, mode);

            while (reader.Read())
                yield return map(reader);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static TResult ExecuteReader<TResult>(IDbConnection connection, string sql, object? parameters, CommandOptions options, Func<IDataReader, TResult> handler)
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
            if (wasClosed) connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using var reader = command.ExecuteReader();
            return handler(reader);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static T QueryFirstCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        T? entity = QueryFirstOrDefaultCore<T>(connection, sql, parameters, options, mode);
        return entity is null ? throw new InvalidOperationException("Sequence contains no elements") : entity;
    }

    private static T? QueryFirstOrDefaultCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read()) return default;
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            return map(reader);
        });
    }

    private static T QuerySingleCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        T? entity = QuerySingleOrDefaultCore<T>(connection, sql, parameters, options, mode);
        return entity is null ? throw new InvalidOperationException("Sequence contains no elements") : entity;
    }

    private static T? QuerySingleOrDefaultCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read()) return default;
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            T entity = map(reader);
            return reader.Read() ? throw new InvalidOperationException("Sequence contains more than one element") : entity;
        });
    }
}