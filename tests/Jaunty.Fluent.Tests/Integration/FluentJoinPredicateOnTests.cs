using System.Text.RegularExpressions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// CF-9 / AUD-R31-007: the 3-way and 4-way IJoinClause gained the predicate-expression
/// On(...) overload the 2-way interface already had. The parameter-renumbering tests are the
/// point of the file: each visitor mints its value parameters from a counter that restarts at
/// 0, so an ON predicate on the third join produces "@jp0" again after the second join or a
/// Where has already bound one.
/// </summary>
public class FluentJoinPredicateOnTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentJoinPredicateOnTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private static IReadOnlyList<string> BoundParameterNames(string sql)
        => Regex.Matches(sql, @"@jp\d+").Select(m => m.Value).Distinct().OrderBy(n => n).ToList();

    [Fact]
    public void ThreeWay_OnPredicate_MatchesTheKeyExpressionOverloadRowForRow()
    {
        var viaKeys = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c").On((p, c) => p.CategoryId == c.CategoryId)
            .InnerJoin<Supplier>("s").On(p => p.SupplierId, s => s.SupplierId)
            .Select();

        var viaPredicate = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c").On((p, c) => p.CategoryId == c.CategoryId)
            .InnerJoin<Supplier>("s").On((p, c, s) => p.SupplierId == s.SupplierId)
            .Select();

        Assert.NotEmpty(viaPredicate);
        Assert.Equal(
            viaKeys.Select(p => p.ProductId).OrderBy(id => id),
            viaPredicate.Select(p => p.ProductId).OrderBy(id => id));
    }

    [Fact]
    public void ThreeWay_OnPredicate_EmitsTheConditionIntoTheJoinNotTheWhere()
    {
        var sql = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c").On((p, c) => p.CategoryId == c.CategoryId)
            .InnerJoin<Supplier>("s").On((p, c, s) => p.SupplierId == s.SupplierId)
            .ToSql();

        int joinIndex = sql.IndexOf("JOIN suppliers", StringComparison.Ordinal);
        Assert.True(joinIndex >= 0, sql);
        Assert.Contains("s.supplier_id", sql.Substring(joinIndex), StringComparison.Ordinal);
        Assert.DoesNotContain("WHERE", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ThreeWay_OnPredicate_WithLiteral_AppliesTheExtraRestriction()
    {
        var all = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c").On((p, c) => p.CategoryId == c.CategoryId)
            .InnerJoin<Supplier>("s").On((p, c, s) => p.SupplierId == s.SupplierId)
            .Select();

        var restricted = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c").On((p, c) => p.CategoryId == c.CategoryId)
            .InnerJoin<Supplier>("s").On((p, c, s) => p.SupplierId == s.SupplierId && s.SupplierId == 1)
            .Select();

        Assert.NotEmpty(restricted);
        Assert.True(restricted.Count < all.Count, $"restricted {restricted.Count} vs all {all.Count}");
        Assert.All(restricted, p => Assert.Equal(1, p.SupplierId));
    }

    [Fact]
    public void ThreeWay_OnPredicate_AfterATwoWayOnPredicateWithItsOwnValue_BindsTwoDistinctParameters()
    {
        // Both visitors mint "@jp0" for their literal. Without renumbering against the
        // query-wide counter the second binding throws a duplicate-key ArgumentException, or one
        // bound value silently serves both conditions.
        var query = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c").On((p, c) => p.CategoryId == c.CategoryId && c.CategoryId > 0)
            .InnerJoin<Supplier>("s").On((p, c, s) => p.SupplierId == s.SupplierId && s.SupplierId == 1);

        Assert.Equal(["@jp0", "@jp1"], BoundParameterNames(query.ToSql()));

        var results = query.Select();
        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.Equal(1, p.SupplierId));
    }

    [Fact]
    public void ThreeWay_OnPredicate_ThenWhere_KeepsBothValuesDistinct()
    {
        var query = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c").On((p, c) => p.CategoryId == c.CategoryId)
            .InnerJoin<Supplier>("s").On((p, c, s) => p.SupplierId == s.SupplierId && s.SupplierId == 1)
            .Where((p, c, s) => p.UnitPrice > 5m);

        Assert.Equal(["@jp0", "@jp1"], BoundParameterNames(query.ToSql()));

        var results = query.Select();
        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.Equal(1, p.SupplierId));
        Assert.All(results, p => Assert.True(p.UnitPrice > 5m));
    }

    [Fact]
    public void FourWay_OnPredicate_MatchesTheColumnNameOverloadRowForRow()
    {
        var viaColumns = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c").On((p, c) => p.CategoryId == c.CategoryId)
            .InnerJoin<Supplier>("s").On((p, c, s) => p.SupplierId == s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>("o").On("p.supplier_id", "o.employee_id")
            .Select();

        var viaPredicate = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c").On((p, c) => p.CategoryId == c.CategoryId)
            .InnerJoin<Supplier>("s").On((p, c, s) => p.SupplierId == s.SupplierId)
            .InnerJoin<Product, Category, Supplier, Order>("o").On((p, c, s, o) => o.EmployeeId == p.SupplierId)
            .Select();

        Assert.NotEmpty(viaPredicate);
        Assert.Equal(
            viaColumns.Select(p => p.ProductId).OrderBy(id => id),
            viaPredicate.Select(p => p.ProductId).OrderBy(id => id));
    }

    [Fact]
    public void FourWay_OnPredicate_AfterEarlierPredicateValues_BindsThreeDistinctParameters()
    {
        var query = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c").On((p, c) => p.CategoryId == c.CategoryId && c.CategoryId > 0)
            .InnerJoin<Supplier>("s").On((p, c, s) => p.SupplierId == s.SupplierId && s.SupplierId == 1)
            .InnerJoin<Product, Category, Supplier, Order>("o")
                .On((p, c, s, o) => o.EmployeeId == p.SupplierId && o.EmployeeId == 1);

        Assert.Equal(["@jp0", "@jp1", "@jp2"], BoundParameterNames(query.ToSql()));

        var results = query.Select();
        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.Equal(1, p.SupplierId));
    }
}
