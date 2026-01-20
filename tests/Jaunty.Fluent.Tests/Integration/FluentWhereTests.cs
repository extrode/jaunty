using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentWhereTests : IDisposable
{
    private readonly Database _db;

    public FluentWhereTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    [Fact]
    public void Where_StringColumn_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .Where("category_id", (short)1)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1);
    }

    [Fact]
    public void Where_Expression_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1);
    }

    [Fact]
    public void Where_And_ChainsConditions()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .And(p => p.Discontinued == false)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1 && p.Discontinued == false);
    }

    [Fact]
    public void Where_Or_ChainsConditions()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Or(p => p.CategoryId == 2)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1 || p.CategoryId == 2);
    }

    [Fact]
    public void Where_ComplexExpression_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1 && p.UnitPrice > 10)
            .ToSql();

        sql.Should().Contain("WHERE");
        sql.Should().Contain("category_id");
        sql.Should().Contain("unit_price");
    }

    [Fact]
    public void Where_NullComparison_GeneratesIsNull()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.SupplierId == null)
            .ToSql();

        sql.Should().Contain("IS NULL");
    }

    // --- String Method Tests ---

    [Fact]
    public void Where_Contains_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Contains("Chef"))
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName.Contains("Chef"));
    }

    [Fact]
    public void Where_StartsWith_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.StartsWith("Chef"))
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName.StartsWith("Chef"));
    }

    [Fact]
    public void Where_EndsWith_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.EndsWith("Syrup"))
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName.EndsWith("Syrup"));
    }

    [Fact]
    public void Where_StringEquals_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Equals("Chai"))
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName == "Chai");
    }

    [Fact]
    public void Where_StringEquals_OrdinalIgnoreCase_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Equals("chai", StringComparison.OrdinalIgnoreCase))
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName.Equals("Chai", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Where_Contains_ToSql_GeneratesLikePattern()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Contains("test"))
            .ToSql();

        // SQLite uses GLOB, others use LIKE
        (sql.Contains("LIKE") || sql.Contains("GLOB")).Should().BeTrue();
    }

    [Fact]
    public void Where_StartsWith_ToSql_GeneratesLikePattern()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.StartsWith("test"))
            .ToSql();

        // SQLite uses GLOB, others use LIKE
        (sql.Contains("LIKE") || sql.Contains("GLOB")).Should().BeTrue();
    }

    [Fact]
    public void Where_EndsWith_ToSql_GeneratesLikePattern()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.EndsWith("test"))
            .ToSql();

        // SQLite uses GLOB, others use LIKE
        (sql.Contains("LIKE") || sql.Contains("GLOB")).Should().BeTrue();
    }
}
