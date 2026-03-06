using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.UpdateAsync{T}(IDbConnection, T, CancellationToken)"/>
    public static ValueTask<int> UpdateAsync<T>(this DuckDb db, T entity, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.UpdateAsync(db.Connection, entity, cancellationToken);
}
