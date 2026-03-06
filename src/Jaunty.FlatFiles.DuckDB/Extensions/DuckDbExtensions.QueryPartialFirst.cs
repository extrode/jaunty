using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QueryPartialFirst{T}(IDbConnection, string)"/>
    public static T QueryPartialFirst<T>(this DuckDb db, string sql) where T : new()
        => Jaunty.QueryPartialFirst<T>(db.Connection, sql);

    /// <inheritdoc cref="Jaunty.QueryPartialFirst{T}(IDbConnection, string, object)"/>
    public static T QueryPartialFirst<T>(this DuckDb db, string sql, object parameters) where T : new()
        => Jaunty.QueryPartialFirst<T>(db.Connection, sql, parameters);
}
