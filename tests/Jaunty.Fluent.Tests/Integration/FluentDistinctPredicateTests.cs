using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// AUD-R35-204: DISTINCT combined with the nine predicate forms that only <c>IFromClause&lt;T&gt;</c>
/// used to declare.
/// </summary>
public class FluentDistinctPredicateTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentDistinctPredicateTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private IDistinctClause<Product> Distinct() => _fixture.Connection.From<Product>().Distinct();

    [Fact]
    public void DistinctWhereRaw_KeepsTheDistinctAndTheCondition()
    {
        var sql = Distinct().WhereRaw("category_id = 1").ToSql();

        Assert.Contains("SELECT DISTINCT", sql);
        Assert.Contains("category_id = 1", sql);
    }

    [Fact]
    public void DistinctWhereRawWithParameters_Executes()
    {
        var results = Distinct().WhereRaw("category_id = @id", new { id = 1 }).Select();

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.Equal((short?)1, p.CategoryId));
    }

    [Fact]
    public void DistinctWhereIn_Executes()
    {
        var results = Distinct().WhereIn(p => p.CategoryId, new short?[] { 1, 2 }).Select();

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.Contains(p.CategoryId, new short?[] { 1, 2 }));
    }

    [Fact]
    public void DistinctWhereNotIn_Executes()
    {
        var results = Distinct().WhereNotIn(p => p.CategoryId, new short?[] { 1 }).Select();

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.NotEqual((short?)1, p.CategoryId));
    }

    [Fact]
    public void DistinctWhereBetween_Executes()
    {
        var results = Distinct().WhereBetween(p => p.CategoryId, (short?)1, (short?)2).Select();

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.InRange(p.CategoryId!.Value, (short)1, (short)2));
    }

    [Fact]
    public void DistinctWhereNotBetween_KeepsTheDistinct()
    {
        var sql = Distinct().WhereNotBetween(p => p.CategoryId, (short?)1, (short?)2).ToSql();

        Assert.Contains("SELECT DISTINCT", sql);
        Assert.Contains("NOT BETWEEN", sql);
    }

    [Fact]
    public void DistinctWhereExists_Executes()
    {
        var results = _fixture.Connection.From<Category>()
            .Distinct()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .Select();

        Assert.NotEmpty(results);
    }

    [Fact]
    public void DistinctWhereNotExists_KeepsTheDistinct()
    {
        var sql = _fixture.Connection.From<Category>()
            .Distinct()
            .WhereNotExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .ToSql();

        Assert.Contains("SELECT DISTINCT", sql);
        Assert.Contains("NOT EXISTS", sql);
    }

    [Fact]
    public void DistinctWhereInSubquery_Executes()
    {
        var results = Distinct()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                _fixture.Connection.From<Category>().Where(c => c.CategoryId == 1))
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.Equal((short?)1, p.CategoryId));
    }

    [Fact]
    public void DistinctWhereNotInSubquery_Executes()
    {
        var results = Distinct()
            .WhereNotInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                _fixture.Connection.From<Category>().Where(c => c.CategoryId == 1))
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.NotEqual((short?)1, p.CategoryId));
    }
}
