using System.Data;

using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// AUD-R35-020. <c>ExistsExpressionVisitor</c> never received the AUD-R33-010 / AUD-R34-018 sweep
/// the WHERE and SELECT visitors got, nor the AUD-R34-020 unary fix, nor the AUD-R34-021
/// nested-member guard. Every unhandled shape fell through to the base visitor's
/// descend-into-children default, which appends nothing for the wrapping node while its children
/// still emit fragments into the shared builder - a syntactically plausible EXISTS asking a
/// different question.
/// </summary>
public class ExistsVisitorHardeningTests
{
    private readonly ExistsHardeningConnection _connection;

    public ExistsVisitorHardeningTests()
    {
        SqlDialectFactory.RegisterDialect(nameof(ExistsHardeningConnection), new TestDialect());
        _connection = new ExistsHardeningConnection();
    }

    // ------------------------------------------------------------------
    // The control: the ordinary correlation still translates
    // ------------------------------------------------------------------

    [Fact]
    public void PlainCorrelation_StillTranslates()
    {
        string sql = _connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .ToSql();

        Assert.Contains("EXISTS", sql);
        Assert.Contains("[category_id]", sql);
    }

    [Fact]
    public void CompoundCorrelation_StillTranslates()
    {
        string sql = _connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId && p.UnitPrice > 10m)
            .ToSql();

        Assert.Contains(" AND ", sql);
    }

    [Fact]
    public void NotAndConvert_StillTranslate()
    {
        string sql = _connection.From<Category>()
            .WhereExists<Product>((c, p) => !(c.CategoryId == p.CategoryId))
            .ToSql();

        Assert.Contains("NOT (", sql);
    }

    // ------------------------------------------------------------------
    // Unary: the operator used to vanish
    // ------------------------------------------------------------------

    [Fact]
    public void Negate_Throws()
    {
        // Reached through AnalyzeExpression, not VisitUnary: VisitBinary analyses its operands
        // directly. It used to be handed to Expression.Compile and surface as an
        // InvalidOperationException naming a LINQ-internal scope.
        var ex = Assert.Throws<NotSupportedException>(() => _connection.From<Category>()
            .WhereExists<Product>((c, p) => -p.UnitPrice > 10m)
            .ToSql());

        Assert.Contains("correlation parameter", ex.Message);
    }

    [Fact]
    public void NegateNestedUnderNot_StillThrows()
    {
        // Not visits its operand, which is a comparison, so the Negate is analysed rather than
        // dispatched - the guard still catches it, one level down.
        Assert.Throws<NotSupportedException>(() => _connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId && !(-p.UnitPrice > 10m))
            .ToSql());
    }

    [Fact]
    public void TypeAsOverAColumn_Throws()
    {
        Assert.Throws<NotSupportedException>(() => _connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId && (p.ProductName as string) != null)
            .ToSql());
    }

    [Fact]
    public void OnesComplement_Throws() =>
        Assert.Throws<NotSupportedException>(() => _connection.From<Category>()
            .WhereExists<Product>((c, p) => ~p.ProductId == c.CategoryId)
            .ToSql());

    [Fact]
    public void ConvertChecked_IsUnwrappedLikeConvert()
    {
        string sql = _connection.From<Category>()
            .WhereExists<Product>((c, p) => checked((long)p.ProductId) == 5L)
            .ToSql();

        Assert.Contains("[product_id]", sql);
    }

    // ------------------------------------------------------------------
    // The AUD-R33-010 shapes, all reached through the AndAlso branch
    // ------------------------------------------------------------------

    [Fact]
    public void TypeTest_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() => _connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId && c.CategoryName is object)
            .ToSql());

        Assert.Contains("Type tests", ex.Message);
    }

    [Fact]
    public void ConstructionThatMentionsNoParameter_IsStillEvaluated()
    {
        // The control for the guard below: an operand free of both correlation parameters is a
        // value the caller could have computed, and is still bound as one.
        string sql = _connection.From<Category>()
            .WhereExists<Product>((c, p) => p.ProductId == new string('x', 3).Length)
            .ToSql();

        Assert.Contains("[product_id]", sql);
    }

    [Fact]
    public void DelegateInvocationOverAColumn_Throws()
    {
        Func<int, int> identity = x => x;

        var ex = Assert.Throws<NotSupportedException>(() => _connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId && identity(p.ProductId) == 1)
            .ToSql());

        Assert.Contains("correlation parameter", ex.Message);
    }

    [Fact]
    public void DelegateInvocationAsAnAndAlsoOperand_ThrowsFromVisitInvocation()
    {
        Func<bool> flag = () => true;

        var ex = Assert.Throws<NotSupportedException>(() => _connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId && flag())
            .ToSql());

        Assert.Contains("Invoking a delegate", ex.Message);
    }

    // ------------------------------------------------------------------
    // The AUD-R34-021 nested-member hole, fourth copy
    // ------------------------------------------------------------------

    [Fact]
    public void NestedMemberOnTheSubqueryParameter_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() => _connection.From<Category>()
            .WhereExists<Order>((c, o) => o.OrderDate!.Value.Year == 1997)
            .ToSql());

        Assert.Contains("is a member of a column, not a column", ex.Message);
    }

    [Fact]
    public void NestedMemberOnTheOuterParameter_Throws() =>
        Assert.Throws<NotSupportedException>(() => _connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryName!.Length == p.ProductId)
            .ToSql());

    [Fact]
    public void ACapturedNestedMemberIsStillEvaluated()
    {
        // The guard only fires when the chain roots at one of the two lambda parameters; a closure
        // member is not a column reference and still evaluates to a bound parameter.
        var local = new { Inner = new { Value = 3 } };

        string sql = _connection.From<Category>()
            .WhereExists<Product>((c, p) => p.ProductId == local.Inner.Value)
            .ToSql();

        Assert.Contains("[product_id]", sql);
    }

    private sealed class ExistsHardeningConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => string.Empty;
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Dispose() { }
        public void Open() { }
    }
}
