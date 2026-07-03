using System.Data;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Base class for query builders with common fields and utilities.
/// </summary>
internal abstract class QueryBuilderBase
{
    protected readonly IDbConnection _connection;
    protected readonly ISqlDialect _dialect;
    protected readonly List<JoinInfo> _joins = new();
    protected readonly List<WhereCondition> _conditions = new();
    protected readonly List<OrderByColumn> _orderByColumns = new();
    protected readonly ParameterCollection _parameters = new();

    protected QueryBuilderBase(IDbConnection connection, ISqlDialect dialect)
    {
        _connection = connection;
        _dialect = dialect;
    }

    /// <summary>
    /// Gets the table name with optional schema, properly escaped.
    /// </summary>
    protected string GetEscapedTableName(string? schema, string table) =>
        _dialect.EscapeTableName(schema, table);

    /// <summary>
    /// Gets a column name with table prefix, properly escaped.
    /// </summary>
    protected string GetColumnName(EntityMetadata metadata, string propertyName, string? tableAlias)
    {
        var columns = metadata.Columns;
        string columnName = propertyName;

        for (var i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
            {
                columnName = columns[i].ColumnName;
                break;
            }
        }

        var prefix = tableAlias ?? metadata.TableName;
        return $"{prefix}.{_dialect.EscapeColumnName(columnName)}";
    }

    /// <summary>
    /// Gets all column names with table prefix, properly escaped.
    /// </summary>
    protected string[] GetPrefixedColumns(EntityMetadata metadata, string? alias)
    {
        var columns = metadata.Columns;
        var result = new string[columns.Count];
        var prefix = alias ?? metadata.TableName;

        for (var i = 0; i < columns.Count; i++)
        {
            var escaped = _dialect.EscapeColumnName(columns[i].ColumnName);
            result[i] = $"{prefix}.{escaped}";
        }

        return result;
    }

    /// <summary>
    /// Gets all column names with table prefix and alias for SELECT.
    /// </summary>
    protected string[] GetPrefixedColumnsWithAlias(EntityMetadata metadata, string? tableAlias, string columnPrefix)
    {
        var columns = metadata.Columns;
        var result = new string[columns.Count];
        var prefix = tableAlias ?? metadata.TableName;

        for (var i = 0; i < columns.Count; i++)
        {
            var colName = columns[i].ColumnName;
            var escaped = _dialect.EscapeColumnName(colName);
            result[i] = $"{prefix}.{escaped} AS {columnPrefix}{colName}";
        }

        return result;
    }

    /// <summary>
    /// Builds the FROM and JOIN clauses of a SQL query.
    /// </summary>
    protected void BuildFromAndJoinClause(StringBuilder sb, string table, string? schema, string? alias)
    {
        sb.Append(" FROM ");
        sb.Append(_dialect.EscapeTableName(schema, table));

        if (alias is not null)
        {
            sb.Append(' ');
            sb.Append(alias);
        }

        foreach (var join in _joins)
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
    }

    /// <summary>
    /// Builds the WHERE clause of a SQL query.
    /// </summary>
    protected void BuildWhereClause(StringBuilder sb)
    {
        if (_conditions.Count == 0)
            return;

        sb.Append(" WHERE ");
        sb.Append(BuildWhereExpression(_conditions));
    }

    /// <summary>
    /// Folds WHERE conditions left-to-right, wrapping each step in parentheses so the
    /// generated SQL evaluates in the same order the fluent Where/And/Or chain was built,
    /// instead of relying on SQL's AND-before-OR operator precedence.
    /// </summary>
    protected static string BuildWhereExpression(List<WhereCondition> conditions)
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

    /// <summary>
    /// Builds the ORDER BY clause of a SQL query.
    /// </summary>
    protected void BuildOrderByClause(StringBuilder sb)
    {
        if (_orderByColumns.Count == 0)
            return;

        sb.Append(" ORDER BY ");

        for (var i = 0; i < _orderByColumns.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");

            var orderBy = _orderByColumns[i];
            sb.Append(orderBy.ColumnName);

            if (orderBy.Descending)
                sb.Append(" DESC");
        }
    }

    /// <summary>
    /// Binds parameters to a database command.
    /// </summary>
    protected void BindParameters(IDbCommand command) =>
        _parameters.BindTo(command);

    /// <summary>
    /// Gets the parameter count.
    /// </summary>
    protected int ParameterCount => _parameters.Count;
}
