using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.Upsert{T}(IDbConnection, T)"/>
    public static int Upsert<T>(this DuckDb db, T entity) where T : new()
        => Jaunty.Upsert(db.Connection, entity);
}
