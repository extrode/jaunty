using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// The third and last cluster the 2026-08-27 mutation baseline left uncovered in
/// <c>ExistsExpressionVisitor</c>. <see cref="ExistsVisitorHardeningTests"/> covers the shapes the
/// visitor rejects through the fluent API and <see cref="ExistsVisitorTranslationTests"/> covers
/// the emission paths; what neither reaches is the <em>dispatcher</em> - the <c>Visit*</c>
/// overrides that only run when a node arrives through the base traversal rather than through
/// <c>VisitBinary</c>'s direct call to <c>AnalyzeExpression</c>.
///
/// <para>
/// A node reaches the dispatcher only as an operand of <c>AndAlso</c>/<c>OrElse</c>/<c>Not</c>,
/// which means it must be bool-typed. Most of the defensive throws cannot be: an array
/// construction, an object initializer or a bare entity parameter is never a bool, so no C#
/// lambda can produce one there. Those are tested by handing the node to <c>Visit</c> directly and
/// are marked as such - they pin a contract, not a reachable path. The rest use ordinary lambdas.
/// </para>
/// </summary>
public class ExistsVisitorDispatchTests
{
    private readonly TestDialect _dialect = new();

    private static readonly ParameterExpression Outer = Expression.Parameter(typeof(Category), "c");
    private static readonly ParameterExpression Subquery = Expression.Parameter(typeof(Product), "p");

    private ExistsExpressionVisitor<Category, Product> Visitor()
        => new(
            _dialect,
            FluentMetadataCache.GetMetadata<Category>(),
            FluentMetadataCache.GetMetadata<Product>(),
            "c",
            "p");

    private (string Sql, List<(string Name, object? Value)> Parameters) Translate(
        Expression<Func<Category, Product, bool>> predicate)
        => Visitor().Translate(predicate);

    private (string Sql, List<(string Name, object? Value)> Parameters) TranslateBody(Expression body)
        => Visitor().Translate(Expression.Lambda<Func<Category, Product, bool>>(body, Outer, Subquery));

    // ------------------------------------------------------------------
    // VisitMember - a bool column is the one shape that reaches it for real
    // ------------------------------------------------------------------

    [Fact]
    public void ABoolColumnAsALogicalOperand_EmitsTheColumnAndBindsNothing()
    {
        var (sql, parameters) = Translate((c, p) => c.CategoryId == p.CategoryId && p.Discontinued);

        Assert.Contains("p.[discontinued]", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void ACapturedBoolAsALogicalOperand_IsBoundRatherThanEmitted()
    {
        var box = new { Flag = true };

        var (sql, parameters) = Translate((c, p) => c.CategoryId == p.CategoryId && box.Flag);

        Assert.Contains("@p_exists", sql);
        var parameter = Assert.Single(parameters);
        Assert.Equal(true, parameter.Value);
    }

    // ------------------------------------------------------------------
    // VisitConstant and the Convert unwrap
    // ------------------------------------------------------------------

    [Fact]
    public void ABoolConstantAsALogicalOperand_IsBound()
    {
        var (sql, parameters) = Translate((c, p) => c.CategoryId == p.CategoryId && true);

        Assert.Contains("@p_exists", sql);
        var parameter = Assert.Single(parameters);
        Assert.Equal(true, parameter.Value);
    }

    [Fact]
    public void ANullConstantEmitsNull_AndTheConvertWrapperIsUnwrapped()
    {
        // Lifted, so hand-built: C# cannot write a bool?-typed null as a logical operand and still
        // give the lambda a bool body. The Convert around the AndAlso is what VisitUnary unwraps.
        Expression body = Expression.Convert(
            Expression.AndAlso(
                Expression.Constant(true, typeof(bool?)),
                Expression.Constant(null, typeof(bool?))),
            typeof(bool));

        var (sql, parameters) = TranslateBody(body);

        Assert.Contains("NULL", sql);
        Assert.Contains("@p_exists", sql);
        var parameter = Assert.Single(parameters);
        Assert.Equal(true, parameter.Value);
    }

    [Fact]
    public void ABoxedBoolColumn_IsUnwrappedThroughBothConverts()
    {
        var (sql, parameters) = Translate((c, p) => c.CategoryId == p.CategoryId && (bool)(object)p.Discontinued);

        Assert.Contains("p.[discontinued]", sql);
        Assert.Empty(parameters);
    }

    // ------------------------------------------------------------------
    // GetOperator: the two comparisons no other test emits, and its default
    // ------------------------------------------------------------------

    [Fact]
    public void LessThanOrEqual_Emits()
    {
        var (sql, _) = Translate((c, p) => p.UnitPrice <= 10m);

        Assert.Contains(" <= ", sql);
    }

    [Fact]
    public void GreaterThanOrEqual_Emits()
    {
        var (sql, _) = Translate((c, p) => p.UnitPrice >= 10m);

        Assert.Contains(" >= ", sql);
    }

    [Fact]
    public void AnArithmeticOperator_Throws()
    {
        // Not reachable through a predicate: an Add never type-checks as a lambda body, and as an
        // operand of a comparison it is analysed rather than dispatched, so RequireNoCorrelation-
        // Parameter reports it first. This pins GetOperator's own default arm.
        var ex = Assert.Throws<NotSupportedException>(() => Visitor().Visit(
            Expression.Add(Expression.Constant(1), Expression.Constant(2))));

        Assert.Contains("Add", ex.Message);
    }

    // ------------------------------------------------------------------
    // The defensive throws. Each node type below cannot be bool-typed, so none of them can arrive
    // through a lambda; Visit is called directly to pin the contract.
    // ------------------------------------------------------------------

    [Fact]
    public void AUnaryThatIsNeitherNotNorConvert_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() => Visitor().Visit(
            Expression.IsTrue(Expression.Constant(true))));

        Assert.Contains("Unary operator", ex.Message);
        Assert.Contains("IsTrue", ex.Message);
    }

    [Fact]
    public void AConstruction_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() => Visitor().Visit(
            Expression.New(typeof(bool))));

        Assert.Contains("Constructing", ex.Message);
        Assert.Contains("Boolean", ex.Message);
    }

    [Fact]
    public void AnArrayConstruction_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() => Visitor().Visit(
            Expression.NewArrayInit(typeof(int), Expression.Constant(1))));

        Assert.Contains("Array construction", ex.Message);
    }

    [Fact]
    public void AnObjectInitializer_Throws()
    {
        MemberInitExpression init = Expression.MemberInit(
            Expression.New(typeof(Product)),
            Expression.Bind(
                typeof(Product).GetProperty(nameof(Product.ProductId))!,
                Expression.Constant(1)));

        var ex = Assert.Throws<NotSupportedException>(() => Visitor().Visit(init));

        Assert.Contains("Object initializers", ex.Message);
        Assert.Contains(nameof(Product), ex.Message);
    }

    [Fact]
    public void ACollectionInitializer_Throws()
    {
        ListInitExpression init = Expression.ListInit(
            Expression.New(typeof(List<int>)),
            Expression.Constant(1));

        var ex = Assert.Throws<NotSupportedException>(() => Visitor().Visit(init));

        Assert.Contains("Collection initializers", ex.Message);
    }

    [Fact]
    public void AnIndexerAccess_Throws()
    {
        IndexExpression index = Expression.MakeIndex(
            Expression.Constant(new List<int> { 1 }),
            typeof(List<int>).GetProperty("Item"),
            new[] { Expression.Constant(0) });

        var ex = Assert.Throws<NotSupportedException>(() => Visitor().Visit(index));

        Assert.Contains("Indexer access", ex.Message);
    }

    [Fact]
    public void WithNoCorrelationParametersRecorded_AnUnanalysableOperandIsEvaluatedRatherThanRejected()
    {
        // CorrelationParameterFinder.Contains returns false before searching when both parameters
        // are null, which is the state a visitor is in until Translate records them. Without that
        // early-out the search would run against nulls and an evaluable operand would be rejected.
        Expression body = Expression.GreaterThan(
            Expression.Add(Expression.Constant(1), Expression.Constant(2)),
            Expression.Constant(0));

        Assert.Null(Record.Exception(() => Visitor().Visit(body)));
    }

    [Fact]
    public void ABareParameter_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() => Visitor().Visit(
            Expression.Parameter(typeof(bool), "flag")));

        Assert.Contains("flag", ex.Message);
        Assert.Contains("not a condition", ex.Message);
    }
}
