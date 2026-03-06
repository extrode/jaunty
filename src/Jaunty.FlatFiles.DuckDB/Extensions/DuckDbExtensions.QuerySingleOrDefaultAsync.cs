using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QuerySingleOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T?> QuerySingleOrDefaultAsync<T>(this DuckDb db, string sql, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.QuerySingleOrDefaultAsync<T>(db.Connection, sql, cancellationToken);

    /// <inheritdoc cref="Jaunty.QuerySingleOrDefaultAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<T?> QuerySingleOrDefaultAsync<T>(this DuckDb db, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.QuerySingleOrDefaultAsync<T>(db.Connection, sql, parameters, cancellationToken);
}
