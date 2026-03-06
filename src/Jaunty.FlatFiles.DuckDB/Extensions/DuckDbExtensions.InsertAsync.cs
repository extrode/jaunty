using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.InsertAsync{T}(IDbConnection, T, CancellationToken)"/>
    public static ValueTask<long> InsertAsync<T>(this DuckDb db, T entity, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.InsertAsync(db.Connection, entity, cancellationToken);
}
