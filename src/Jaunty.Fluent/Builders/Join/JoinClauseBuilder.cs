using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent;

/// <summary>
/// Builder for the JOIN...ON clause. Implements IJoinClause.
/// </summary>
internal sealed class JoinClauseBuilder<TFrom, TJoin> : IJoinClause<TFrom, TJoin> where TFrom : new() where TJoin : new()
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
        _joinMetadata = FluentMetadataCache.GetMetadata<TJoin>();
    }

    public IJoinedQuery<TFrom, TJoin> On<TLeftKey, TRightKey>(Expression<Func<TFrom, TLeftKey>> leftKey, Expression<Func<TJoin, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName<TFrom>(leftProp, _fromBuilder.Alias);
        string rightColumn = GetColumnName<TJoin>(rightProp, _joinAlias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery(condition);
    }

    public IJoinedQuery<TFrom, TJoin> On(Expression<Func<TFrom, TJoin, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor<TFrom, TJoin>(
            _fromBuilder.Dialect,
            _fromBuilder.Alias,
            _joinAlias);

        (string condition, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        JoinedQueryBuilder<TFrom, TJoin> joinedQuery = CreateJoinedQuery(condition);
        for (int i = 0; i < parameters.Count; i++)
        {
            joinedQuery.AddParameter(parameters[i].Name, parameters[i].Value);
        }
        return joinedQuery;
    }

    public IJoinedQuery<TFrom, TJoin> On(string leftColumn, string rightColumn)
    {
        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery(condition);
    }

    public IJoinedQuery<TFrom, TJoin> On(string condition)
    {
        return CreateJoinedQuery(condition);
    }

    public IJoinedQuery<TFrom, TJoin> On<TValue>(string condition, TValue value)
        => On(condition, JoinParameterName.Default, value);

    public IJoinedQuery<TFrom, TJoin> On<TValue>(string condition, string parameterName, TValue value)
    {
        string qualified = JoinParameterName.Qualify(_fromBuilder.Dialect.ParameterPrefix, parameterName, nameof(parameterName));

        JoinedQueryBuilder<TFrom, TJoin> joinedQuery = CreateJoinedQuery(condition);

        if (joinedQuery.HasParameter(qualified))
            throw JoinParameterName.DuplicateError(qualified, nameof(parameterName));

        joinedQuery.AddParameter(qualified, value);
        return joinedQuery;
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
        // AUD-R26-058: AUD-R25 replaced this linear scan plus per-reference re-escape with the
        // pre-escaped CachedDialectMetadata lookup across the where/exists/select visitors and the
        // arity-3 and arity-4 join visitors, and left this site on the old shape. EscapeColumnName
        // re-runs SqlIdentifierValidator's regex match and a keyword HashSet lookup on every column
        // reference of every query build; the cache does it once per (entity, dialect) pair.
        EntityMetadata metadata = FluentMetadataCache.GetMetadata<T>();
        CachedDialectMetadata cached = FluentMetadataCache.GetForDialect<T>(_fromBuilder.Dialect);

        var escaped = cached.GetColumnName(propertyName);
        var prefix = alias ?? _fromBuilder.Dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);
        return $"{prefix}.{escaped}";
    }
}