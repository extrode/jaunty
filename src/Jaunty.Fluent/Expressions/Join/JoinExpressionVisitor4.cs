using System.Linq.Expressions;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// Converts Expression{Func{T1, T2, T3, T4, bool}} predicates to SQL JOIN ON or WHERE clauses.
/// </summary>
internal sealed class JoinExpressionVisitor4<T1, T2, T3, T4> : ExpressionVisitor
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
{
    private readonly ISqlDialect _dialect;
    private readonly string? _alias1;
    private readonly string? _alias2;
    private readonly string? _alias3;
    private readonly string? _alias4;
    private readonly StringBuilder _sql = new();
    private ParameterExpression? _param1;
    private ParameterExpression? _param2;
    private ParameterExpression? _param3;
    private ParameterExpression? _param4;
    private readonly List<(string Name, object? Value)> _parameters = new();
    private int _parameterIndex;

    public JoinExpressionVisitor4(ISqlDialect dialect, string? alias1, string? alias2, string? alias3, string? alias4)
    {
        _dialect = dialect;
        _alias1 = alias1;
        _alias2 = alias2;
        _alias3 = alias3;
        _alias4 = alias4;
    }

    public (string Sql, List<(string Name, object? Value)> Parameters) Translate(Expression<Func<T1, T2, T3, T4, bool>> predicate)
    {
        _sql.Clear();
        _parameters.Clear();
        _parameterIndex = 0;
        _param1 = predicate.Parameters[0];
        _param2 = predicate.Parameters[1];
        _param3 = predicate.Parameters[2];
        _param4 = predicate.Parameters[3];
        Visit(predicate.Body);
        return (_sql.ToString(), _parameters);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _sql.Append('(');

        if (node.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            Visit(node.Left);
            _sql.Append(node.NodeType == ExpressionType.AndAlso ? " AND " : " OR ");
            Visit(node.Right);
            _sql.Append(')');
            return node;
        }

        string? leftColumn = TryGetColumnExpression(node.Left);
        string? rightColumn = TryGetColumnExpression(node.Right);

        // Handle null comparisons: emit IS NULL / IS NOT NULL instead of = NULL / <> NULL.
        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
        {
            if (leftColumn is not null && rightColumn is null && IsNullValue(node.Right))
            {
                _sql.Append(leftColumn);
                _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                _sql.Append(')');
                return node;
            }

            if (rightColumn is not null && leftColumn is null && IsNullValue(node.Left))
            {
                _sql.Append(rightColumn);
                _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                _sql.Append(')');
                return node;
            }
        }

        if (leftColumn is not null)
            _sql.Append(leftColumn);
        else
            Visit(node.Left);

        _sql.Append(node.NodeType switch
        {
            ExpressionType.Equal => " = ",
            ExpressionType.NotEqual => " <> ",
            ExpressionType.LessThan => " < ",
            ExpressionType.LessThanOrEqual => " <= ",
            ExpressionType.GreaterThan => " > ",
            ExpressionType.GreaterThanOrEqual => " >= ",
            _ => throw new NotSupportedException($"Operator {node.NodeType} is not supported.")
        });

        if (rightColumn is not null)
            _sql.Append(rightColumn);
        else
            Visit(node.Right);

        _sql.Append(')');

        return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            _sql.Append("NOT (");
            Visit(node.Operand);
            _sql.Append(')');
            return node;
        }

        if (node.NodeType == ExpressionType.Convert)
        {
            Visit(node.Operand);
            return node;
        }

        // AUD-R34-019: the base implementation visits the operand and appends nothing for the
        // operator, so `-p.UnitPrice` used to translate to a bare column - the negation silently
        // gone from the emitted SQL. Negate, TypeAs, OnesComplement, ArrayLength and UnaryPlus all
        // took that route.
        throw new NotSupportedException(
            $"Unary operator '{node.NodeType}' is not supported in JOIN expressions.");
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        string? column = TryGetColumnExpression(node);
        if (column is not null)
        {
            _sql.Append(column);
            return node;
        }

        // Constant or other value
        object? value = EvaluateExpression(node);
        AppendValue(value);
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        AppendValue(node.Value);
        return node;
    }

    /// <inheritdoc cref="JoinExpressionVisitor{T1, T2}.VisitConditional"/>
    protected override Expression VisitConditional(ConditionalExpression node)
        => throw new NotSupportedException(
            "Conditional (ternary) expressions are not supported in JOIN predicates. " +
            "Split the predicate into separate conditions, or filter with Where after the join.");

    /// <inheritdoc cref="JoinExpressionVisitor{T1, T2}.VisitNew"/>
    protected override Expression VisitNew(NewExpression node)
        => throw new NotSupportedException(
            $"Constructing a '{node.Type.Name}' is not supported inside a JOIN predicate. " +
            "Compute the value before the query and compare against it.");

    /// <inheritdoc cref="JoinExpressionVisitor{T1, T2}.VisitNew"/>
    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        => throw new NotSupportedException(
            "Type tests ('is', 'as') are not supported in JOIN predicates. There is no SQL " +
            "equivalent of a CLR type test over a column; join on a discriminator column instead.");

    /// <inheritdoc cref="JoinExpressionVisitor{T1, T2}.VisitNew"/>
    protected override Expression VisitInvocation(InvocationExpression node)
        => throw new NotSupportedException(
            "Invoking a delegate or a nested lambda is not supported inside a JOIN predicate. " +
            "Inline the condition.");

    /// <inheritdoc cref="JoinExpressionVisitor{T1, T2}.VisitNew"/>
    protected override Expression VisitNewArray(NewArrayExpression node)
        => throw new NotSupportedException(
            "Array construction is not supported inside a JOIN predicate. Build the array before " +
            "the query and pass it in.");

    /// <inheritdoc cref="JoinExpressionVisitor{T1, T2}.VisitNew"/>
    protected override Expression VisitMemberInit(MemberInitExpression node)
        => throw new NotSupportedException(
            $"Object initializers ('new {node.Type.Name} {{ ... }}') are not supported inside a " +
            "JOIN predicate. Compute the value before the query and compare against it.");

    /// <inheritdoc cref="JoinExpressionVisitor{T1, T2}.VisitNew"/>
    protected override Expression VisitListInit(ListInitExpression node)
        => throw new NotSupportedException(
            "Collection initializers are not supported inside a JOIN predicate. Build the " +
            "collection before the query and pass it in.");

    /// <inheritdoc cref="JoinExpressionVisitor{T1, T2}.VisitNew"/>
    protected override Expression VisitIndex(IndexExpression node)
        => throw new NotSupportedException(
            "Indexer access is not supported inside a JOIN predicate. Compute the value before " +
            "the query and compare against it.");

    /// <inheritdoc cref="JoinExpressionVisitor{T1, T2}.VisitNew"/>
    protected override Expression VisitDefault(DefaultExpression node)
        => throw new NotSupportedException(
            $"'default({node.Type.Name})' is not supported inside a JOIN predicate. Write the " +
            "value out, or compute it before the query.");

    /// <inheritdoc cref="JoinExpressionVisitor{T1, T2}.VisitNew"/>
    protected override Expression VisitParameter(ParameterExpression node)
        => throw new NotSupportedException(
            $"'{node.Name}' is a whole entity, not a condition. Compare its properties instead.");

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        throw new NotSupportedException($"Method '{node.Method.Name}' is not supported in JOIN expressions.");
    }

    private string? TryGetColumnExpression(Expression expression)
    {
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        if (expression is not MemberExpression member)
            return null;

        Expression? current = member;
        while (current is MemberExpression me)
            current = me.Expression;

        if (current is not ParameterExpression param)
            return null;

        string? alias;
        EntityMetadata metadata;
        CachedDialectMetadata cached;

        // Match by parameter reference/identity (not by Type) so self-joins resolve correctly.
        if (param == _param1)
        {
            alias = _alias1;
            metadata = FluentMetadataCache.GetMetadata<T1>();
            cached = FluentMetadataCache.GetForDialect<T1>(_dialect);
        }
        else if (param == _param2)
        {
            alias = _alias2;
            metadata = FluentMetadataCache.GetMetadata<T2>();
            cached = FluentMetadataCache.GetForDialect<T2>(_dialect);
        }
        else if (param == _param3)
        {
            alias = _alias3;
            metadata = FluentMetadataCache.GetMetadata<T3>();
            cached = FluentMetadataCache.GetForDialect<T3>(_dialect);
        }
        else if (param == _param4)
        {
            alias = _alias4;
            metadata = FluentMetadataCache.GetMetadata<T4>();
            cached = FluentMetadataCache.GetForDialect<T4>(_dialect);
        }
        else
        {
            return null;
        }

        // AUD-R25: this used to run metadata.Columns.FirstOrDefault(c => c.PropertyName == ...) -
        // a LINQ delegate allocation plus an O(columns) linear scan - and hand the result to
        // _dialect.EscapeColumnName, which re-runs SqlIdentifierValidator's regex match and a
        // keyword HashSet lookup, both per column reference per query build. CachedDialectMetadata
        // holds an OrdinalIgnoreCase dictionary of property name to already-escaped column name,
        // built once per (entity, dialect) pair.
        string escapedColumn = cached.GetColumnName(member.Member.Name);

        string prefix = alias ?? _dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);
        return $"{prefix}.{escapedColumn}";
    }

    private void AppendValue(object? value)
    {
        if (value is null)
        {
            _sql.Append("NULL");
            return;
        }

        var paramName = $"{_dialect.ParameterPrefix}jp{_parameterIndex++}";
        _sql.Append(paramName);
        _parameters.Add((paramName, value));
    }

    private bool IsNullValue(Expression expression)
    {
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        if (expression is ConstantExpression constant)
            return constant.Value is null;

        if (expression is MemberExpression && TryGetColumnExpression(expression) is null)
            return EvaluateExpression(expression) is null;

        return false;
    }

    // AUD-R25: this was one of eight byte-identical private copies. Kept as a one-line forwarder
    // rather than rewriting every call site, so the shared implementation - including its
    // closure-member fast path - is the only place the behaviour lives.
    private static object? EvaluateExpression(Expression expression) => ExpressionEvaluator.Evaluate(expression);
}
