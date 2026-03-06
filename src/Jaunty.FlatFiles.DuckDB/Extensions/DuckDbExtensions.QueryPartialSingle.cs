using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.QueryPartialSingle{T}(IDbConnection, string)"/>
    public static T QueryPartialSingle<T>(this DuckDb db, string sql) where T : new()
        => Jaunty.QueryPartialSingle<T>(db.Connection, sql);

    /// <inheritdoc cref="Jaunty.QueryPartialSingle{T}(IDbConnection, string, object)"/>
    public static T QueryPartialSingle<T>(this DuckDb db, string sql, object parameters) where T : new()
        => Jaunty.QueryPartialSingle<T>(db.Connection, sql, parameters);
}
