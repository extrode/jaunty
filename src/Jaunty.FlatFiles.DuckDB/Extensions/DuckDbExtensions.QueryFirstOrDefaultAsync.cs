using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QueryFirstOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T?> QueryFirstOrDefaultAsync<T>(this DuckDb db, string sql, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.QueryFirstOrDefaultAsync<T>(db.Connection, sql, cancellationToken);

    /// <inheritdoc cref="Jaunty.QueryFirstOrDefaultAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<T?> QueryFirstOrDefaultAsync<T>(this DuckDb db, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.QueryFirstOrDefaultAsync<T>(db.Connection, sql, parameters, cancellationToken);
}
