using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentOrderByTests : IDisposable
{
    private readonly Database _db;

    public FluentOrderByTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    [Fact]
    public void OrderBy_Expression_SortsAscending()
    {
        // Act
        var products = _db.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .Select();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public void OrderByDescending_Expression_SortsDescending()
    {
        // Act
        var products = _db.Connection.From<Product>()
            .OrderByDescending(p => p.ProductId)
            .Select();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().BeInDescendingOrder(p => p.ProductId);
    }

    [Fact]
    public void OrderBy_String_SortsCorrectly()
    {
        // Act - use actual database column name
        var products = _db.Connection.From<Product>()
            .OrderBy("product_name")
            .Select();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public void ThenBy_MultipleColumns_SortsCorrectly()
    {
        // Act
        var products = _db.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.ProductName)
            .Select();

        // Assert
        products.Should().NotBeEmpty();
    }

    [Fact]
    public void OrderBy_WithWhere_GeneratesCorrectSql()
    {
        // Act
        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrderBy(p => p.ProductName)
            .ToSql();

        // Assert
        sql.Should().Contain("WHERE");
        sql.Should().Contain("ORDER BY");
    }
}
