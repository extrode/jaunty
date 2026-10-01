using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Integration;

/// <summary>
/// AUD-R38-029: the WHERE fold parenthesised each step but not a raw operand, so a raw OR next to
/// another condition was regrouped by SQL's AND-before-OR precedence.
/// </summary>
public class RawWhereFoldParenthesesTests : IClassFixture<FluentDatabaseFixture>
{
    private const string RawOr = "category_id = 1 OR category_id = 2";

    private readonly FluentDatabaseFixture _fixture;

    public RawWhereFoldParenthesesTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void ARawOrAfterAnExpression_IsParenthesised()
    {
        string sql = _fixture.Connection.From<Product>()
            .Where(p => p.UnitPrice > 10)
            .AndRaw(RawOr)
            .ToSql();

        Assert.Contains($" AND ({RawOr}))", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ARawOrInFirstPosition_IsParenthesised()
    {
        string sql = _fixture.Connection.From<Product>()
            .WhereRaw(RawOr)
            .And(p => p.UnitPrice > 10)
            .ToSql();

        Assert.Contains($"WHERE (({RawOr}) AND ", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ASingleRawCondition_IsLeftAsWritten()
    {
        string sql = _fixture.Connection.From<Product>()
            .WhereRaw(RawOr)
            .ToSql();

        Assert.Contains($"WHERE {RawOr}", sql, StringComparison.Ordinal);
        Assert.DoesNotContain($"({RawOr})", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ARawOrAfterAnExpression_KeepsTheExpressionAppliedToEveryRow()
    {
        List<Product> products = _fixture.Connection.From<Product>()
            .Where(p => p.UnitPrice > 30)
            .AndRaw(RawOr)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
        {
            Assert.True(p.UnitPrice > 30, $"{p.ProductName} costs {p.UnitPrice}");
            Assert.InRange(p.CategoryId!.Value, 1, 2);
        });
    }

    [Fact]
    public void AJoinedRawOrAfterAnExpression_IsParenthesised()
    {
        const string joinedOr = "categories.category_name = 'A' OR categories.category_name = 'B'";

        string sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.UnitPrice > 10)
            .Where(joinedOr)
            .ToSql();

        Assert.Contains($" AND ({joinedOr}))", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AGroupedRawOrAfterAnExpression_IsParenthesised()
    {
        string sql = _fixture.Connection.From<Product>()
            .Where(p => p.UnitPrice > 10)
            .AndRaw(RawOr)
            .GroupBy(p => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Total = g.Count() });

        Assert.Contains($" AND ({RawOr}))", sql, StringComparison.Ordinal);
    }
}
