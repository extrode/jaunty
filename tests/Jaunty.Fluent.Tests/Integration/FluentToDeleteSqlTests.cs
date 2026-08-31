using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// AUD-R35-214: <c>IWhereClause&lt;T&gt;.ToDeleteSql()</c> - previewing the DELETE a chain would run,
/// where the inherited <c>ToSql()</c> returns the SELECT.
/// </summary>
public class FluentToDeleteSqlTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentToDeleteSqlTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private IWhereClause<Product> Discontinued()
        => _fixture.Connection.From<Product>().Where(p => p.Discontinued == true);

    [Fact]
    public void ToDeleteSql_EmitsADeleteAgainstTheTable()
    {
        var sql = Discontinued().ToDeleteSql();

        Assert.StartsWith("DELETE FROM", sql);
        Assert.Contains("products", sql);
        Assert.DoesNotContain("SELECT", sql);
    }

    [Fact]
    public void ToDeleteSql_CarriesTheWhereConditions()
    {
        var sql = Discontinued().ToDeleteSql();

        Assert.Contains("WHERE", sql);
        Assert.Contains("discontinued", sql);
    }

    [Fact]
    public void ToDeleteSql_CarriesEveryConditionOfAChain()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .And(p => p.UnitPrice > 10)
            .Or(p => p.Discontinued == true)
            .ToDeleteSql();

        Assert.Contains("category_id", sql);
        Assert.Contains("unit_price", sql);
        Assert.Contains("discontinued", sql);
        Assert.Contains(" AND ", sql);
        Assert.Contains(" OR ", sql);
    }

    [Fact]
    public void ToDeleteSql_LeavesParameterPlaceholdersInPlace()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .ToDeleteSql();

        Assert.Contains("@", sql);
        Assert.DoesNotContain("= 1", sql);
    }

    [Fact]
    public void ToSql_OnTheSameChain_StillReturnsTheSelect()
    {
        var clause = Discontinued();

        Assert.StartsWith("SELECT", clause.ToSql());
        Assert.StartsWith("DELETE FROM", clause.ToDeleteSql());
    }

    [Fact]
    public void ToDeleteSql_IsRepeatableAndDoesNotExecute()
    {
        var before = _fixture.Connection.From<Product>().Where(p => p.Discontinued == true).Count();

        var clause = Discontinued();
        var first = clause.ToDeleteSql();
        var second = clause.ToDeleteSql();

        var after = _fixture.Connection.From<Product>().Where(p => p.Discontinued == true).Count();

        Assert.Equal(first, second);
        Assert.Equal(before, after);
    }

    [Fact]
    public void ToDeleteSql_CarriesNoOrderByOrPaging()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == true)
            .ToDeleteSql();

        Assert.DoesNotContain("ORDER BY", sql);
        Assert.DoesNotContain("LIMIT", sql);
    }

    [Fact]
    public void ToDeleteSql_ReportsAnAliasCorrelationBeforeTheDeleteRuns()
    {
        var act = () => _fixture.Connection.From<Product>("p")
            .WhereExists<Category>((p, c) => p.CategoryId == c.CategoryId)
            .ToDeleteSql();

        var ex = Assert.Throws<NotSupportedException>(act);
        Assert.Contains("DELETE", ex.Message);
    }
}
