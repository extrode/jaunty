using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Integration;

/// <summary>
/// AUD-R38-034: an IN-subquery operand's own ORDER BY and paging were spliced inside IN (...).
/// </summary>
public class InSubqueryOperandOrderingTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public InSubqueryOperandOrderingTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private IWhereClause<Category> Categories() => _fixture.Connection.From<Category>().Where(c => c.CategoryId > 0);

    [Fact]
    public void WhereInSubquery_WithAnOrderedButUnpagedOperand_IsRefused()
    {
        var ex = Assert.Throws<NotSupportedException>(() => _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(p => p.CategoryId!.Value, c => c.CategoryId,
                Categories().OrderBy(c => c.CategoryName)));

        Assert.StartsWith("WhereInSubquery/WhereNotInSubquery must not be given", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WhereNotInSubquery_WithAnOrderedButUnpagedOperand_IsRefused()
    {
        Assert.Throws<NotSupportedException>(() => _fixture.Connection.From<Product>()
            .WhereNotInSubquery<int, Category>(p => p.CategoryId!.Value, c => c.CategoryId,
                Categories().OrderBy(c => c.CategoryName)));
    }

    [Fact]
    public void WhereInSubquery_WithAPagedOperand_WrapsItInADerivedTable()
    {
        string sql = _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(p => p.CategoryId!.Value, c => c.CategoryId,
                Categories().OrderBy(c => c.CategoryName).Take(2))
            .ToSql();

        Assert.Matches(@" IN \(SELECT \S+ FROM \(SELECT .+ ORDER BY .+\) jaunty_in\)", sql);
    }

    [Fact]
    public void WhereInSubquery_WithATakeOperand_KeepsOnlyTheFirstRows()
    {
        List<int> firstTwo = Categories().OrderBy(c => c.CategoryName).Take(2).Select().Select(c => c.CategoryId).ToList();

        List<Product> products = _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(p => p.CategoryId!.Value, c => c.CategoryId,
                Categories().OrderBy(c => c.CategoryName).Take(2))
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Contains(p.CategoryId!.Value, firstTwo));
    }

    [Fact]
    public void WhereNotInSubquery_WithASkipOperand_ExcludesOnlyTheRemainingRows()
    {
        List<int> afterFirst = Categories().OrderBy(c => c.CategoryName).Skip(1).Select().Select(c => c.CategoryId).ToList();

        List<Product> products = _fixture.Connection.From<Product>()
            .WhereNotInSubquery<int, Category>(p => p.CategoryId!.Value, c => c.CategoryId,
                Categories().OrderBy(c => c.CategoryName).Skip(1))
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.DoesNotContain(p.CategoryId!.Value, afterFirst));
    }

    [Fact]
    public void WhereInSubquery_WithAPlainOperand_IsNotWrapped()
    {
        string sql = _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(p => p.CategoryId!.Value, c => c.CategoryId, Categories())
            .ToSql();

        Assert.Contains(" IN (SELECT ", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("jaunty_in", sql, StringComparison.Ordinal);
    }
}
