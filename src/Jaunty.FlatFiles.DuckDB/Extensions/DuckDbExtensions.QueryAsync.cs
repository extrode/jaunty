using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QueryAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<List<T>> QueryAsync<T>(this DuckDb db, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.QueryAsync<T>(db.Connection, sql, parameters, cancellationToken);
}
