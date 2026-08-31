using System.Data;
using System.Data.Common;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Internals.Parameters;
using Jaunty.Internals.Read;
using Jaunty.Interceptors;
using Jaunty.Internals;

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
        // AUD-R35-009. This used to delegate to QueryFirstOrDefaultCore and test `entity is null`.
        // `T` is constrained only by `new()`, so for a value type - and `QueryFirst<(int, string)>`
        // is an API the suite exercises - the empty-result sentinel is `default(T)`, which is never
        // null: an empty result set returned `(0, null)` instead of throwing. The async twin
        // (QueryCoreAsync.QueryFirstCoreAsync) reads the reader itself and throws directly; this
        // now does the same, so the two agree for every T.
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");

                Func<DbDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
                return map(reader);
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");

            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, options, mode);
            return map(reader);
        });
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

    // AUD-R34-004: the multi-entity cores delegate to the single-entity ones when options.Mapper is
    // set (AUD-R33-004 / AUD-R34-001), but QueryFirstOrDefaultCore/QuerySingleOrDefaultCore return
    // T? over a type parameter constrained only by new(). For a ValueTuple that T? is the tuple
    // itself, so an empty result set came back as (default, default) and the implicit conversion to
    // (T1, ..., TN)? set HasValue - the OrDefault methods returned a tuple of nulls instead of null,
    // and the null guards in QueryFirst/QuerySingle never fired. These twins constrain the tuple to
    // struct, so default is a real null and both mapper paths keep the reflection path's contract.
    private static TTuple? QueryFirstOrDefaultMappedTupleCore<TTuple>(IDbConnection connection, string sql, object? parameters, CommandOptions<TTuple> options, MappingMode mode) where TTuple : struct
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read()) return (TTuple?)null;
                Func<DbDataReader, TTuple> map = DrDispatcher.Resolve(reader, options, mode);
                return map(reader);
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read()) return (TTuple?)null;
            Func<IDataReader, TTuple> map = DrDispatcher.Resolve(reader, options, mode);
            return map(reader);
        });
    }

    // describeType is only ever invoked on the more-than-one-row failure. It is passed rather than
    // derived from typeof(TTuple), whose Name is "ValueTuple`2" - callers already build the
    // "(T1, T2)" wording their reflection-mapping path throws, and a non-capturing lambda is cached
    // by the compiler, so matching the two messages costs no per-call allocation.
    private static TTuple? QuerySingleOrDefaultMappedTupleCore<TTuple>(IDbConnection connection, string sql, object? parameters, CommandOptions<TTuple> options, MappingMode mode, Func<string> describeType) where TTuple : struct
    {
        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read()) return (TTuple?)null;
                Func<DbDataReader, TTuple> map = DrDispatcher.Resolve(reader, options, mode);
                TTuple entity = map(reader);
                return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '{describeType()}'.") : (TTuple?)entity;
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read()) return (TTuple?)null;
            Func<IDataReader, TTuple> map = DrDispatcher.Resolve(reader, options, mode);
            TTuple entity = map(reader);
            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '{describeType()}'.") : (TTuple?)entity;
        });
    }

    private static T QueryScalarCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions<T> options)
    {
        // Use InterceptorPipeline if registered, otherwise execute directly
        // One resolution, one read: CommandObservation decides whether anything is watching
        // (interceptor, diagnostics subscriber, or both) and hands back what to route through.
        InterceptorPipeline? pipeline = CommandObservation.Observer;

        if (pipeline is not null)
        {
            T result = default!;
            pipeline.ExecuteWithInterception(
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

                        // A DbConnection's IDbCommand.Transaction setter is DbCommand's explicit interface
                        // implementation, which casts to DbTransaction internally - assigning a
                        // non-DbTransaction IDbTransaction through it throws an opaque
                        // InvalidCastException. Validate via AsyncTransactionValidator first (mirroring
                        // GetByIdSimpleCoreDirect) so an incompatible transaction gets Jaunty's clear
                        // ArgumentException instead.
                        if (options.Transaction is not null)
                        {
                            command.Transaction = connection is DbConnection
                                ? AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
                                : options.Transaction;
                        }

                        if (options.CommandTimeout.HasValue)
                            command.CommandTimeout = options.CommandTimeout.Value;

                        if (parameters is not null)
                            ParameterBinder.Bind(command, parameters);

                        JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

                        // AUD-R26: options.Mapper was accepted and discarded here. See ScalarExecution.
                        result = ScalarExecution.Execute(command, options.Mapper);

                        return result;
                    }
                    finally
                    {
                        if (wasClosed && connection.State != ConnectionState.Closed)
                            connection.Close();
                    }
                });
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

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            // AUD-R26: options.Mapper was accepted and discarded here. See ScalarExecution.
            return ScalarExecution.Execute(command, options.Mapper);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    // Deliberately no InterceptorPipeline here, unlike QueryCore/QueryFirstCore/QuerySingleCore
    // above (R24 flagged the divergence as undocumented and untested; it is intentional).
    // ExecuteWithInterception wraps a delegate and reports the command as completed when that
    // delegate returns. This method is a lazy iterator: nothing runs until the caller enumerates,
    // and the reader stays open for the whole enumeration, so there is no point at which the
    // pipeline could report completion without first materializing every row into a list - which
    // is exactly what streaming exists to avoid. ExecuteQueryMultiple can wrap its execution
    // because ExecuteReader() there really does run the command in one round trip (see the
    // rationale comment in ExecuteQueryMultiple.cs); that does not hold here.
    // ICommandInterceptor's docs state this contract for callers; ReadCoreInterceptorTests pins it.
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

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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
        // 2. Source-generated - prefer MapperFactory (shape validated once here instead of per
        // row) over the plain per-row Mapper delegate, matching DrDispatcher.Resolve's ordering.
        // MapperFactory needs a reader to build the row mapper, so leave 'mapper' unset here and
        // let it resolve via DrDispatcher.Resolve below once the reader is open; checking Mapper
        // first would silently drop this optimization for any type that has both.
        else if (mode == MappingMode.Strict
            && Internals.Read.MappedCache<T>.MapperFactory is null
            && Internals.Read.MappedCache<T>.Mapper is not null)
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

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QueryCore<(T1, T2)>(connection, sql, parameters, options, mode);

        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<(T1, T2)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

                if (!reader.Read())
                    return results;

                var mapping = MultiEntityMapper<T1, T2>.Build(reader);

                do
                {
                    var t1 = new T1();
                    var t2 = new T2();

                    mapping.Map(t1, t2, reader);

                    results.Add((t1, t2));
                }
                while (reader.Read());

                return results;
            });
        }

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<(T1, T2)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

            if (!reader.Read())
                return results;

            // Build mapping on first row
            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.Map(t1, t2, reader);

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
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QueryFirstOrDefaultMappedTupleCore<(T1, T2)>(connection, sql, parameters, options, mode);

        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return ((T1, T2)?)null;

                var mapping = MultiEntityMapper<T1, T2>.Build(reader);

                var t1 = new T1();
                var t2 = new T2();

                mapping.Map(t1, t2, reader);

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

            mapping.Map(t1, t2, reader);

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
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QuerySingleOrDefaultMappedTupleCore<(T1, T2)>(connection, sql, parameters, options, mode, static () => $"({typeof(T1).Name}, {typeof(T2).Name})");

        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                if (!reader.Read())
                    return ((T1, T2)?)null;

                var mapping = MultiEntityMapper<T1, T2>.Build(reader);

                var t1 = new T1();
                var t2 = new T2();

                mapping.Map(t1, t2, reader);

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

            mapping.Map(t1, t2, reader);

            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name})'.") : ((T1, T2)?)(t1, t2);
        });
    }

    private static IEnumerable<(T1, T2)> QueryStreamMultiEntityCore<T1, T2>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2)> options, MappingMode mode) where T1 : new() where T2 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
        {
            foreach ((T1, T2) mapped in QueryStreamCore<(T1, T2)>(connection, sql, parameters, options, mode))
                yield return mapped;
            yield break;
        }

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

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            using IDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.Map(t1, t2, reader);

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
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
        {
            foreach ((T1, T2) mapped in QueryStreamCoreFast<(T1, T2)>(connection, sql, parameters, options, mode))
                yield return mapped;
            yield break;
        }

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            using DbDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.Map(t1, t2, reader);

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

    private static List<(T1, T2, T3)> QueryMultiEntityCore<T1, T2, T3>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QueryCore<(T1, T2, T3)>(connection, sql, parameters, options, mode);

        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<(T1, T2, T3)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

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
            var results = new List<(T1, T2, T3)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

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

    private static (T1, T2, T3) QueryFirstMultiEntityCore<T1, T2, T3>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        (T1, T2, T3)? result = QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name})'.") : result.Value;
    }

    private static (T1, T2, T3)? QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QueryFirstOrDefaultMappedTupleCore<(T1, T2, T3)>(connection, sql, parameters, options, mode);

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

    private static (T1, T2, T3) QuerySingleMultiEntityCore<T1, T2, T3>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        (T1, T2, T3)? result = QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name})'.") : result.Value;
    }

    private static (T1, T2, T3)? QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QuerySingleOrDefaultMappedTupleCore<(T1, T2, T3)>(connection, sql, parameters, options, mode, static () => $"({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name})");

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

    private static IEnumerable<(T1, T2, T3)> QueryStreamMultiEntityCore<T1, T2, T3>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
        {
            foreach ((T1, T2, T3) mapped in QueryStreamCore<(T1, T2, T3)>(connection, sql, parameters, options, mode))
                yield return mapped;
            yield break;
        }

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

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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

    private static IEnumerable<(T1, T2, T3)> QueryStreamMultiEntityCoreFast<T1, T2, T3>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
        {
            foreach ((T1, T2, T3) mapped in QueryStreamCoreFast<(T1, T2, T3)>(connection, sql, parameters, options, mode))
                yield return mapped;
            yield break;
        }

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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

    private static List<(T1, T2, T3, T4)> QueryMultiEntityCore<T1, T2, T3, T4>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QueryCore<(T1, T2, T3, T4)>(connection, sql, parameters, options, mode);

        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<(T1, T2, T3, T4)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

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
            var results = new List<(T1, T2, T3, T4)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

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

    private static (T1, T2, T3, T4) QueryFirstMultiEntityCore<T1, T2, T3, T4>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        (T1, T2, T3, T4)? result = QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4)? QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QueryFirstOrDefaultMappedTupleCore<(T1, T2, T3, T4)>(connection, sql, parameters, options, mode);

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

    private static (T1, T2, T3, T4) QuerySingleMultiEntityCore<T1, T2, T3, T4>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        (T1, T2, T3, T4)? result = QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4)? QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QuerySingleOrDefaultMappedTupleCore<(T1, T2, T3, T4)>(connection, sql, parameters, options, mode, static () => $"({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name})");

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

    private static IEnumerable<(T1, T2, T3, T4)> QueryStreamMultiEntityCore<T1, T2, T3, T4>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
        {
            foreach ((T1, T2, T3, T4) mapped in QueryStreamCore<(T1, T2, T3, T4)>(connection, sql, parameters, options, mode))
                yield return mapped;
            yield break;
        }

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

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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

    private static IEnumerable<(T1, T2, T3, T4)> QueryStreamMultiEntityCoreFast<T1, T2, T3, T4>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
        {
            foreach ((T1, T2, T3, T4) mapped in QueryStreamCoreFast<(T1, T2, T3, T4)>(connection, sql, parameters, options, mode))
                yield return mapped;
            yield break;
        }

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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

    private static List<(T1, T2, T3, T4, T5)> QueryMultiEntityCore<T1, T2, T3, T4, T5>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QueryCore<(T1, T2, T3, T4, T5)>(connection, sql, parameters, options, mode);

        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<(T1, T2, T3, T4, T5)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

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
            var results = new List<(T1, T2, T3, T4, T5)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

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

    private static (T1, T2, T3, T4, T5) QueryFirstMultiEntityCore<T1, T2, T3, T4, T5>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        (T1, T2, T3, T4, T5)? result = QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4, T5>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4, T5)? QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4, T5>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QueryFirstOrDefaultMappedTupleCore<(T1, T2, T3, T4, T5)>(connection, sql, parameters, options, mode);

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

    private static (T1, T2, T3, T4, T5) QuerySingleMultiEntityCore<T1, T2, T3, T4, T5>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        (T1, T2, T3, T4, T5)? result = QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4, T5>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4, T5)? QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4, T5>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QuerySingleOrDefaultMappedTupleCore<(T1, T2, T3, T4, T5)>(connection, sql, parameters, options, mode, static () => $"({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name})");

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

    private static IEnumerable<(T1, T2, T3, T4, T5)> QueryStreamMultiEntityCore<T1, T2, T3, T4, T5>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
        {
            foreach ((T1, T2, T3, T4, T5) mapped in QueryStreamCore<(T1, T2, T3, T4, T5)>(connection, sql, parameters, options, mode))
                yield return mapped;
            yield break;
        }

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

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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

    private static IEnumerable<(T1, T2, T3, T4, T5)> QueryStreamMultiEntityCoreFast<T1, T2, T3, T4, T5>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
        {
            foreach ((T1, T2, T3, T4, T5) mapped in QueryStreamCoreFast<(T1, T2, T3, T4, T5)>(connection, sql, parameters, options, mode))
                yield return mapped;
            yield break;
        }

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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

    private static List<(T1, T2, T3, T4, T5, T6)> QueryMultiEntityCore<T1, T2, T3, T4, T5, T6>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QueryCore<(T1, T2, T3, T4, T5, T6)>(connection, sql, parameters, options, mode);

        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<(T1, T2, T3, T4, T5, T6)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

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
            var results = new List<(T1, T2, T3, T4, T5, T6)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

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

    private static (T1, T2, T3, T4, T5, T6) QueryFirstMultiEntityCore<T1, T2, T3, T4, T5, T6>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        (T1, T2, T3, T4, T5, T6)? result = QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4, T5, T6)? QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QueryFirstOrDefaultMappedTupleCore<(T1, T2, T3, T4, T5, T6)>(connection, sql, parameters, options, mode);

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

    private static (T1, T2, T3, T4, T5, T6) QuerySingleMultiEntityCore<T1, T2, T3, T4, T5, T6>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        (T1, T2, T3, T4, T5, T6)? result = QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4, T5, T6)? QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QuerySingleOrDefaultMappedTupleCore<(T1, T2, T3, T4, T5, T6)>(connection, sql, parameters, options, mode, static () => $"({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name})");

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

    private static IEnumerable<(T1, T2, T3, T4, T5, T6)> QueryStreamMultiEntityCore<T1, T2, T3, T4, T5, T6>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
        {
            foreach ((T1, T2, T3, T4, T5, T6) mapped in QueryStreamCore<(T1, T2, T3, T4, T5, T6)>(connection, sql, parameters, options, mode))
                yield return mapped;
            yield break;
        }

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

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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

    private static IEnumerable<(T1, T2, T3, T4, T5, T6)> QueryStreamMultiEntityCoreFast<T1, T2, T3, T4, T5, T6>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
        {
            foreach ((T1, T2, T3, T4, T5, T6) mapped in QueryStreamCoreFast<(T1, T2, T3, T4, T5, T6)>(connection, sql, parameters, options, mode))
                yield return mapped;
            yield break;
        }

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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

    private static List<(T1, T2, T3, T4, T5, T6, T7)> QueryMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6, T7)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QueryCore<(T1, T2, T3, T4, T5, T6, T7)>(connection, sql, parameters, options, mode);

        if (connection is DbConnection dbConnection)
        {
            return ExecuteReader(dbConnection, sql, parameters, options, reader =>
            {
                var results = new List<(T1, T2, T3, T4, T5, T6, T7)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

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
            var results = new List<(T1, T2, T3, T4, T5, T6, T7)>(options.ExpectedRowCount ?? JauntyConfig.QueryResultCapacity);

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

    private static (T1, T2, T3, T4, T5, T6, T7) QueryFirstMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6, T7)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        (T1, T2, T3, T4, T5, T6, T7)? result = QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name}, {typeof(T7).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4, T5, T6, T7)? QueryFirstOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6, T7)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QueryFirstOrDefaultMappedTupleCore<(T1, T2, T3, T4, T5, T6, T7)>(connection, sql, parameters, options, mode);

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

    private static (T1, T2, T3, T4, T5, T6, T7) QuerySingleMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6, T7)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        (T1, T2, T3, T4, T5, T6, T7)? result = QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(connection, sql, parameters, options, mode);
        return result is null ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name}, {typeof(T7).Name})'.") : result.Value;
    }

    private static (T1, T2, T3, T4, T5, T6, T7)? QuerySingleOrDefaultMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6, T7)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
            return QuerySingleOrDefaultMappedTupleCore<(T1, T2, T3, T4, T5, T6, T7)>(connection, sql, parameters, options, mode, static () => $"({typeof(T1).Name}, {typeof(T2).Name}, {typeof(T3).Name}, {typeof(T4).Name}, {typeof(T5).Name}, {typeof(T6).Name}, {typeof(T7).Name})");

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

    private static IEnumerable<(T1, T2, T3, T4, T5, T6, T7)> QueryStreamMultiEntityCore<T1, T2, T3, T4, T5, T6, T7>(IDbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6, T7)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
        {
            foreach ((T1, T2, T3, T4, T5, T6, T7) mapped in QueryStreamCore<(T1, T2, T3, T4, T5, T6, T7)>(connection, sql, parameters, options, mode))
                yield return mapped;
            yield break;
        }

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

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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

    private static IEnumerable<(T1, T2, T3, T4, T5, T6, T7)> QueryStreamMultiEntityCoreFast<T1, T2, T3, T4, T5, T6, T7>(DbConnection connection, string sql, object? parameters, CommandOptions<(T1, T2, T3, T4, T5, T6, T7)> options, MappingMode mode) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        // AUD-R33-004: options.Mapper was accepted and discarded on every multi-entity core.
        // The tuple satisfies new(), and DrDispatcher returns options.Mapper ahead of every
        // other strategy, so the single-entity core is the honouring path already written and
        // tested - delegating to it is exact rather than a second implementation to keep in step.
        if (options.Mapper is not null)
        {
            foreach ((T1, T2, T3, T4, T5, T6, T7) mapped in QueryStreamCoreFast<(T1, T2, T3, T4, T5, T6, T7)>(connection, sql, parameters, options, mode))
                yield return mapped;
            yield break;
        }

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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