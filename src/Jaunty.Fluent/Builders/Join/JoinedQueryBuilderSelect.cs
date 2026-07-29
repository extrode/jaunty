using System.Data;
using System.Runtime.CompilerServices;

using Jaunty.Core;
using Jaunty.Fluent.Internals;
using Jaunty.Configuration;
using Jaunty.Internals.Read;
using Jaunty.Internals;

namespace Jaunty.Fluent;

/// <summary>
/// Select operations for 2-table joins.
/// </summary>
internal partial class JoinedQueryBuilder<TFrom, TJoin>
{
    public List<TFrom> Select()
    {
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = BuildSelectSql(columns);
        return _connection.QueryPartial<TFrom>(sql, _parameters.ToParameterObject()!);
    }

    public List<TFrom> Select(CommandOptions options)
    {
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = BuildSelectSql(columns);
        return _connection.QueryPartial<TFrom>(sql, _parameters.ToParameterObject()!, ToTypedOptions<TFrom>(options));
    }

    public List<T> Select<T>() where T : new()
    {
        if (typeof(T) == typeof(TFrom))
        {
            List<TFrom> result = Select();
            return Unsafe.As<List<TFrom>, List<T>>(ref result);
        }

        if (typeof(T) == typeof(TJoin))
        {
            List<TJoin> result = SelectJoined();
            return Unsafe.As<List<TJoin>, List<T>>(ref result);
        }

        return SelectWithMapping<T>(MappingMode.Strict);
    }

    public List<T> Select<T>(Func<IDataReader, T> mapper) => SelectWithMapper(mapper);

    public List<(TFrom From, TJoin Joined)> SelectBoth() => SelectBothInternal();

    public List<(T1, T2)> Select<T1, T2>()
        where T1 : new()
        where T2 : new()
    {
        if (typeof(T1) != typeof(TFrom))
            throw new ArgumentException($"T1 must be {typeof(TFrom).Name}, got {typeof(T1).Name}", nameof(T1));

        if (typeof(T2) != typeof(TJoin))
            throw new ArgumentException($"T2 must be {typeof(TJoin).Name}, got {typeof(T2).Name}", nameof(T2));

        List<(TFrom From, TJoin Joined)> result = SelectBothInternal();
        return Unsafe.As<List<(TFrom, TJoin)>, List<(T1, T2)>>(ref result);
    }

    public TFrom SelectFirst()
    {
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 1);
        return _connection.QueryPartialFirst<TFrom>(sql, _parameters.ToParameterObject()!);
    }

    public TFrom SelectFirst(CommandOptions options)
    {
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 1);
        return _connection.QueryPartialFirst<TFrom>(sql, _parameters.ToParameterObject()!, ToTypedOptions<TFrom>(options));
    }

    public TFrom? SelectFirstOrDefault()
    {
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 1);
        return _connection.QueryPartialFirstOrDefault<TFrom>(sql, _parameters.ToParameterObject()!);
    }

    public TFrom? SelectFirstOrDefault(CommandOptions options)
    {
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 1);
        return _connection.QueryPartialFirstOrDefault<TFrom>(sql, _parameters.ToParameterObject()!, ToTypedOptions<TFrom>(options));
    }

    public TFrom SelectSingle()
    {
        // AUD-R26-057: LIMIT 2, not the whole result set. QueryPartialSingle throws when a second
        // row exists, so two rows is all it takes to make that decision - reading the rest only to
        // discard it is pure waste, and on a large join SelectSingle read the entire result set to
        // discover it should have thrown. Mirrors QueryBuilder.SelectSingle, which sets _take = 2
        // for exactly this reason, and the SelectFirst neighbours four lines above which already
        // page to 1.
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 2);
        return _connection.QueryPartialSingle<TFrom>(sql, _parameters.ToParameterObject()!);
    }

    public TFrom SelectSingle(CommandOptions options)
    {
        // AUD-R26-057: LIMIT 2, not the whole result set. QueryPartialSingle throws when a second
        // row exists, so two rows is all it takes to make that decision - reading the rest only to
        // discard it is pure waste, and on a large join SelectSingle read the entire result set to
        // discover it should have thrown. Mirrors QueryBuilder.SelectSingle, which sets _take = 2
        // for exactly this reason, and the SelectFirst neighbours four lines above which already
        // page to 1.
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 2);
        return _connection.QueryPartialSingle<TFrom>(sql, _parameters.ToParameterObject()!, ToTypedOptions<TFrom>(options));
    }

    public TFrom? SelectSingleOrDefault()
    {
        // AUD-R26-057: LIMIT 2, not the whole result set. QueryPartialSingle throws when a second
        // row exists, so two rows is all it takes to make that decision - reading the rest only to
        // discard it is pure waste, and on a large join SelectSingle read the entire result set to
        // discover it should have thrown. Mirrors QueryBuilder.SelectSingle, which sets _take = 2
        // for exactly this reason, and the SelectFirst neighbours four lines above which already
        // page to 1.
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 2);
        return _connection.QueryPartialSingleOrDefault<TFrom>(sql, _parameters.ToParameterObject()!);
    }

    public TFrom? SelectSingleOrDefault(CommandOptions options)
    {
        // AUD-R26-057: LIMIT 2, not the whole result set. QueryPartialSingle throws when a second
        // row exists, so two rows is all it takes to make that decision - reading the rest only to
        // discard it is pure waste, and on a large join SelectSingle read the entire result set to
        // discover it should have thrown. Mirrors QueryBuilder.SelectSingle, which sets _take = 2
        // for exactly this reason, and the SelectFirst neighbours four lines above which already
        // page to 1.
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 2);
        return _connection.QueryPartialSingleOrDefault<TFrom>(sql, _parameters.ToParameterObject()!, ToTypedOptions<TFrom>(options));
    }

    public T SelectFirst<T>() where T : new()
    {
        if (typeof(T) == typeof(TFrom))
        {
            TFrom? result = SelectFirst();
            return Unsafe.As<TFrom, T>(ref result);
        }

        if (typeof(T) == typeof(TJoin))
        {
            TJoin? result = SelectFirstJoined();
            return Unsafe.As<TJoin, T>(ref result);
        }

        List<T> results = SelectWithMapping<T>(MappingMode.Strict, limit: 1);
        if (results.Count == 0)
            throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");

        return results[0];
    }

    public T SelectFirst<T>(Func<IDataReader, T> mapper)
    {
        List<T> results = SelectWithMapper(mapper, limit: 1);
        if (results.Count == 0)
            throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");

        return results[0];
    }

    public T? SelectFirstOrDefault<T>() where T : new()
    {
        if (typeof(T) == typeof(TFrom))
        {
            TFrom? result = SelectFirstOrDefault();
            return Unsafe.As<TFrom?, T?>(ref result);
        }

        if (typeof(T) == typeof(TJoin))
        {
            TJoin? result = SelectFirstOrDefaultJoined();
            return Unsafe.As<TJoin?, T?>(ref result);
        }

        List<T> results = SelectWithMapping<T>(MappingMode.Strict, limit: 1);
        return results.Count > 0 ? results[0] : default;
    }

    public T? SelectFirstOrDefault<T>(Func<IDataReader, T> mapper)
    {
        List<T> results = SelectWithMapper(mapper, limit: 1);
        return results.Count > 0 ? results[0] : default;
    }

    public (TFrom From, TJoin Joined) SelectFirstBoth()
    {
        List<(TFrom From, TJoin Joined)> result = SelectBothInternal(limit: 1);
        if (result.Count == 0)
            throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(TFrom).Name}, {typeof(TJoin).Name})'.");

        return result[0];
    }

    public int Count()
    {
        string sql = BuildCountSql();
        return _connection.QueryScalar<int>(sql, _parameters.ToParameterObject()!);
    }

    public int Count(CommandOptions options)
    {
        string sql = BuildCountSql();
        return _connection.QueryScalar<int>(sql, _parameters.ToParameterObject()!, ToTypedOptions<int>(options));
    }

    public long LongCount()
    {
        string sql = BuildCountSql();
        return _connection.QueryScalar<long>(sql, _parameters.ToParameterObject()!);
    }

    public long LongCount(CommandOptions options)
    {
        string sql = BuildCountSql();
        return _connection.QueryScalar<long>(sql, _parameters.ToParameterObject()!, ToTypedOptions<long>(options));
    }

    public string ToSql()
    {
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        return BuildSelectSql(columns);
    }

    private List<TJoin> SelectJoined()
    {
        string[] columns = GetPrefixedColumns(_joinMetadata, _joins[0].Alias);
        string sql = BuildSelectSql(columns);
        return _connection.QueryPartial<TJoin>(sql, _parameters.ToParameterObject()!);
    }

    private TJoin SelectFirstJoined()
    {
        string[] columns = GetPrefixedColumns(_joinMetadata, _joins[0].Alias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 1);
        return _connection.QueryPartialFirst<TJoin>(sql, _parameters.ToParameterObject()!);
    }

    private TJoin? SelectFirstOrDefaultJoined()
    {
        string[] columns = GetPrefixedColumns(_joinMetadata, _joins[0].Alias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 1);
        return _connection.QueryPartialFirstOrDefault<TJoin>(sql, _parameters.ToParameterObject()!);
    }

    /// <summary>
    /// Materialises the joined rows, optionally bounded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26-057: the limit did not exist, so SelectFirstBoth called this and indexed [0] -
    /// materialising and mapping every joined row to return one. SelectWithMapper already took a
    /// limit for the same job; this is the sibling that did not.
    /// </para>
    /// <para>
    /// AUD-R26-060, knowingly left open here: this helper and its two neighbours
    /// (<c>SelectWithMapping</c>, <c>SelectWithMapper</c>) build and execute their own command, so
    /// a caller's transaction and timeout cannot reach it - the terminals that route through them
    /// (<c>SelectBoth</c>, <c>Select&lt;T&gt;</c>, <c>Select&lt;T&gt;(mapper)</c>, the
    /// <c>SelectFirst</c>/<c>SelectFirstOrDefault</c> mapper overloads and <c>SelectFirstBoth</c>)
    /// carry no <see cref="CommandOptions"/> overload. The neighbours that delegate to core
    /// (<c>Select</c>, <c>SelectFirst</c>, <c>SelectSingle</c>, <c>Count</c>, <c>LongCount</c>) all
    /// do. Closing this uniformly means the same set of overloads on arities 2, 3 and 4, sync and
    /// async, on both <c>IJoinedQuery</c> and the partial-select family - roughly sixty new public
    /// methods. Doing arity 2 alone would relocate the inconsistency rather than remove it, so this
    /// is a public-API decision rather than a defect fix and is carried forward deliberately. The
    /// two places that had no options path at all - <c>InsertBuilder</c> and
    /// <c>GroupedQueryBuilder</c> - were fixed under AUD-R26-060; see <c>FluentCommandOptions</c>.
    /// </para>
    /// </remarks>
    private List<(TFrom From, TJoin Joined)> SelectBothInternal(int? limit = null)
    {
        string[] fromColumns = GetPrefixedColumnsWithAlias(_fromMetadata, _fromAlias, "f_");
        string[] joinColumns = GetPrefixedColumnsWithAlias(_joinMetadata, _joins[0].Alias, "j_");
        string[] allColumns = fromColumns.Concat(joinColumns).ToArray();

        string sql = BuildSelectSql(allColumns);
        if (limit.HasValue) sql = _dialect.GetPagingSql(sql, 0, limit.Value);

        return CommandObservation.Execute(
            sql, DescribeParameters(), _connection, CommandType.Text, Body);

        List<(TFrom From, TJoin Joined)> Body()
        {
            var results = new List<(TFrom, TJoin)>();

            using IDbCommand command = _connection.CreateCommand();
            command.CommandText = sql;
            BindParameters(command);

            CommandObservation.Log(sql, DescribeParameters());

            bool wasClosed = _connection.State == ConnectionState.Closed;
            if (wasClosed)
                _connection.Open();

            try
            {
                using IDataReader reader = command.ExecuteReader();
                Dictionary<string, int> ordinals = BuildOrdinalLookup(reader);

                while (reader.Read())
                {
                    TFrom? fromObj = MapEntity<TFrom>(_fromMetadata, reader, "f_", ordinals);
                    TJoin? joinObj = MapEntity<TJoin>(_joinMetadata, reader, "j_", ordinals);
                    results.Add((fromObj, joinObj));
                }
            }
            finally
            {
                if (wasClosed)
                    _connection.Close();
            }

            return results;
        }
    }

    private List<T> SelectWithMapping<T>(MappingMode mode, int? limit = null)
        where T : new()
    {
        string sql = BuildSelectPartialSql("*");

        return CommandObservation.Execute(
            sql, DescribeParameters(), _connection, CommandType.Text, Body);

        List<T> Body()
        {
            if (limit.HasValue)
                sql = _dialect.GetPagingSql(sql, 0, limit.Value);

            var results = new List<T>();

            using IDbCommand command = _connection.CreateCommand();
            command.CommandText = sql;
            BindParameters(command);

            CommandObservation.Log(sql, DescribeParameters());

            bool wasClosed = _connection.State == ConnectionState.Closed;
            if (wasClosed)
                _connection.Open();

            try
            {
                using IDataReader reader = command.ExecuteReader();
                EnsureNoAmbiguousColumns(reader);
                Func<IDataReader, T> mapper = DrDispatcher.Resolve<T>(reader, default, mode);

                while (reader.Read())
                    results.Add(mapper(reader));
            }
            finally
            {
                if (wasClosed)
                    _connection.Close();
            }

            return results;
        }
    }

    private static CommandOptions<TResult> ToTypedOptions<TResult>(CommandOptions options) =>
        new(transaction: options.Transaction, commandTimeout: options.CommandTimeout, commandType: options.CommandType);

    private static void EnsureNoAmbiguousColumns(IDataReader reader)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < reader.FieldCount; i++)
        {
            string name = reader.GetName(i);
            if (!seen.Add(name))
                throw new InvalidOperationException(
                    $"Column '{name}' is ambiguous: it appears more than once in the joined result set. " +
                    "Use SelectPartial with aliased columns, or Select(Func<IDataReader, T> mapper), to disambiguate.");
        }
    }

    private List<T> SelectWithMapper<T>(Func<IDataReader, T> mapper, int? limit = null)
    {
        string sql = BuildSelectPartialSql("*");

        return CommandObservation.Execute(
            sql, DescribeParameters(), _connection, CommandType.Text, Body);

        List<T> Body()
        {
            if (limit.HasValue)
                sql = _dialect.GetPagingSql(sql, 0, limit.Value);

            var results = new List<T>();

            using IDbCommand command = _connection.CreateCommand();
            command.CommandText = sql;
            BindParameters(command);

            CommandObservation.Log(sql, DescribeParameters());

            bool wasClosed = _connection.State == ConnectionState.Closed;
            if (wasClosed)
                _connection.Open();

            try
            {
                using IDataReader reader = command.ExecuteReader();

                while (reader.Read())
                    results.Add(mapper(reader));
            }
            finally
            {
                if (wasClosed)
                    _connection.Close();
            }

            return results;
        }
    }
}
