using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.Delete{T}(IDbConnection, T)"/>
    public static int Delete<T>(this DuckDb db, T entity) where T : new()
        => Jaunty.Delete(db.Connection, entity);

    /// <inheritdoc cref="Jaunty.Delete{T}(IDbConnection, object)"/>
    public static int Delete<T>(this DuckDb db, object id) where T : new()
        => Jaunty.Delete<T>(db.Connection, id);
}
