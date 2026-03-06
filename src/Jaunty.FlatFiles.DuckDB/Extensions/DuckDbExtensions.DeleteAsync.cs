using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.DeleteAsync{T}(IDbConnection, T, CancellationToken)"/>
    public static ValueTask<int> DeleteAsync<T>(this DuckDb db, T entity, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.DeleteAsync(db.Connection, entity, cancellationToken);

    /// <inheritdoc cref="Jaunty.DeleteAsync{T}(IDbConnection, object, CancellationToken)"/>
    public static ValueTask<int> DeleteAsync<T>(this DuckDb db, object id, CancellationToken cancellationToken = default) where T : new()
        => Jaunty.DeleteAsync<T>(db.Connection, id, cancellationToken);
}
