using System.Text.RegularExpressions;

using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Integration;

/// <summary>
/// AUD-R38-035: mixed set-operation chains were emitted flat, so INTERSECT bound tighter than the
/// UNION/EXCEPT before it on SQL Server, PostgreSQL and MySQL but not on SQLite.
/// </summary>
public class SetOperationFoldOrderTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public SetOperationFoldOrderTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private IQueryTerminal<Product> InCategory(int categoryId) =>
        _fixture.Connection.From<Product>().Where(p => p.CategoryId == categoryId);

    private static int Wraps(string sql) => Regex.Matches(sql, "jaunty_set").Count;

    [Fact]
    public void Union_ThenIntersect_FoldsTheUnionIntoADerivedTable()
    {
        string sql = _fixture.Connection.From<Product>().Where(p => p.CategoryId == 1)
            .Union(InCategory(2))
            .Intersect(InCategory(2))
            .ToSql();

        Assert.StartsWith("SELECT * FROM (SELECT ", sql, StringComparison.Ordinal);
        Assert.Matches(@" UNION SELECT .+\) \W?jaunty_set\W? INTERSECT SELECT ", sql);
        Assert.Equal(1, Wraps(sql));
    }

    [Fact]
    public void Union_ThenIntersect_ReturnsTheLeftToRightResult()
    {
        List<Product> results = _fixture.Connection.From<Product>().Where(p => p.CategoryId == 1)
            .Union(InCategory(2))
            .Intersect(InCategory(2))
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.True(p.CategoryId == 2));
    }

    [Fact]
    public void Except_ThenIntersect_IsAlsoFolded()
    {
        string sql = _fixture.Connection.From<Product>().Where(p => p.CategoryId == 1)
            .Except(InCategory(2))
            .Intersect(InCategory(1))
            .ToSql();

        Assert.Equal(1, Wraps(sql));
        Assert.NotEmpty(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 1)
            .Except(InCategory(2))
            .Intersect(InCategory(1))
            .Select());
    }

    [Fact]
    public void UnionAll_ThenIntersect_IsAlsoFolded()
    {
        string sql = _fixture.Connection.From<Product>().Where(p => p.CategoryId == 1)
            .UnionAll(InCategory(2))
            .Intersect(InCategory(2))
            .ToSql();

        Assert.Equal(1, Wraps(sql));
    }

    [Fact]
    public void Intersect_ThenUnion_StaysFlat()
    {
        string sql = _fixture.Connection.From<Product>().Where(p => p.CategoryId == 1)
            .Intersect(InCategory(1))
            .Union(InCategory(2))
            .ToSql();

        Assert.Equal(0, Wraps(sql));
        Assert.StartsWith("SELECT ", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT * FROM (", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Union_ThenTwoIntersects_WrapsOnce()
    {
        string sql = _fixture.Connection.From<Product>().Where(p => p.CategoryId == 1)
            .Union(InCategory(2))
            .Intersect(InCategory(2))
            .Intersect(InCategory(2))
            .ToSql();

        Assert.Equal(1, Wraps(sql));
    }

    [Fact]
    public void AlternatingChain_WrapsAtEachIntersectAfterAUnion_AndRuns()
    {
        ISetOperationClause<Product> chain = _fixture.Connection.From<Product>().Where(p => p.CategoryId == 1)
            .Union(InCategory(2))
            .Intersect(InCategory(2))
            .Union(InCategory(3))
            .Intersect(InCategory(3));

        Assert.Equal(2, Wraps(chain.ToSql()));

        List<Product> results = chain.Select();
        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.True(p.CategoryId == 3));
    }

    [Fact]
    public void FoldedChain_WithOrderingAndPaging_Runs()
    {
        List<Product> results = _fixture.Connection.From<Product>().Where(p => p.CategoryId == 1)
            .Union(InCategory(2))
            .Intersect(InCategory(2))
            .OrderBy(p => p.ProductName)
            .Take(2)
            .Select();

        Assert.Equal(2, results.Count);
        Assert.All(results, p => Assert.True(p.CategoryId == 2));
        Assert.True(string.CompareOrdinal(results[0].ProductName, results[1].ProductName) <= 0);
    }
}
