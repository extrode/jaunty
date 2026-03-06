using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QueryScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T> QueryScalarAsync<T>(this DuckDb db, string sql, CancellationToken cancellationToken = default)
        => Jaunty.QueryScalarAsync<T>(db.Connection, sql, cancellationToken);

    /// <inheritdoc cref="Jaunty.QueryScalarAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<T> QueryScalarAsync<T>(this DuckDb db, string sql, object parameters, CancellationToken cancellationToken = default)
        => Jaunty.QueryScalarAsync<T>(db.Connection, sql, parameters, cancellationToken);
}
