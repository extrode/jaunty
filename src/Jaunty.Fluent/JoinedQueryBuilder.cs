using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Enums;
using Jaunty.Internals.Read;

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

    public IJoinClause<TFrom, TJoin, T3> InnerJoin<T3>(string? alias = null)
        where T3 : new() =>
        new JoinClause3Builder<TFrom, TJoin, T3>(this, JoinType.Inner, alias);

    public IJoinClause<TFrom, TJoin, T3> LeftJoin<T3>(string? alias = null)
        where T3 : new() =>
        new JoinClause3Builder<TFrom, TJoin, T3>(this, JoinType.Left, alias);

    internal void AddJoin(JoinInfo join) => _joins.Add(join);

    internal void AddWhereCondition(WhereCondition condition) => _conditions.Add(condition);

    internal void AddParameter<TValue>(string name, TValue value) =>
        _parameters.Add(name, value);

    internal void BindParameters(IDbCommand command) =>
        _parameters.BindTo(command);

    internal string[] GetPrefixedColumns(EntityMetadata metadata, string? alias)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        var result = new string[columns.Count];
        string prefix = alias ?? metadata.TableName;

        for (var i = 0; i < columns.Count; i++)
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

        for (var i = 0; i < columns.Count; i++)
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

            for (var i = 0; i < _conditions.Count; i++)
            {
                WhereCondition condition = _conditions[i];
                if (i > 0)
                    sb.Append(condition.Operator == LogicalOperator.Or ? " OR " : " AND ");
                sb.Append(condition.Sql);
            }
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

        for (var i = 0; i < columns.Count; i++)
        {
            ColumnMetadata col = columns[i];
            string aliasName = $"{prefix}{col.ColumnName}";

            try
            {
                var ordinal = reader.GetOrdinal(aliasName);
                if (!reader.IsDBNull(ordinal))
                {
                    object? value = reader.GetValue(ordinal);
                    Type propertyType = col.Property.PropertyType;
                    Type targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
                    object? convertedValue = Convert.ChangeType(value, targetType);
                    col.Property.SetValue(entity, convertedValue);
                }
            }
            catch (IndexOutOfRangeException)
            {
                // Column not found, skip
            }
        }

        return entity;
    }
}
