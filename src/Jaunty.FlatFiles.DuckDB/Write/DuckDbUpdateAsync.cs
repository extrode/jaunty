using System.Linq.Expressions;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public async ValueTask<int> UpdateAsync<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> column, object value, CancellationToken cancellationToken = default) where T : class, new()
    {
        return await UpdateCoreAsync(predicate, column, value, default, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<int> UpdateAsync<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> column, object value, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return await UpdateCoreAsync(predicate, column, value, options, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> UpdateCoreAsync<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> column, object value, CommandOptions options, CancellationToken cancellationToken) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(column);

        IFileSource source = GetSourceOrThrow<T>();
        await TablePromoter.EnsurePromotedToTableAsync(_connection, source, _dialect, cancellationToken).ConfigureAwait(false);

        var columnName = ExpressionTranslator.ResolveColumnName(column);

        var setParam = new DuckDBParameter { Value = value ?? DBNull.Value };
        (string whereSql, List<DuckDBParameter> whereParams) = ExpressionTranslator.Translate<T>(predicate, paramOffset: 1);

        var allParams = new List<DuckDBParameter>(whereParams.Count + 1) { setParam };
        allParams.AddRange(whereParams);

        var sql = $"UPDATE {_dialect.EscapeTableName(null, source.TableName)} SET {_dialect.EscapeColumnName(columnName)} = $1 WHERE {whereSql}";
        var result = await NonQueryExecutor.ExecuteAsync(_connection, sql, allParams, options, cancellationToken).ConfigureAwait(false);

        if (result > 0) _modified.TryAdd(typeof(T), true);
        return result;
    }
}