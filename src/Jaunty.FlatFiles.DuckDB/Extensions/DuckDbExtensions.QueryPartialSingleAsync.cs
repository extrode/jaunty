using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QueryPartialSingleAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T> QueryPartialSingleAsync<T>(this DuckDb db, string sql, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.QueryPartialSingleAsync<T>(db.Connection, sql, cancellationToken);

    /// <inheritdoc cref="Jaunty.QueryPartialSingleAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<T> QueryPartialSingleAsync<T>(this DuckDb db, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.QueryPartialSingleAsync<T>(db.Connection, sql, parameters, cancellationToken);
}
