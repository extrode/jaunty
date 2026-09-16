using System.Linq.Expressions;

using Extrode.Jaunty.Core;
using Extrode.Jaunty.FlatFiles.DuckDB.Internals;
using Extrode.Jaunty.FlatFiles.Interfaces;

namespace Extrode.Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public int Delete<T>(Expression<Func<T, bool>> predicate) where T : class, new()
    {
        return DeleteCore(predicate, default);
    }

    /// <inheritdoc />
    public int Delete<T>(Expression<Func<T, bool>> predicate, CommandOptions options) where T : class, new()
    {
        return DeleteCore(predicate, options);
    }

    private int DeleteCore<T>(Expression<Func<T, bool>> predicate, CommandOptions options) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(predicate);

        IFileSource source = GetSourceOrThrow<T>();
        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);

        (string whereSql, List<global::DuckDB.NET.Data.DuckDBParameter> whereParams) = ExpressionTranslator.Translate<T>(predicate);

        var sql = $"DELETE FROM {_dialect.EscapeTableName(null, source.TableName)} WHERE {whereSql}";
        var result = NonQueryExecutor.Execute(_connection, sql, whereParams, options);

        if (result > 0) _modified.TryAdd(typeof(T), true);
        return result;
    }
}