using System.Data;
using System.Text;
using System.Text.RegularExpressions;

using Jaunty.Dialects;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent;

/// <summary>
/// Query builder for 2-table joins.
/// </summary>
internal sealed partial class JoinedQueryBuilder<TFrom, TJoin> : IJoinedQuery<TFrom, TJoin>
    where TFrom : new()
    where TJoin : new()
{
    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly string _fromTable;
    private readonly string? _fromSchema;
    private readonly string? _fromAlias;
    private readonly List<JoinInfo> _joins = [];
    private readonly List<WhereCondition> _conditions = [];
    private readonly List<OrderByColumn> _orderByColumns = [];
    private readonly ParameterCollection _parameters = new();
    private int _paramSeq;
    private readonly EntityMetadata _fromMetadata;
    private readonly EntityMetadata _joinMetadata;

    internal JoinedQueryBuilder(
        IDbConnection connection,
        ISqlDialect dialect,
        string fromTable,
        string? fromSchema,
        string? fromAlias,
        JoinInfo firstJoin)
    {
        _connection = connection;
        _dialect = dialect;
        _fromTable = fromTable;
        _fromSchema = fromSchema;
        _fromAlias = fromAlias;
        _joins.Add(firstJoin);
        _fromMetadata = FluentMetadataCache.GetMetadata<TFrom>();
        _joinMetadata = FluentMetadataCache.GetMetadata<TJoin>();
    }

    internal string? FromAlias => _fromAlias;
    internal string FromTable => _fromTable;
    internal string? FromSchema => _fromSchema;
    internal ISqlDialect Dialect => _dialect;
    internal IDbConnection Connection => _connection;
    internal List<JoinInfo> Joins => _joins;
    internal List<WhereCondition> Conditions => _conditions;
    internal List<OrderByColumn> GetOrderByColumns() => _orderByColumns;
    internal ParameterCollection GetParameters() => _parameters;

    internal void AddOrderByColumn(string columnName, string direction, bool isFirst)
    {
        _orderByColumns.Add(new OrderByColumn(columnName, direction == "DESC"));
    }

    internal string BuildCountSql()
    {
        var sb = new StringBuilder(128);
        sb.Append("SELECT COUNT(*) FROM ");
        sb.Append(_dialect.EscapeTableName(_fromSchema, _fromTable));

        if (_fromAlias is not null)
        {
            sb.Append(' ');
            sb.Append(_fromAlias);
        }

        foreach (JoinInfo join in _joins)
        {
            sb.Append(' ');
            sb.Append(join.JoinKeyword);
            sb.Append(' ');
            sb.Append(_dialect.EscapeTableName(join.SchemaName, join.TableName));

            if (join.Alias is not null)
            {
                sb.Append(' ');
                sb.Append(join.Alias);
            }

            sb.Append(" ON ");
            sb.Append(join.OnCondition);
        }

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        return sb.ToString();
    }

    internal string BuildSelectPartialSql(string columns)
    {
        var sb = new StringBuilder(256);
        sb.Append("SELECT ");
        sb.Append(columns);
        sb.Append(" FROM ");
        sb.Append(_dialect.EscapeTableName(_fromSchema, _fromTable));

        if (_fromAlias is not null)
        {
            sb.Append(' ');
            sb.Append(_fromAlias);
        }

        foreach (JoinInfo join in _joins)
        {
            sb.Append(' ');
            sb.Append(join.JoinKeyword);
            sb.Append(' ');
            sb.Append(_dialect.EscapeTableName(join.SchemaName, join.TableName));

            if (join.Alias is not null)
            {
                sb.Append(' ');
                sb.Append(join.Alias);
            }

            sb.Append(" ON ");
            sb.Append(join.OnCondition);
        }

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        if (_orderByColumns.Count > 0)
        {
            sb.Append(" ORDER BY ");

            for (var i = 0; i < _orderByColumns.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                OrderByColumn orderBy = _orderByColumns[i];
                sb.Append(orderBy.ColumnName);

                if (orderBy.Descending)
                    sb.Append(" DESC");
            }
        }

        return sb.ToString();
    }

    // FROM/JOIN/WHERE fragment shared with grouped-joined queries (spec 004's state-reuse
    // seam), which append GROUP BY/HAVING instead of the plain column SELECT/ORDER BY that
    // BuildSelectSql/BuildCountSql/BuildSelectPartialSql produce.
    internal string BuildFromJoinWhereSql()
    {
        var sb = new StringBuilder(256);
        sb.Append(_dialect.EscapeTableName(_fromSchema, _fromTable));

        if (_fromAlias is not null)
        {
            sb.Append(' ');
            sb.Append(_fromAlias);
        }

        foreach (JoinInfo join in _joins)
        {
            sb.Append(' ');
            sb.Append(join.JoinKeyword);
            sb.Append(' ');
            sb.Append(_dialect.EscapeTableName(join.SchemaName, join.TableName));

            if (join.Alias is not null)
            {
                sb.Append(' ');
                sb.Append(join.Alias);
            }

            sb.Append(" ON ");
            sb.Append(join.OnCondition);
        }

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        return sb.ToString();
    }

    internal string[] GetSelectColumns<T>() where T : new()
    {
        var metadata = FluentMetadataCache.GetMetadata<T>();
        return GetPrefixedColumns(metadata, _fromAlias);
    }

    public IJoinClause<TFrom, TJoin, T3> InnerJoin<T3>(string? alias = null) where T3 : new()
        => new JoinClause3Builder<TFrom, TJoin, T3>(this, JoinType.Inner, alias);

    public IJoinClause<TFrom, TJoin, T3> LeftJoin<T3>(string? alias = null) where T3 : new()
        => new JoinClause3Builder<TFrom, TJoin, T3>(this, JoinType.Left, alias);

    internal void AddJoin(JoinInfo join) => _joins.Add(join);

    internal void AddWhereCondition(WhereCondition condition) => _conditions.Add(condition);

    /// <summary>
    /// Adds a WHERE condition produced by a join-predicate expression visitor, renumbering
    /// its positional parameter names (e.g. "jp0", "jp1") against a running counter shared by
    /// the whole query. Each Where/And/Or call uses a fresh visitor whose parameter index resets
    /// to 0, so without renumbering, separately-translated conditions on the same query can
    /// collide on identical parameter names (e.g. two conditions each producing "@jp0"), silently
    /// dropping/overwriting one of the bound values.
    /// </summary>
    internal void AddWhereExpression(string sql, List<(string Name, object? Value)> parameters, LogicalOperator op)
    {
        string finalSql = sql;

        for (int i = 0; i < parameters.Count; i++)
        {
            (string oldName, object? value) = parameters[i];
            string newName = $"{_dialect.ParameterPrefix}jp{_paramSeq++}";

            if (newName != oldName)
                finalSql = Regex.Replace(finalSql, Regex.Escape(oldName) + @"(?!\w)", m => newName);

            _parameters.Add(newName, value);
        }

        _conditions.Add(WhereCondition.Expression(finalSql, op));
    }

    /// <summary>
    /// Folds WHERE conditions left-to-right, wrapping each step in parentheses so the
    /// generated SQL evaluates in the same order the fluent Where/And/Or chain was built,
    /// instead of relying on SQL's AND-before-OR operator precedence.
    /// </summary>
    private static string BuildWhereExpression(List<WhereCondition> conditions)
    {
        var expr = conditions[0].Sql;

        for (var i = 1; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            var op = condition.Operator == LogicalOperator.Or ? "OR" : "AND";
            expr = $"({expr} {op} {condition.Sql})";
        }

        return expr;
    }

    internal void AddParameter<TValue>(string name, TValue value) =>
        _parameters.Add(name, value);

    internal void BindParameters(IDbCommand command) =>
        _parameters.BindTo(command);

    internal string[] GetPrefixedColumns(EntityMetadata metadata, string? alias)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        var result = new string[columns.Count];
        string prefix = alias ?? metadata.TableName;

        for (int i = 0; i < columns.Count; i++)
        {
            string escaped = _dialect.EscapeColumnName(columns[i].ColumnName);
            result[i] = $"{prefix}.{escaped}";
        }

        return result;
    }

    internal string[] GetPrefixedColumnsWithAlias(EntityMetadata metadata, string? tableAlias, string columnPrefix)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        var result = new string[columns.Count];
        string prefix = tableAlias ?? metadata.TableName;

        for (int i = 0; i < columns.Count; i++)
        {
            string colName = columns[i].ColumnName;
            string escaped = _dialect.EscapeColumnName(colName);
            result[i] = $"{prefix}.{escaped} AS {columnPrefix}{colName}";
        }

        return result;
    }

    internal string BuildSelectSql(string[] columns)
    {
        var sb = new StringBuilder(256);
        sb.Append("SELECT ");

        for (var i = 0; i < columns.Length; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(columns[i]);
        }

        sb.Append(" FROM ");
        sb.Append(_dialect.EscapeTableName(_fromSchema, _fromTable));

        if (_fromAlias is not null)
        {
            sb.Append(' ');
            sb.Append(_fromAlias);
        }

        foreach (JoinInfo join in _joins)
        {
            sb.Append(' ');
            sb.Append(join.JoinKeyword);
            sb.Append(' ');
            sb.Append(_dialect.EscapeTableName(join.SchemaName, join.TableName));

            if (join.Alias is not null)
            {
                sb.Append(' ');
                sb.Append(join.Alias);
            }

            sb.Append(" ON ");
            sb.Append(join.OnCondition);
        }

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        if (_orderByColumns.Count > 0)
        {
            sb.Append(" ORDER BY ");

            for (var i = 0; i < _orderByColumns.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                OrderByColumn orderBy = _orderByColumns[i];
                sb.Append(orderBy.ColumnName);

                if (orderBy.Descending)
                    sb.Append(" DESC");
            }
        }

        return sb.ToString();
    }

    internal static TEntity MapEntity<TEntity>(EntityMetadata metadata, IDataReader reader, string prefix)
        where TEntity : new()
    {
        var entity = new TEntity();
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        Dictionary<string, int> ordinals = BuildOrdinalLookup(reader);

        for (int i = 0; i < columns.Count; i++)
        {
            ColumnMetadata col = columns[i];
            string aliasName = $"{prefix}{col.ColumnName}";

            if (!ordinals.TryGetValue(aliasName, out int ordinal))
                continue; // Column not found, skip

            if (reader.IsDBNull(ordinal))
                continue;

            object value = reader.GetValue(ordinal);
            Type propertyType = col.PropertyType;
            Type targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            object convertedValue = GroupedJoinedResultMapper.ConvertColumnValue(value, targetType);

            if (col.Setter is { } setter)
                setter(entity!, convertedValue);
            else
                col.Property!.SetValue(entity, convertedValue);
        }

        return entity;
    }

    /// <summary>
    /// Builds a per-call ordinal lookup for the reader's current column set, avoiding the
    /// exception-driven IndexOutOfRangeException-per-missing-column pattern that
    /// <see cref="IDataRecord.GetOrdinal(string)"/> relies on when a column isn't present.
    /// </summary>
    private static Dictionary<string, int> BuildOrdinalLookup(IDataReader reader)
    {
        var map = new Dictionary<string, int>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < reader.FieldCount; i++)
        {
            string name = reader.GetName(i);
            if (!map.ContainsKey(name))
                map[name] = i;
        }

        return map;
    }
}
