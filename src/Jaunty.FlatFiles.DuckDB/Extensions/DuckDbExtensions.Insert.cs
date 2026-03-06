using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.Insert{T}(IDbConnection, T)"/>
    public static long Insert<T>(this DuckDb db, T entity) where T : new()
        => Jaunty.Insert(db.Connection, entity);
}
