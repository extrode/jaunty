using System.Data;

using Jaunty.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, null, default, MappingMode.Projection, null, cancellationToken);
    }

    public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, parameters, default, MappingMode.Projection, null, cancellationToken);
    }

    public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, null, options, MappingMode.Projection, null, cancellationToken);
    }

    public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, parameters, options, MappingMode.Projection, null, cancellationToken);
    }

    public static Task<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, object param1, object param2, params object[] rest) where T : new()
    {
        return QueryCoreAsync<T>(connection, sql, CombineParams(param1, param2, rest), default, MappingMode.Projection, null, default);
    }
}
