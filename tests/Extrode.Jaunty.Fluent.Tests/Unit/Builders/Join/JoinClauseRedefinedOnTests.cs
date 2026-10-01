using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Builders.Join;

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

    private static IDictionary<string, object?> Bound<T1, T2, T3>(IJoinedQuery3<T1, T2, T3> query)
        where T1 : new() where T2 : new() where T3 : new()
        => (IDictionary<string, object?>)((JoinedQuery3Builder<T1, T2, T3>)query)._parent.DescribeParameters();

    private static IDictionary<string, object?> Bound<T1, T2, T3, T4>(IJoinedQuery4<T1, T2, T3, T4> query)
        where T1 : new() where T2 : new() where T3 : new() where T4 : new()
        => (IDictionary<string, object?>)((JoinedQuery4Builder<T1, T2, T3, T4>)query)._parent._parent.DescribeParameters();

    private IJoinClause<Product, Category, Supplier> ThirdClause() => _fixture.Connection.From<Product>()
        .InnerJoin<Category>()
        .On(p => p.CategoryId, c => c.CategoryId)
        .InnerJoin<Supplier>();

    private IJoinClause<Product, Category, Supplier, Order> FourthClause() => _fixture.Connection.From<Product>()
        .InnerJoin<Category>()
        .On(p => p.CategoryId, c => c.CategoryId)
        .InnerJoin<Supplier>()
        .On(p => p.SupplierId, s => s.SupplierId)
        .InnerJoin<Product, Category, Supplier, Order>();

    [Fact]
    public void ThirdJoinClause_ParameterisedOnRedefined_RebindsTheSameName()
    {
        var clause = ThirdClause();

        clause.On("suppliers.supplier_id = @sid", "sid", 1);
        var query = clause.On("suppliers.supplier_id = @sid", "sid", 2);

        Assert.Equal(2, Assert.Single(Bound(query)).Value);
    }

    [Fact]
    public void ThirdJoinClause_PredicateOnRedefined_DropsTheFirstValues()
    {
        var clause = ThirdClause();
        int first = 1, second = 2;

        clause.On((p, c, s) => s.SupplierId == first);
        var query = clause.On((p, c, s) => s.SupplierId == second);

        Assert.Equal(second, Assert.Single(Bound(query)).Value);
        Assert.Contains(Assert.Single(Bound(query)).Key, query.ToSql());
    }

    [Fact]
    public void ThirdJoinClause_RedefinedWithoutParameters_DropsThePreviousOnes()
    {
        var clause = ThirdClause();

        clause.On("suppliers.supplier_id = @sid", "sid", 1);
        var query = clause.On("products.supplier_id", "suppliers.supplier_id");

        Assert.Empty(Bound(query));
    }

    [Fact]
    public void ThirdJoinClause_ANameBoundElsewhere_IsStillADuplicate()
    {
        var clause = ThirdClause();
        clause.On("suppliers.supplier_id = @sid", "sid", 1);

        var fourth = clause.On("suppliers.supplier_id = @sid", "sid", 1).InnerJoin<Product, Category, Supplier, Order>();

        Assert.Throws<ArgumentException>(() => fourth.On("orders.employee_id = @sid", "sid", 3));
    }

    [Fact]
    public void FourthJoinClause_ParameterisedOnRedefined_RebindsTheSameName()
    {
        var clause = FourthClause();

        clause.On("orders.employee_id = @eid", "eid", 1);
        var query = clause.On("orders.employee_id = @eid", "eid", 2);

        Assert.Equal(2, Assert.Single(Bound(query)).Value);
    }

    [Fact]
    public void FourthJoinClause_PredicateOnRedefined_DropsTheFirstValues()
    {
        var clause = FourthClause();
        int first = 1, second = 2;

        clause.On((p, c, s, o) => o.EmployeeId == first);
        var query = clause.On((p, c, s, o) => o.EmployeeId == second);

        Assert.Equal(second, Assert.Single(Bound(query)).Value);
    }

    [Fact]
    public void FourthJoinClause_RedefinedWithoutParameters_DropsThePreviousOnes()
    {
        var clause = FourthClause();

        clause.On("orders.employee_id = @eid", "eid", 1);
        var query = clause.On("products.supplier_id", "orders.employee_id");

        Assert.Empty(Bound(query));
    }

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
