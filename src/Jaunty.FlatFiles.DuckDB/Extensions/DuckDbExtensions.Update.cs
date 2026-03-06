using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.Update{T}(IDbConnection, T)"/>
    public static int Update<T>(this DuckDb db, T entity) where T : new()
        => Jaunty.Update(db.Connection, entity);
}
