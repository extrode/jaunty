using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QueryPartialSingleOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T?> QueryPartialSingleOrDefaultAsync<T>(this DuckDb db, string sql, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.QueryPartialSingleOrDefaultAsync<T>(db.Connection, sql, cancellationToken);

    /// <inheritdoc cref="Jaunty.QueryPartialSingleOrDefaultAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<T?> QueryPartialSingleOrDefaultAsync<T>(this DuckDb db, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.QueryPartialSingleOrDefaultAsync<T>(db.Connection, sql, parameters, cancellationToken);
}
