using System.Linq.Expressions;

using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Configuration;

namespace Jaunty.Fluent;

/// <summary>
/// Where clause operations for 2-table joins.
/// </summary>
internal partial class JoinedQueryBuilder<TFrom, TJoin>
{
    public IJoinedQuery<TFrom, TJoin> Where(Expression<Func<TFrom, TJoin, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor<TFrom, TJoin>(
            _dialect,
            _fromAlias,
            _joins[0].Alias);

        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        AddWhereExpression(sql, parameters, LogicalOperator.None);
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> Where(string condition)
    {
        _conditions.Add(WhereCondition.Raw(condition, LogicalOperator.None));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> Where(string column, object value)
    {
        // AUD-R35-014: uniquified and sanitized, so filtering one column twice is a range filter
        // rather than a duplicate-parameter throw. See ParameterCollection.CreateUniqueName.
        string paramName = _parameters.CreateUniqueName(_dialect.ParameterPrefix, column);
        string escapedColumn = EscapeQualifiedColumn(column);
        _conditions.Add(WhereCondition.Column($"{escapedColumn} = {paramName}", LogicalOperator.None));
        _parameters.Add(paramName, value);
        return this;
    }

    // The alias prefix (if any) is validated as a plain identifier - not dialect-escaped, since
    // aliases are library-controlled bare names in the generated SQL, not user data - so a caller
    // can't smuggle arbitrary SQL text through the alias segment while the column name is escaped.
    private string EscapeQualifiedColumn(string column)
    {
        int dotIndex = column.IndexOf('.');
        if (dotIndex < 0)
            return _dialect.EscapeColumnName(column);

        string alias = column.Substring(0, dotIndex);
        string columnName = column.Substring(dotIndex + 1);
        SqlIdentifierValidator.Validate(alias, nameof(column));
        return $"{alias}.{_dialect.EscapeColumnName(columnName)}";
    }

    public IJoinedQuery<TFrom, TJoin> And(Expression<Func<TFrom, TJoin, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor<TFrom, TJoin>(_dialect, _fromAlias, _joins[0].Alias);
        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        AddWhereExpression(sql, parameters, LogicalOperator.And);
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> Or(Expression<Func<TFrom, TJoin, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor<TFrom, TJoin>(_dialect, _fromAlias, _joins[0].Alias);
        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        AddWhereExpression(sql, parameters, LogicalOperator.Or);
        return this;
    }
}
