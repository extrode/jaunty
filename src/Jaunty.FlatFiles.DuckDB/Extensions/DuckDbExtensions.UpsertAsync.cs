using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.UpsertAsync{T}(IDbConnection, T, CancellationToken)"/>
    public static ValueTask<int> UpsertAsync<T>(this DuckDb db, T entity, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.UpsertAsync(db.Connection, entity, cancellationToken);
}
