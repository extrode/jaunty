using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent;

/// <summary>
/// Builder for the JOIN...ON clause. Implements IJoinClause.
/// </summary>
internal sealed class JoinClauseBuilder<TFrom, TJoin> : IJoinClause<TFrom, TJoin>
    where TFrom : new()
    where TJoin : new()
{
    private readonly QueryBuilder<TFrom> _fromBuilder;
    private readonly JoinType _joinType;
    private readonly string? _joinAlias;
    private readonly EntityMetadata _joinMetadata;

    internal JoinClauseBuilder(QueryBuilder<TFrom> fromBuilder, JoinType joinType, string? joinAlias)
    {
        _fromBuilder = fromBuilder;
        _joinType = joinType;
        _joinAlias = joinAlias;
        _joinMetadata = MetadataCache<TJoin>.Metadata;
    }

    public IJoinedQuery<TFrom, TJoin> On<TLeftKey, TRightKey>(
        Expression<Func<TFrom, TLeftKey>> leftKey,
        Expression<Func<TJoin, TRightKey>> rightKey)
    {
        var leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        var rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        var leftColumn = GetColumnName<TFrom>(leftProp, _fromBuilder.Alias);
        var rightColumn = GetColumnName<TJoin>(rightProp, _joinAlias);

        var condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery(condition);
    }

    public IJoinedQuery<TFrom, TJoin> On(Expression<Func<TFrom, TJoin, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor<TFrom, TJoin>(
            _fromBuilder.Dialect,
            _fromBuilder.Alias,
            _joinAlias);

        var condition = visitor.Translate(predicate);
        return CreateJoinedQuery(condition);
    }

    public IJoinedQuery<TFrom, TJoin> OnColumns(string leftColumn, string rightColumn)
    {
        var condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery(condition);
    }

    public IJoinedQuery<TFrom, TJoin> OnRaw(string condition)
    {
        return CreateJoinedQuery(condition);
    }

    private JoinedQueryBuilder<TFrom, TJoin> CreateJoinedQuery(string onCondition)
    {
        var joinInfo = new JoinInfo(
            _joinType,
            _joinMetadata.TableName,
            _joinMetadata.SchemaName,
            _joinAlias,
            onCondition);

        return new JoinedQueryBuilder<TFrom, TJoin>(
            _fromBuilder.Connection,
            _fromBuilder.Dialect,
            _fromBuilder.TableName,
            _fromBuilder.SchemaName,
            _fromBuilder.Alias,
            joinInfo);
    }

    private string GetColumnName<T>(string propertyName, string? alias) where T : new()
    {
        var metadata = MetadataCache<T>.Metadata;
        var columns = metadata.Columns;

        string columnName = propertyName;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
            {
                columnName = columns[i].ColumnName;
                break;
            }
        }

        var escaped = _fromBuilder.Dialect.EscapeColumnName(columnName);
        var prefix = alias ?? metadata.TableName;
        return $"{prefix}.{escaped}";
    }
}

/// <summary>
/// Information about a single JOIN clause.
/// </summary>
internal readonly struct JoinInfo
{
    public readonly JoinType JoinType;
    public readonly string TableName;
    public readonly string? SchemaName;
    public readonly string? Alias;
    public readonly string OnCondition;

    public JoinInfo(JoinType joinType, string tableName, string? schemaName, string? alias, string onCondition)
    {
        JoinType = joinType;
        TableName = tableName;
        SchemaName = schemaName;
        Alias = alias;
        OnCondition = onCondition;
    }

    public string JoinKeyword => JoinType switch
    {
        JoinType.Inner => "INNER JOIN",
        JoinType.Left => "LEFT JOIN",
        JoinType.Right => "RIGHT JOIN",
        _ => "JOIN"
    };
}
