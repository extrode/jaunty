using System.Data;
using System.Data.Common;

using Jaunty.Internal.Execution;
using Jaunty.Internal.Mapping;

namespace Jaunty;

public static partial class Jaunty
{
    #region QueryScalarAsync

    public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default)
    {
        return QueryScalarCoreAsync<T>(connection, sql, null, default, cancellationToken);
    }

    public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default)
    {
        return QueryScalarCoreAsync<T>(connection, sql, parameters, default, cancellationToken);
    }

    public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, CommandOptions options, CancellationToken cancellationToken = default)
    {
        return QueryScalarCoreAsync<T>(connection, sql, null, options, cancellationToken);
    }

    public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)
    {
        return QueryScalarCoreAsync<T>(connection, sql, parameters, options, cancellationToken);
    }

    internal static async Task<T> QueryScalarCoreAsync<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, CancellationToken cancellationToken)
    {
        return await CommandExecutor.ExecuteReaderAsync(connection, sql, parameters, options.Transaction, options.CommandTimeout, async reader =>
        {
            if (reader is DbDataReader dbReader)
            {
                if (!await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false) || await dbReader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
                    return default!;

                return await dbReader.GetFieldValueAsync<T>(0, cancellationToken).ConfigureAwait(false);
            }

            // Fallback for non-DbDataReader
            if (!reader.Read() || reader.IsDBNull(0)) return default!;
            var obj = reader.GetValue(0);
            return (T)Convert.ChangeType(obj, typeof(T));
        }, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region QueryAsync (Strict Mode)

    public static Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    public static Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    public static Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    public static Task<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }

    #endregion

    #region QueryPartialAsync (Partial/Projection Mode)

    public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, null, default, MappingMode.Projection, cancellationToken);
    }

    public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, parameters, default, MappingMode.Projection, cancellationToken);
    }

    public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, null, options, MappingMode.Projection, cancellationToken);
    }

    public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, parameters, options, MappingMode.Projection, cancellationToken);
    }

    #endregion

    #region Core Async Implementation

    internal static async Task<List<T>> QueryCoreAsync<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode, CancellationToken cancellationToken) where T : new()
    {
        return await CommandExecutor.ExecuteReaderAsync(connection, sql, parameters, options.Transaction, options.CommandTimeout, async reader =>
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

    #endregion
}
