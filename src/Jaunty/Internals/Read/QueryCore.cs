using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Enums;
using Jaunty.Internals.Parameters;
using Jaunty.Internals.Read;

namespace Jaunty;

public static partial class Jaunty
{
    private static List<T> QueryCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<T>();
                var map = DrDispatcher.Resolve(reader, options, mode);

                while (reader.Read())
                    results.Add(map(reader));

                return results;
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<T>();
            var map = DrDispatcher.Resolve(reader, options, mode);

            while (reader.Read())
                results.Add(map(reader));

            return results;
        });
    }

    private static T QueryFirstCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        T? entity = QueryFirstOrDefaultCore(connection, sql, parameters, options, mode);
        return entity is null ? throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.") : entity;
    }

    private static T? QueryFirstOrDefaultCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read()) return default;
                var map = DrDispatcher.Resolve(reader, options, mode);
                return map(reader);
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read()) return default;
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            return map(reader);
        });
    }

    private static T QuerySingleCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read()) throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
                var map = DrDispatcher.Resolve(reader, options, mode);
                T entity = map(reader);
                return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.") : entity;
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read()) throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            T entity = map(reader);
            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.") : entity;
        });
    }

    private static T? QuerySingleOrDefaultCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read()) return default;
                var map = DrDispatcher.Resolve(reader, options, mode);
                T entity = map(reader);
                return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.") : entity;
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read()) return default;

            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            T entity = map(reader);

            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.") : entity;
        });
    }

    private static T QueryScalarCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options)
    {
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            Configuration.JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            var result = command.ExecuteScalar();

            if (result is null || result is DBNull)
                return default!;

            if (result is T direct)
                return direct;

            return ScalarConverter<T>.Convert(result);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static IEnumerable<T> QueryStreamCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        if (connection is DbConnection dbConnection)
        {
            foreach (var item in QueryStreamCoreFast<T>(dbConnection, sql, parameters, options, mode))
                yield return item;
            yield break;
        }

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

    private static IEnumerable<T> QueryStreamCoreFast<T>(DbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
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

    #region Multi-Entity Core Methods

    private static List<(T1, T2)> QueryMultiEntityCore<T1, T2>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode) where T1 : new() where T2 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<(T1, T2)>();

                if (!reader.Read())
                    return results;

                var mapping = MultiEntityMapper<T1, T2>.Build(reader);

                do
                {
                    var t1 = new T1();
                    var t2 = new T2();

                    mapping.ApplyT1(t1, reader);
                    mapping.ApplyT2(t2, reader);

                    results.Add((t1, t2));
                }
                while (reader.Read());

                return results;
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<(T1, T2)>();

            if (!reader.Read())
                return results;

            // Build mapping on first row
            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);

                results.Add((t1, t2));
            }
            while (reader.Read());

            return results;
        });
    }

    private static (T1, T2) QueryFirstMultiEntityCore<T1, T2>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode) where T1 : new() where T2 : new()
    {
        var result = QueryFirstOrDefaultMultiEntityCore<T1, T2>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name})'.") : result.Value;
    }

    private static (T1, T2)? QueryFirstOrDefaultMultiEntityCore<T1, T2>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode) where T1 : new() where T2 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return ((T1, T2)?)null;

                var mapping = MultiEntityMapper<T1, T2>.Build(reader);

                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);

                return (t1, t2);
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return ((T1, T2)?)null;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);

            return (t1, t2);
        });
    }

    private static (T1, T2) QuerySingleMultiEntityCore<T1, T2>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode) where T1 : new() where T2 : new()
    {
        var result = QuerySingleOrDefaultMultiEntityCore<T1, T2>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name})'.") : result.Value;
    }

    private static (T1, T2)? QuerySingleOrDefaultMultiEntityCore<T1, T2>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode) where T1 : new() where T2 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return ((T1, T2)?)null;

                var mapping = MultiEntityMapper<T1, T2>.Build(reader);

                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);

                return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name})'.") : ((T1, T2)?)(t1, t2);
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return ((T1, T2)?)null;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);

            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name})'.") : ((T1, T2)?)(t1, t2);
        });
    }

    private static IEnumerable<(T1, T2)> QueryStreamMultiEntityCore<T1, T2>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode) where T1 : new() where T2 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            foreach (var item in QueryStreamMultiEntityCoreFast<T1, T2>(dbConnection, sql, parameters, options, mode))
                yield return item;
            yield break;
        }

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

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);

                yield return (t1, t2);
            }
            while (reader.Read());
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static IEnumerable<(T1, T2)> QueryStreamMultiEntityCoreFast<T1, T2>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode) where T1 : new() where T2 : new()
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

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);

                yield return (t1, t2);
            }
            while (reader.Read());
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    #endregion

}
