using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Text;

using Extrode.Jaunty.Dialects;
using Extrode.Jaunty.Fluent.Internals;
using Extrode.Jaunty.Internals.Entity;

namespace Extrode.Jaunty.Fluent.Expressions;

/// <summary>One lambda parameter of a join predicate: its alias and the entity it maps.</summary>
internal readonly struct JoinSide
{
    private JoinSide(string? alias, EntityMetadata metadata, CachedDialectMetadata cached)
    {
        Alias = alias;
        Metadata = metadata;
        Cached = cached;
    }

    public string? Alias { get; }

    public EntityMetadata Metadata { get; }

    public CachedDialectMetadata Cached { get; }

    public static JoinSide For<T>(ISqlDialect dialect, string? alias) where T : new()
        => new(alias, FluentMetadataCache.GetMetadata<T>(), FluentMetadataCache.GetForDialect<T>(dialect));
}

/// <summary>
/// Converts a join predicate over two to four entities to a SQL ON or WHERE clause. Supports
/// ==, !=, &lt;, &lt;=, &gt;, &gt;=, &amp;&amp;, ||, !, bool columns as conditions, and comparisons
/// between columns or against captured values.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R38-007. The arity-2, -3 and -4 visitors were three copies of this class, and they had
/// drifted: each fix reached one or two of them (AUD-R26-058 found the arity-2 copy still scanning
/// columns linearly after AUD-R25 fixed the others). None of the three had
/// <see cref="ColumnReference.RequireDirect"/>, so <c>o.OrderDate.Year</c> resolved to a column named
/// <c>Year</c> - the fifth copy of AUD-R34-021. The generic <c>JoinExpressionVisitor</c> classes are
/// now thin typed entry points over this one implementation.
/// </para>
/// <para>
/// Also fixed here, all previously fixed in the WHERE or EXISTS twin only: a bool column or captured
/// bool used as a whole condition is emitted as <c>col = TRUE</c> / <c>1 = 1</c> rather than bare,
/// which SQL Server rejects (AUD-R38-036); an operand that is not a column but still mentions a
/// join parameter raises <see cref="NotSupportedException"/> instead of the evaluator's opaque
/// unbound-variable error; <c>ConvertChecked</c> is unwrapped like <c>Convert</c>; and a relational
/// comparison against NULL emits <c>1 = 0</c> like WHERE, so <c>!(p.SupplierId &gt; noInt)</c>
/// matches every row as C#'s lifted operator does rather than none.
/// </para>
/// </remarks>
internal abstract class JoinPredicateVisitor : ExpressionVisitor
{
    private readonly ISqlDialect _dialect;
    private readonly JoinSide[] _sides;
    private readonly StringBuilder _sql = new();
    private readonly List<(string Name, object? Value)> _parameters = new();
    private ReadOnlyCollection<ParameterExpression> _lambdaParameters = new(Array.Empty<ParameterExpression>());
    private int _parameterIndex;

    // The column the value about to be appended is being compared against, so the parameter can be
    // named after it. Set by VisitBinary immediately before it visits the non-column side, and
    // consumed once by AppendValue - a value reached any other way has no single column to name it
    // after and takes the positional form.
    private string? _pendingColumn;

    protected JoinPredicateVisitor(ISqlDialect dialect, JoinSide[] sides)
    {
        _dialect = dialect;
        _sides = sides;
    }

    protected (string Sql, List<(string Name, object? Value)> Parameters) TranslateCore(LambdaExpression predicate)
    {
        _sql.Clear();
        _parameters.Clear();
        _parameterIndex = 0;
        _pendingColumn = null;
        _lambdaParameters = predicate.Parameters;

        VisitCondition(predicate.Body);

        return (_sql.ToString(), _parameters);
    }

    /// <summary>
    /// Visits an expression that stands as a whole condition: the predicate body, an operand of
    /// <c>&amp;&amp;</c>/<c>||</c>, or the operand of <c>!</c>.
    /// </summary>
    private void VisitCondition(Expression node)
    {
        if (node.Type == typeof(bool))
        {
            if (TryGetColumnExpression(node) is { } column)
            {
                _sql.Append(column).Append(" = ").Append(_dialect.FormatBooleanLiteral(true));
                return;
            }

            if (node is MemberExpression or ConstantExpression && !MentionsJoinParameter(node))
            {
                _sql.Append(EvaluateExpression(node) is true ? "1 = 1" : "1 = 0");
                return;
            }
        }

        Visit(node);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _sql.Append('(');

        if (node.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            VisitCondition(node.Left);
            _sql.Append(node.NodeType == ExpressionType.AndAlso ? " AND " : " OR ");
            VisitCondition(node.Right);
            _sql.Append(')');
            return node;
        }

        var leftColumn = TryGetColumnExpression(node.Left);
        var rightColumn = TryGetColumnExpression(node.Right);
        bool leftIsNull = leftColumn is null && IsNullValue(node.Left);
        bool rightIsNull = rightColumn is null && IsNullValue(node.Right);

        // Handle null comparisons: emit IS NULL / IS NOT NULL instead of = NULL / <> NULL.
        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
        {
            // Both sides closed over null: C# says null == null is true, but SQL's NULL = NULL is
            // UNKNOWN and would match no rows. Fold it to the constant C# would have produced.
            if (leftIsNull && rightIsNull)
            {
                _sql.Append(node.NodeType == ExpressionType.Equal ? "1 = 1" : "1 = 0");
                _sql.Append(')');
                return node;
            }

            if (leftColumn is not null && rightIsNull)
            {
                _sql.Append(leftColumn);
                _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                _sql.Append(')');
                return node;
            }

            if (rightColumn is not null && leftIsNull)
            {
                _sql.Append(rightColumn);
                _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                _sql.Append(')');
                return node;
            }
        }
        else if (leftIsNull || rightIsNull)
        {
            // A relational comparison against NULL is false for C#'s lifted operators; `col > NULL`
            // is UNKNOWN instead, which a surrounding NOT keeps UNKNOWN rather than flipping to true.
            _sql.Append("1 = 0");
            _sql.Append(')');
            return node;
        }

        if (leftColumn is not null)
        {
            _sql.Append(leftColumn);
        }
        else
        {
            _pendingColumn = rightColumn;
            Visit(node.Left);
        }

        _sql.Append(GetOperator(node.NodeType));

        if (rightColumn is not null)
        {
            _sql.Append(rightColumn);
        }
        else
        {
            _pendingColumn = leftColumn;
            Visit(node.Right);
        }

        _sql.Append(')');
        return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            _sql.Append("NOT (");
            VisitCondition(node.Operand);
            _sql.Append(')');
            return node;
        }

        if (node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
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
        var column = TryGetColumnExpression(node);
        if (column is not null)
        {
            _sql.Append(column);
            return node;
        }

        RequireNoJoinParameter(node);
        AppendValue(EvaluateExpression(node));
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        AppendValue(node.Value);
        return node;
    }

    /// <summary>
    /// AUD-R33-003. The AUD-R32-002 fix reached <c>WhereExpressionVisitor</c> and
    /// <c>SelectExpressionVisitor</c> but never the JOIN and EXISTS visitors, which have the same
    /// gap: with no override the base traversal walks Test/IfTrue/IfFalse and each appends its own
    /// fragment to the shared <c>_sql</c> builder with nothing joining them. Every other
    /// unsupported shape in this file throws, so this does too.
    /// </summary>
    protected override Expression VisitConditional(ConditionalExpression node)
        => throw new NotSupportedException(
            "Conditional (ternary) expressions are not supported in JOIN predicates. " +
            "Split the predicate into separate conditions, or filter with Where after the join.");

    /// <summary>
    /// AUD-R34-019. AUD-R32-002/R33-003 closed <see cref="ConditionalExpression"/> here, but every
    /// other node type this class does not override still fell through to
    /// <see cref="ExpressionVisitor"/>'s descend-into-children default, which appends each child's
    /// fragment to the shared <c>_sql</c> builder with nothing joining them. A constructor call in
    /// a predicate - <c>(p, c) =&gt; p.Created == new DateTime(2020, 1, 1)</c> - is ordinary C# and
    /// used to emit <c>(p.[created] = @jp0@jp1@jp2)</c>. The rest emit their children bare, or
    /// nothing at all, leaving a dangling operator. All of them now throw, like the rest of this
    /// file.
    /// </summary>
    protected override Expression VisitNew(NewExpression node)
        => throw new NotSupportedException(
            $"Constructing a '{node.Type.Name}' is not supported inside a JOIN predicate. " +
            "Compute the value before the query and compare against it.");

    /// <inheritdoc cref="VisitNew"/>
    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        => throw new NotSupportedException(
            "Type tests ('is', 'as') are not supported in JOIN predicates. There is no SQL " +
            "equivalent of a CLR type test over a column; join on a discriminator column instead.");

    /// <inheritdoc cref="VisitNew"/>
    protected override Expression VisitInvocation(InvocationExpression node)
        => throw new NotSupportedException(
            "Invoking a delegate or a nested lambda is not supported inside a JOIN predicate. " +
            "Inline the condition.");

    /// <inheritdoc cref="VisitNew"/>
    protected override Expression VisitNewArray(NewArrayExpression node)
        => throw new NotSupportedException(
            "Array construction is not supported inside a JOIN predicate. Build the array before " +
            "the query and pass it in.");

    /// <inheritdoc cref="VisitNew"/>
    protected override Expression VisitMemberInit(MemberInitExpression node)
        => throw new NotSupportedException(
            $"Object initializers ('new {node.Type.Name} {{ ... }}') are not supported inside a " +
            "JOIN predicate. Compute the value before the query and compare against it.");

    /// <inheritdoc cref="VisitNew"/>
    protected override Expression VisitListInit(ListInitExpression node)
        => throw new NotSupportedException(
            "Collection initializers are not supported inside a JOIN predicate. Build the " +
            "collection before the query and pass it in.");

    /// <inheritdoc cref="VisitNew"/>
    protected override Expression VisitIndex(IndexExpression node)
        => throw new NotSupportedException(
            "Indexer access is not supported inside a JOIN predicate. Compute the value before " +
            "the query and compare against it.");

    /// <inheritdoc cref="VisitNew"/>
    protected override Expression VisitDefault(DefaultExpression node)
        => throw new NotSupportedException(
            $"'default({node.Type.Name})' is not supported inside a JOIN predicate. Write the " +
            "value out, or compute it before the query.");

    /// <inheritdoc cref="VisitNew"/>
    protected override Expression VisitParameter(ParameterExpression node)
        => throw new NotSupportedException(
            $"'{node.Name}' is a whole entity, not a condition. Compare its properties instead.");

    protected override Expression VisitMethodCall(MethodCallExpression node)
        => throw new NotSupportedException($"Method '{node.Method.Name}' is not supported in JOIN expressions.");

    private static Expression UnwrapConvert(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            ? unary.Operand
            : expression;

    private string? TryGetColumnExpression(Expression expression)
    {
        if (UnwrapConvert(expression) is not MemberExpression member)
            return null;

        Expression? current = member;
        while (current is MemberExpression me)
            current = me.Expression;

        // Match by parameter identity, not by type, so self-joins resolve to the right alias.
        int index = current is ParameterExpression param ? _lambdaParameters.IndexOf(param) : -1;
        if (index < 0)
            return null;

        ColumnReference.RequireDirect(member);

        JoinSide side = _sides[index];
        var escaped = side.Cached.GetColumnName(member.Member.Name);
        var prefix = side.Alias ?? _dialect.EscapeTableName(side.Metadata.SchemaName, side.Metadata.TableName);
        return $"{prefix}.{escaped}";
    }

    private bool IsNullValue(Expression expression)
    {
        expression = UnwrapConvert(expression);

        if (expression is ConstantExpression constant)
            return constant.Value is null;

        // Only evaluate side-effect-free member accesses over captured values to detect nulls.
        if (expression is MemberExpression && !MentionsJoinParameter(expression))
            return EvaluateExpression(expression) is null;

        return false;
    }

    private bool MentionsJoinParameter(Expression expression)
        => ParameterReferenceFinder.Mentions(expression, _lambdaParameters);

    /// <summary>
    /// An operand that is not a column but still mentions a join parameter - <c>p.Name.Trim().Length</c> -
    /// cannot be evaluated before the query; compiling it surfaced the evaluator's internal
    /// "variable 'p' ... referenced from scope ''" message. Same guard as the EXISTS twin (AUD-R35-021).
    /// </summary>
    private void RequireNoJoinParameter(Expression expression)
    {
        if (!MentionsJoinParameter(expression))
            return;

        throw new NotSupportedException(
            $"'{expression}' is not a translatable column reference, and it cannot be evaluated " +
            "before the query because it still refers to a join parameter. Compare columns " +
            "directly, or compute the value outside the predicate and capture it.");
    }

    private void AppendValue(object? value)
    {
        if (value is null)
        {
            _sql.Append("NULL");
            return;
        }

        string? column = _pendingColumn;
        _pendingColumn = null;
        var paramName = JoinParameterNaming.Derive(_dialect.ParameterPrefix, column, _parameters, ref _parameterIndex);
        _sql.Append(paramName);
        _parameters.Add((paramName, value));
    }

    // AUD-R25: this was one of eight byte-identical private copies. Kept as a one-line forwarder
    // rather than rewriting every call site, so the shared implementation - including its
    // closure-member fast path - is the only place the behaviour lives.
    private static object? EvaluateExpression(Expression expression) => ExpressionEvaluator.Evaluate(expression);

    private static string GetOperator(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Equal => " = ",
        ExpressionType.NotEqual => " <> ",
        ExpressionType.LessThan => " < ",
        ExpressionType.LessThanOrEqual => " <= ",
        ExpressionType.GreaterThan => " > ",
        ExpressionType.GreaterThanOrEqual => " >= ",
        _ => throw new NotSupportedException($"Operator {nodeType} is not supported in JOIN expressions.")
    };
}
