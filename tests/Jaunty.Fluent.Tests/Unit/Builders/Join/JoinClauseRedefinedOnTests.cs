using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Builders.Join;

/// <summary>
/// AUD-R35-179. The arity-3 and arity-4 clause builders mutate a shared root builder rather than
/// constructing a fresh one the way the arity-2 clause builder does, so calling <c>On(...)</c> twice
/// on one held clause builder appended the same join twice: two identical JOIN clauses in the
/// generated SQL, a silently changed join count, and <c>Joins[1]</c> pointing at the wrong join for
/// arity-4 alias resolution. A clause builder describes one join, so a second <c>On</c> now
/// redefines it.
/// </summary>
public class JoinClauseRedefinedOnTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public JoinClauseRedefinedOnTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private static int Count(string sql, string needle) =>
        sql.Split(needle, StringSplitOptions.None).Length - 1;

    [Fact]
    public void ThirdJoinClause_OnCalledTwice_ProducesOneJoin()
    {
        var clause = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>();

        clause.On("products.supplier_id", "suppliers.supplier_id");
        string sql = clause.On("products.category_id", "suppliers.supplier_id").ToSql();

        Assert.Equal(2, Count(sql, "INNER JOIN"));
        Assert.Equal(1, Count(sql, "INNER JOIN suppliers"));
    }

    [Fact]
    public void ThirdJoinClause_LastOnWins()
    {
        var clause = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>();

        clause.On("products.supplier_id", "suppliers.supplier_id");
        string sql = clause.On("products.category_id", "suppliers.supplier_id").ToSql();

        Assert.Contains("products.category_id = suppliers.supplier_id", sql);
        Assert.DoesNotContain("products.supplier_id = suppliers.supplier_id", sql);
    }

    /// <summary>
    /// The residual documented on <c>ReplaceJoin</c>: both wrappers a clause builder hands out share
    /// one root, so the earlier wrapper sees the redefinition too. That is unchanged by the fix -
    /// what changed is that it sees one join rather than two.
    /// </summary>
    [Fact]
    public void ThirdJoinClause_EarlierWrapperSeesTheSameSingleJoin()
    {
        var clause = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>();

        var first = clause.On("products.supplier_id", "suppliers.supplier_id");
        clause.On("products.category_id", "suppliers.supplier_id");

        string sql = first.ToSql();

        Assert.Equal(2, Count(sql, "INNER JOIN"));
        Assert.Contains("products.category_id = suppliers.supplier_id", sql);
    }

    [Fact]
    public void ThirdJoinClause_OnCalledOnce_StillAddsTheJoin()
    {
        string sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On("products.supplier_id", "suppliers.supplier_id")
            .ToSql();

        Assert.Equal(2, Count(sql, "INNER JOIN"));
        Assert.Contains("suppliers", sql);
    }

    [Fact]
    public void FourthJoinClause_OnCalledTwice_ProducesOneJoin()
    {
        var clause = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>();

        clause.On("products.supplier_id", "orders.employee_id");
        string sql = clause.On("products.product_id", "orders.employee_id").ToSql();

        Assert.Equal(3, Count(sql, "INNER JOIN"));
        Assert.Equal(1, Count(sql, "INNER JOIN orders"));
        Assert.Contains("products.product_id = orders.employee_id", sql);
    }

    [Fact]
    public void FourthJoinClause_OnCalledOnce_StillAddsTheJoin()
    {
        string sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>()
            .On("products.supplier_id", "orders.employee_id")
            .ToSql();

        Assert.Equal(3, Count(sql, "INNER JOIN"));
        Assert.Contains("orders", sql);
    }
}
