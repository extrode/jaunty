using System.Linq.Expressions;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public async ValueTask<int> UpdateAsync<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> column, object value, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(column);

        var source = GetSourceOrThrow<T>();
        await TablePromoter.EnsurePromotedToTableAsync(_connection, source, _dialect, cancellationToken).ConfigureAwait(false);

        var columnName = ExpressionTranslator.ResolveColumnName(column);

        var setParam = new DuckDBParameter { Value = value ?? DBNull.Value };
        var (whereSql, whereParams) = ExpressionTranslator.Translate<T>(predicate, paramOffset: 1);

        var allParams = new List<DuckDBParameter>(whereParams.Count + 1) { setParam };
        allParams.AddRange(whereParams);

        var sql = $"UPDATE \"{source.TableName}\" SET \"{columnName}\" = $1 WHERE {whereSql}";
        var result = await NonQueryExecutor.ExecuteAsync(_connection, sql, allParams, cancellationToken).ConfigureAwait(false);

        if (result > 0) _modified.TryAdd(typeof(T), true);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<int> UpdateAsync<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> column, object value, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return await UpdateAsync(predicate, column, value, cancellationToken).ConfigureAwait(false);
    }
}
