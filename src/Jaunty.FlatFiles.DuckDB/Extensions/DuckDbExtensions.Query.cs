using System.Data;

namespace Jaunty.FlatFiles.DuckDB;

/// <summary>
/// Extension methods that provide Jaunty API parity on <see cref="DuckDb"/> instances.
/// </summary>
public static partial class DuckDbExtensions
{
    /// <inheritdoc cref="Jaunty.Query{T}(IDbConnection, string)"/>
    public static List<T> Query<T>(this DuckDb db, string sql) where T : new()
        => Jaunty.Query<T>(db.Connection, sql);

    /// <inheritdoc cref="Jaunty.Query{T}(IDbConnection, string, object)"/>
    public static List<T> Query<T>(this DuckDb db, string sql, object parameters) where T : new() => Jaunty.Query<T>(db.Connection, sql, parameters);
}
