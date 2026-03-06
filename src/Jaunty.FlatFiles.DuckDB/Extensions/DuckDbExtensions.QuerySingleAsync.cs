using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QuerySingleAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T> QuerySingleAsync<T>(this DuckDb db, string sql, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.QuerySingleAsync<T>(db.Connection, sql, cancellationToken);

    /// <inheritdoc cref="Jaunty.QuerySingleAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<T> QuerySingleAsync<T>(this DuckDb db, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.QuerySingleAsync<T>(db.Connection, sql, parameters, cancellationToken);
}
