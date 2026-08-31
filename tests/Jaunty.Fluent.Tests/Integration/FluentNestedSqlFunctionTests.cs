using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// AUD-R35-064. The WHERE visitor's <c>TranslateArgumentToSql</c> handled a <c>Convert</c> wrapper
/// and a direct parameter member and sent everything else to <c>EvaluateExpression</c>, so a nested
/// <c>Sql.*</c> call - which still references the lambda parameter - reached
/// <c>Expression.Lambda(...).Compile()</c> and threw
/// <c>variable 'p' of type 'Product' referenced from scope ''</c>. The SELECT visitor's twin has
/// always recursed, so the same expression translated in a projection and crashed in a WHERE.
/// </summary>
public class FluentNestedSqlFunctionTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentNestedSqlFunctionTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------------
    // Nested string functions
    // ------------------------------------------------------------------

    [Fact]
    public void Upper_OfTrim_Translates()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => Sql.Upper(Sql.Trim(p.ProductName)) == "CHAI")
            .ToSql();

        Assert.Contains("UPPER", sql, StringComparison.Ordinal);
        Assert.Contains("TRIM", sql, StringComparison.Ordinal);
        Assert.Contains("product_name", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Upper_OfTrim_Executes()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Upper(Sql.Trim(p.ProductName)) == "CHAI")
            .Select();

        Assert.Single(products);
        Assert.Equal("Chai", products[0].ProductName);
    }

    [Fact]
    public void Length_OfTrim_Executes()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Length(Sql.Trim(p.ProductName)) == 4)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal(4, p.ProductName!.Trim().Length));
    }

    [Fact]
    public void ThreeDeep_Translates()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => Sql.Length(Sql.Upper(Sql.Trim(p.ProductName))) > 3)
            .ToSql();

        Assert.Contains("LENGTH", sql, StringComparison.Ordinal);
        Assert.Contains("UPPER", sql, StringComparison.Ordinal);
        Assert.Contains("TRIM", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Substring_OfUpper_Executes()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Substring(Sql.Upper(p.ProductName), 1, 4) == "CHAI")
            .Select();

        Assert.Single(products);
        Assert.Equal("Chai", products[0].ProductName);
    }

    // ------------------------------------------------------------------
    // Nested null-handling functions
    // ------------------------------------------------------------------

    [Fact]
    public void Length_OfCoalesce_Executes()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Length(Sql.Coalesce(p.ProductName, "")) == 4)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal(4, (p.ProductName ?? "").Length));
    }

    [Fact]
    public void Coalesce_OfUpper_Translates()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => Sql.Coalesce(Sql.Upper(p.ProductName), "X") == "CHAI")
            .ToSql();

        Assert.Contains("COALESCE", sql, StringComparison.Ordinal);
        Assert.Contains("UPPER", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// The <c>??</c> binary, which the SELECT side renders through the dialect's IS NULL form and
    /// the WHERE side did not recognise at all.
    /// </summary>
    [Fact]
    public void Upper_OfNullCoalescingOperator_Executes()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Upper(p.ProductName ?? "") == "CHAI")
            .Select();

        Assert.Single(products);
        Assert.Equal("Chai", products[0].ProductName);
    }

    // ------------------------------------------------------------------
    // The single-level forms still work exactly as before.
    // ------------------------------------------------------------------

    [Fact]
    public void Upper_OfAColumn_StillExecutes()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Upper(p.ProductName) == "CHAI")
            .Select();

        Assert.Single(products);
    }

    [Fact]
    public void Coalesce_WithAConstant_StillParameterisesTheConstant()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitsInStock, (short)0) > 10)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True((p.UnitsInStock ?? 0) > 10));
    }

    // ------------------------------------------------------------------
    // What is still not translatable now says so.
    // ------------------------------------------------------------------

    [Fact]
    public void AnUntranslatableParameterReferencingArgument_ThrowsNotSupported()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            _fixture.Connection.From<Product>()
                .Where(p => Sql.Upper(p.ProductName!.ToUpper()) == "CHAI")
                .ToSql());

        Assert.Contains("Sql.*", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("referenced from scope", ex.Message, StringComparison.Ordinal);
    }
}
