using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QueryPartialAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<List<T>> QueryPartialAsync<T>(this DuckDb db, string sql, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.QueryPartialAsync<T>(db.Connection, sql, cancellationToken);

    /// <inheritdoc cref="Jaunty.QueryPartialAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<List<T>> QueryPartialAsync<T>(this DuckDb db, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.QueryPartialAsync<T>(db.Connection, sql, parameters, cancellationToken);
}
