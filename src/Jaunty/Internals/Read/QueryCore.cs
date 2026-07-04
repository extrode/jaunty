using System.Data;
using System.Data.Common;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Internals.Enums;
using Jaunty.Internals.Parameters;
using Jaunty.Internals.Read;
using Jaunty.Interceptors;

namespace Jaunty;

public static partial class Jaunty
{
    private static List<T> QueryCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        int capacity = options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity;
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<T>(capacity);
                Func<DbDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);

                while (reader.Read())
                    results.Add(map(reader));

                return results;
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<T>(capacity);
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);

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
                Func<DbDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
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
                Func<DbDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
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
                Func<DbDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
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
        // Use InterceptorPipeline if registered, otherwise execute directly
        if (JauntyConfig.InterceptorPipeline?.HasInterceptors == true)
        {
            T result = default!;
            JauntyConfig.InterceptorPipeline.ExecuteWithInterceptionAsync(
                sql,
                parameters,
                connection,
                options.CommandType,
                () =>
                {
                    bool wasClosed = connection.State == ConnectionState.Closed;

                    try
                    {
                        if (wasClosed) connection.Open();

                        using IDbCommand command = connection.CreateCommand();
                        command.CommandText = sql;

                        if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                            command.CommandType = options.CommandType;

                        if (options.Transaction is not null)
                            command.Transaction = options.Transaction;

                        if (options.CommandTimeout.HasValue)
                            command.CommandTimeout = options.CommandTimeout.Value;

                        if (parameters is not null)
                            ParameterBinder.Bind(command, parameters);

                        object? commandResult = command.ExecuteScalar();

                        if (commandResult is null or DBNull)
                            result = default!;
                        else if (commandResult is T direct)
                            result = direct;
                        else
                            result = ScalarConverter<T>.Convert(commandResult);

                        return new ValueTask<T>(result);
                    }
                    finally
                    {
                        if (wasClosed && connection.State != ConnectionState.Closed)
                            connection.Close();
                    }
                },
                CancellationToken.None).GetAwaiter().GetResult();
            return result;
        }

        // Fast path: no interceptors, direct execution
        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            object? result = command.ExecuteScalar();

            if (result is null or DBNull)
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
            foreach (T? item in QueryStreamCoreFast<T>(dbConnection, sql, parameters, options, mode))
                yield return item;
            yield break;
        }

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using IDataReader reader = command.ExecuteReader();
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);

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
        // Resolve mapper early for common cases (user override, source-gen, reflection)
        // to avoid opening connection if resolution will fail
        Func<DbDataReader, T>? mapper = null;

        // 1. User override
        if (options.Mapper is not null)
        {
            Func<IDataReader, T> userMapper = options.Mapper;
            mapper = dbReader => userMapper(dbReader);
        }
        // 2. Source-generated (IMapped<T>)
        else if (mode == MappingMode.Strict && Internals.Read.MappedCache<T>.Mapper is not null)
        {
            Func<IDataReader, T> sgMapper = Internals.Read.MappedCache<T>.Mapper;
            mapper = dbReader => sgMapper(dbReader);
        }

        // If we couldn't resolve without reader, we'll need to resolve after opening
        bool needsReaderForMapper = mapper is null;

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using DbDataReader reader = command.ExecuteReader();

            // Resolve mapper now if we couldn't resolve it earlier
            if (needsReaderForMapper)
                mapper = DrDispatcher.Resolve(reader, options, mode);

            while (reader.Read())
                yield return mapper!(reader);
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
                var results = new List<(T1, T2)>(JauntyConfig.QueryResultCapacity * 2);

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
            var results = new List<(T1, T2)>(JauntyConfig.QueryResultCapacity * 2);

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
        (T1, T2)? result = QueryFirstOrDefaultMultiEntityCore<T1, T2>(connection, sql, parameters, options, mode);
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
        (T1, T2)? result = QuerySingleOrDefaultMultiEntityCore<T1, T2>(connection, sql, parameters, options, mode);
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
            foreach ((T1, T2) item in QueryStreamMultiEntityCoreFast<T1, T2>(dbConnection, sql, parameters, options, mode))
                yield return item;
            yield break;
        }

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using IDataReader reader = command.ExecuteReader();

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

            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using DbDataReader reader = command.ExecuteReader();

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

    #region N-ary Multi-Entity Core Methods

    private static List<(T1, T2, T3)> QueryMultiEntityCore<T1, T2, T3>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<(T1, T2, T3)>(JauntyConfig.QueryResultCapacity);

                if (!reader.Read())
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3>.Build(reader);

                do
                {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);

                    results.Add((t1, t2, t3));
                }
                while (reader.Read());

                return results;
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<(T1, T2, T3)>(JauntyConfig.QueryResultCapacity);

            if (!reader.Read())
                return results;

            var mapping = MultiEntityMapper<T1, T2, T3>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);

                results.Add((t1, t2, t3));
            }
            while (reader.Read());

            return results;
        });
    }

    private static (T1, T2, T3) QueryFirstMultiEntityCore<T1, T2, T3>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        (T1, T2, T3)? result = QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name})'.") : result.Value;
    }

    private static (T1, T2, T3)? QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return (((T1, T2, T3)?)null);

                var mapping = MultiEntityMapper<T1, T2, T3>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);

                return (t1, t2, t3);
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return (((T1, T2, T3)?)null);

            var mapping = MultiEntityMapper<T1, T2, T3>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);

            return (t1, t2, t3);
        });
    }

    private static (T1, T2, T3) QuerySingleMultiEntityCore<T1, T2, T3>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        (T1, T2, T3)? result = QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name})'.") : result.Value;
    }

    private static (T1, T2, T3)? QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return (((T1, T2, T3)?)null);

                var mapping = MultiEntityMapper<T1, T2, T3>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);

                return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name})'.") : (((T1, T2, T3)?)(t1, t2, t3));
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return (((T1, T2, T3)?)null);

            var mapping = MultiEntityMapper<T1, T2, T3>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);

            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name})'.") : (((T1, T2, T3)?)(t1, t2, t3));
        });
    }

    private static IEnumerable<(T1, T2, T3)> QueryStreamMultiEntityCore<T1, T2, T3>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            foreach ((T1, T2, T3) item in QueryStreamMultiEntityCoreFast<T1, T2, T3>(dbConnection, sql, parameters, options, mode))
                yield return item;
            yield break;
        }

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using IDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);

                yield return (t1, t2, t3);
            }
            while (reader.Read());
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static IEnumerable<(T1, T2, T3)> QueryStreamMultiEntityCoreFast<T1, T2, T3>(DbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using DbDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);

                yield return (t1, t2, t3);
            }
            while (reader.Read());
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static List<(T1, T2, T3, T4)> QueryMultiEntityCore<T1, T2, T3, T4>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<(T1, T2, T3, T4)>(JauntyConfig.QueryResultCapacity);

                if (!reader.Read())
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3, T4>.Build(reader);

                do
                {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);

                    results.Add((t1, t2, t3, t4));
                }
                while (reader.Read());

                return results;
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<(T1, T2, T3, T4)>(JauntyConfig.QueryResultCapacity);

            if (!reader.Read())
                return results;

            var mapping = MultiEntityMapper<T1, T2, T3, T4>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);

                results.Add((t1, t2, t3, t4));
            }
            while (reader.Read());

            return results;
        });
    }

    private static (T1, T2, T3, T4) QueryFirstMultiEntityCore<T1, T2, T3, T4>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        (T1, T2, T3, T4)? result = QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4)? QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return (((T1, T2, T3, T4)?)null);

                var mapping = MultiEntityMapper<T1, T2, T3, T4>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);

                return (t1, t2, t3, t4);
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return (((T1, T2, T3, T4)?)null);

            var mapping = MultiEntityMapper<T1, T2, T3, T4>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);

            return (t1, t2, t3, t4);
        });
    }

    private static (T1, T2, T3, T4) QuerySingleMultiEntityCore<T1, T2, T3, T4>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        (T1, T2, T3, T4)? result = QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4)? QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return (((T1, T2, T3, T4)?)null);

                var mapping = MultiEntityMapper<T1, T2, T3, T4>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);

                return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name})'.") : (((T1, T2, T3, T4)?)(t1, t2, t3, t4));
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return (((T1, T2, T3, T4)?)null);

            var mapping = MultiEntityMapper<T1, T2, T3, T4>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);

            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name})'.") : (((T1, T2, T3, T4)?)(t1, t2, t3, t4));
        });
    }

    private static IEnumerable<(T1, T2, T3, T4)> QueryStreamMultiEntityCore<T1, T2, T3, T4>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            foreach ((T1, T2, T3, T4) item in QueryStreamMultiEntityCoreFast<T1, T2, T3, T4>(dbConnection, sql, parameters, options, mode))
                yield return item;
            yield break;
        }

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using IDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3, T4>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);

                yield return (t1, t2, t3, t4);
            }
            while (reader.Read());
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static IEnumerable<(T1, T2, T3, T4)> QueryStreamMultiEntityCoreFast<T1, T2, T3, T4>(DbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using DbDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3, T4>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);

                yield return (t1, t2, t3, t4);
            }
            while (reader.Read());
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static List<(T1, T2, T3, T4, T5)> QueryMultiEntityCore<T1, T2, T3, T4, T5>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<(T1, T2, T3, T4, T5)>(JauntyConfig.QueryResultCapacity);

                if (!reader.Read())
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(reader);

                do
                {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);

                    results.Add((t1, t2, t3, t4, t5));
                }
                while (reader.Read());

                return results;
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<(T1, T2, T3, T4, T5)>(JauntyConfig.QueryResultCapacity);

            if (!reader.Read())
                return results;

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);

                results.Add((t1, t2, t3, t4, t5));
            }
            while (reader.Read());

            return results;
        });
    }

    private static (T1, T2, T3, T4, T5) QueryFirstMultiEntityCore<T1, T2, T3, T4, T5>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        (T1, T2, T3, T4, T5)? result = QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4, T5>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4, T5)? QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4, T5>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return (((T1, T2, T3, T4, T5)?)null);

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);

                return (t1, t2, t3, t4, t5);
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return (((T1, T2, T3, T4, T5)?)null);

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);

            return (t1, t2, t3, t4, t5);
        });
    }

    private static (T1, T2, T3, T4, T5) QuerySingleMultiEntityCore<T1, T2, T3, T4, T5>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        (T1, T2, T3, T4, T5)? result = QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4, T5>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4, T5)? QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4, T5>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return (((T1, T2, T3, T4, T5)?)null);

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);

                return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name})'.") : (((T1, T2, T3, T4, T5)?)(t1, t2, t3, t4, t5));
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return (((T1, T2, T3, T4, T5)?)null);

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);

            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name})'.") : (((T1, T2, T3, T4, T5)?)(t1, t2, t3, t4, t5));
        });
    }

    private static IEnumerable<(T1, T2, T3, T4, T5)> QueryStreamMultiEntityCore<T1, T2, T3, T4, T5>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            foreach ((T1, T2, T3, T4, T5) item in QueryStreamMultiEntityCoreFast<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, mode))
                yield return item;
            yield break;
        }

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using IDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);

                yield return (t1, t2, t3, t4, t5);
            }
            while (reader.Read());
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static IEnumerable<(T1, T2, T3, T4, T5)> QueryStreamMultiEntityCoreFast<T1, T2, T3, T4, T5>(DbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using DbDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);

                yield return (t1, t2, t3, t4, t5);
            }
            while (reader.Read());
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static List<(T1, T2, T3, T4, T5, T6)> QueryMultiEntityCore<T1, T2, T3, T4, T5, T6>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<(T1, T2, T3, T4, T5, T6)>(JauntyConfig.QueryResultCapacity);

                if (!reader.Read())
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(reader);

                do
                {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);

                    results.Add((t1, t2, t3, t4, t5, t6));
                }
                while (reader.Read());

                return results;
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<(T1, T2, T3, T4, T5, T6)>(JauntyConfig.QueryResultCapacity);

            if (!reader.Read())
                return results;

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);

                results.Add((t1, t2, t3, t4, t5, t6));
            }
            while (reader.Read());

            return results;
        });
    }

    private static (T1, T2, T3, T4, T5, T6) QueryFirstMultiEntityCore<T1, T2, T3, T4, T5, T6>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        (T1, T2, T3, T4, T5, T6)? result = QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4, T5, T6)? QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return (((T1, T2, T3, T4, T5, T6)?)null);

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);

                return (t1, t2, t3, t4, t5, t6);
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return (((T1, T2, T3, T4, T5, T6)?)null);

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);

            return (t1, t2, t3, t4, t5, t6);
        });
    }

    private static (T1, T2, T3, T4, T5, T6) QuerySingleMultiEntityCore<T1, T2, T3, T4, T5, T6>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        (T1, T2, T3, T4, T5, T6)? result = QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4, T5, T6)? QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return (((T1, T2, T3, T4, T5, T6)?)null);

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);

                return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name})'.") : (((T1, T2, T3, T4, T5, T6)?)(t1, t2, t3, t4, t5, t6));
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return (((T1, T2, T3, T4, T5, T6)?)null);

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);

            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name})'.") : (((T1, T2, T3, T4, T5, T6)?)(t1, t2, t3, t4, t5, t6));
        });
    }

    private static IEnumerable<(T1, T2, T3, T4, T5, T6)> QueryStreamMultiEntityCore<T1, T2, T3, T4, T5, T6>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            foreach ((T1, T2, T3, T4, T5, T6) item in QueryStreamMultiEntityCoreFast<T1, T2, T3, T4, T5, T6>(dbConnection, sql, parameters, options, mode))
                yield return item;
            yield break;
        }

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using IDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);

                yield return (t1, t2, t3, t4, t5, t6);
            }
            while (reader.Read());
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static IEnumerable<(T1, T2, T3, T4, T5, T6)> QueryStreamMultiEntityCoreFast<T1, T2, T3, T4, T5, T6>(DbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using DbDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);

                yield return (t1, t2, t3, t4, t5, t6);
            }
            while (reader.Read());
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static List<(T1, T2, T3, T4, T5, T6, T7)> QueryMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<(T1, T2, T3, T4, T5, T6, T7)>(JauntyConfig.QueryResultCapacity);

                if (!reader.Read())
                    return results;

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(reader);

                do
                {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();
            var t7 = new T7();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);
            mapping.ApplyT7(t7, reader);

                    results.Add((t1, t2, t3, t4, t5, t6, t7));
                }
                while (reader.Read());

                return results;
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<(T1, T2, T3, T4, T5, T6, T7)>(JauntyConfig.QueryResultCapacity);

            if (!reader.Read())
                return results;

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();
            var t7 = new T7();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);
            mapping.ApplyT7(t7, reader);

                results.Add((t1, t2, t3, t4, t5, t6, t7));
            }
            while (reader.Read());

            return results;
        });
    }

    private static (T1, T2, T3, T4, T5, T6, T7) QueryFirstMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        (T1, T2, T3, T4, T5, T6, T7)? result = QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name}, {typeof(T7).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4, T5, T6, T7)? QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return (((T1, T2, T3, T4, T5, T6, T7)?)null);

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();
            var t7 = new T7();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);
            mapping.ApplyT7(t7, reader);

                return (t1, t2, t3, t4, t5, t6, t7);
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return (((T1, T2, T3, T4, T5, T6, T7)?)null);

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();
            var t7 = new T7();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);
            mapping.ApplyT7(t7, reader);

            return (t1, t2, t3, t4, t5, t6, t7);
        });
    }

    private static (T1, T2, T3, T4, T5, T6, T7) QuerySingleMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        (T1, T2, T3, T4, T5, T6, T7)? result = QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name}, {typeof(T7).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4, T5, T6, T7)? QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return (((T1, T2, T3, T4, T5, T6, T7)?)null);

                var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();
            var t7 = new T7();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);
            mapping.ApplyT7(t7, reader);

                return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name}, {typeof(T7).Name})'.") : (((T1, T2, T3, T4, T5, T6, T7)?)(t1, t2, t3, t4, t5, t6, t7));
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return (((T1, T2, T3, T4, T5, T6, T7)?)null);

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();
            var t7 = new T7();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);
            mapping.ApplyT7(t7, reader);

            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name}, {typeof(T7).Name})'.") : (((T1, T2, T3, T4, T5, T6, T7)?)(t1, t2, t3, t4, t5, t6, t7));
        });
    }

    private static IEnumerable<(T1, T2, T3, T4, T5, T6, T7)> QueryStreamMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        if (connection is DbConnection dbConnection)
        {
            foreach ((T1, T2, T3, T4, T5, T6, T7) item in QueryStreamMultiEntityCoreFast<T1, T2, T3, T4, T5, T6, T7>(dbConnection, sql, parameters, options, mode))
                yield return item;
            yield break;
        }

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using IDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();
            var t7 = new T7();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);
            mapping.ApplyT7(t7, reader);

                yield return (t1, t2, t3, t4, t5, t6, t7);
            }
            while (reader.Read());
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    private static IEnumerable<(T1, T2, T3, T4, T5, T6, T7)> QueryStreamMultiEntityCoreFast<T1, T2, T3, T4, T5, T6, T7>(DbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using DbDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(reader);

            do
            {
            var t1 = new T1();
            var t2 = new T2();
            var t3 = new T3();
            var t4 = new T4();
            var t5 = new T5();
            var t6 = new T6();
            var t7 = new T7();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);
            mapping.ApplyT3(t3, reader);
            mapping.ApplyT4(t4, reader);
            mapping.ApplyT5(t5, reader);
            mapping.ApplyT6(t6, reader);
            mapping.ApplyT7(t7, reader);

                yield return (t1, t2, t3, t4, t5, t6, t7);
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