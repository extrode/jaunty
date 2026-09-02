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

        (string? fromAlias, string? joinAlias) = Infer(
            leftKey.Parameters[0].Name,
            rightKey.Parameters[0].Name);

        string leftColumn = GetColumnName<TFrom>(leftProp, fromAlias);
        string rightColumn = GetColumnName<TJoin>(rightProp, joinAlias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery(condition, fromAlias, joinAlias);
    }

    public IJoinedQuery<TFrom, TJoin> On(Expression<Func<TFrom, TJoin, bool>> predicate)
    {
        (string? fromAlias, string? joinAlias) = Infer(
            predicate.Parameters[0].Name,
            predicate.Parameters[1].Name);

        var visitor = new JoinExpressionVisitor<TFrom, TJoin>(
            _fromBuilder.Dialect,
            fromAlias,
            joinAlias);

        (string condition, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        JoinedQueryBuilder<TFrom, TJoin> joinedQuery = CreateJoinedQuery(condition, fromAlias, joinAlias);
        joinedQuery.AddOnParameters(parameters);
        return joinedQuery;
    }

    /// <summary>
    /// Resolves the pair of table aliases for an expression-bearing <c>On</c>, falling back to the
    /// caller's explicit aliases when a lambda parameter name cannot serve as one.
    /// </summary>
    /// <remarks>
    /// Called before the ON condition is rendered, which is the only moment it can be: the
    /// condition is a string by the time <see cref="JoinedQueryBuilder{TFrom, TJoin}"/> exists, so
    /// an alias decided later could not reach it.
    /// </remarks>
    private (string? From, string? Join) Infer(string? fromName, string? joinName)
        => AliasInference.ForJoin(
            _fromBuilder.Dialect,
            _fromBuilder.Alias,
            _joinAlias,
            _fromBuilder.TableName,
            _joinMetadata.TableName,
            fromName,
            joinName);

    public IJoinedQuery<TFrom, TJoin> On(string leftColumn, string rightColumn)
    {
        // AUD-R34-022: see JoinColumnReference - this overload wins the overload resolution a
        // string-valued On(condition, value) call meant for the generic one.
        JoinColumnReference.Require(leftColumn, nameof(leftColumn));
        JoinColumnReference.Require(rightColumn, nameof(rightColumn));

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
        => CreateJoinedQuery(onCondition, _fromBuilder.Alias, _joinAlias);

    /// <summary>
    /// Builds the joined query with the aliases this <c>On</c> settled on, which are the explicit
    /// ones for the string-condition overloads and may be inferred for the expression overloads.
    /// </summary>
    private JoinedQueryBuilder<TFrom, TJoin> CreateJoinedQuery(string onCondition, string? fromAlias, string? joinAlias)
    {
        var joinInfo = new JoinInfo(
            _joinType,
            _joinMetadata.TableName,
            _joinMetadata.SchemaName,
            joinAlias,
            onCondition);

        return new JoinedQueryBuilder<TFrom, TJoin>(
            _fromBuilder.Connection,
            _fromBuilder.Dialect,
            _fromBuilder.TableName,
            _fromBuilder.SchemaName,
            fromAlias,
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