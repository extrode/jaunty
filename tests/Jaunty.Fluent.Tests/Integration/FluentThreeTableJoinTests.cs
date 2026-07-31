using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;
using Jaunty.Fluent;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for 3-table join functionality (JoinedQuery3Builder), focused on the
/// Where(string column, object value) overload and the RightJoin&lt;T3&gt; overload.
///
/// Schema: Product → Category (category_id), Product → Supplier (supplier_id).
/// </summary>
public class FluentThreeTableJoinTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentThreeTableJoinTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void ThreeTableJoin_RightJoinThird_ToSql_ContainsRightJoin()
    {
        // Regression test: IJoinedQuery<TFrom,TJoin> previously only exposed InnerJoin<T3>
        // and LeftJoin<T3> to extend a 2-way join to 3-way - RightJoin<T3> was missing even
        // though the 2-way RightJoin<TJoin> and JoinType.Right both already existed.
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .RightJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .ToSql();

        Assert.Contains("RIGHT JOIN", sql);
    }

    [Fact]
    public void ThreeTableJoin_WhereColumnValue_ToSql_UsesBoundParameter()
    {
        // Regression test: JoinedQuery3Builder.Where(string column, object value) previously
        // did not exist, forcing callers onto raw-string Where() (or the strongly-typed
        // predicate overload) for a simple equality filter. It must bind the value as a
        // parameter, not inline it into the SQL text.
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where("products.product_name", "Chai")
            .ToSql();

        Assert.Contains("WHERE", sql);
        Assert.Contains("products.product_name =", sql);
        Assert.DoesNotContain("'Chai'", sql);
    }

    [Fact]
    public void ThreeTableJoin_WhereColumnValue_RejectsInjectionInAliasSegment()
    {
        // Regression test: the alias segment of a qualified column name (before the '.') is
        // validated as a plain identifier via SqlIdentifierValidator, so it can't be used to
        // smuggle arbitrary SQL text into the generated WHERE clause.
        Assert.Throws<ArgumentException>(() => _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where("products; DROP TABLE products--.product_name", "Chai"));
    }

    [Fact]
    public void ThreeTableJoin_OnTypedValue_BindsParameterInsteadOfInlining()
    {
        // Regression test: IJoinClause<T1,T2,T3> previously only exposed 4 On() overloads
        // (key-pair from T1, key-pair from T2, string/string, raw string), missing the
        // On<TValue>(condition, value) parameterized-raw-condition overload that the 2-way
        // IJoinClause<TFrom,TJoin> already had.
        var joinedQuery = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On<int>("suppliers.supplier_id = products.supplier_id AND suppliers.supplier_id = @value", 1);

        var builder = Assert.IsType<JoinedQuery3Builder<Product, Category, Supplier>>(joinedQuery);
        var parameters = builder._parent.GetParameters().GetAll();

        Assert.Contains(parameters, p => p.Name == "@value" && Equals(p.Value, 1));
    }

    [Fact]
    public void ThreeTableJoin_WhereColumnValue_ColumnWithSpace_ThrowsBeforeParameterIsBuilt()
    {
        Assert.Throws<ArgumentException>(() =>
            _fixture.Connection.From<Product>()
                .InnerJoin<Category>()
                .On(p => p.CategoryId, c => c.CategoryId)
                .InnerJoin<Supplier>()
                .On(p => p.SupplierId, s => s.SupplierId)
                .Where("products.product name", "Chai"));
    }
}
