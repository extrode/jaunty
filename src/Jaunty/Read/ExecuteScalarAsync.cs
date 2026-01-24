using System.Data;
using System.Data.Common;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    public static Task<T> ExecuteScalarAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default)
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QueryScalarCoreAsync<T>(dbConnection, sql, null, default, cancellationToken);
    }

    public static Task<T> ExecuteScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default)
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QueryScalarCoreAsync<T>(dbConnection, sql, parameters, default, cancellationToken);
    }

    public static Task<T> ExecuteScalarAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QueryScalarCoreAsync<T>(dbConnection, sql, null, options, cancellationToken);
    }

    public static Task<T> ExecuteScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QueryScalarCoreAsync<T>(dbConnection, sql, parameters, options, cancellationToken);
    }
}
