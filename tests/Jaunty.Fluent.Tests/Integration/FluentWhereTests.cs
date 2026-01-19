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
        // Act - use actual database column name (snake_case)
        var products = _db.Connection.From<Product>()
            .Where("category_id", 1)
            .Select();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1);
    }

    [Fact]
    public void Where_Expression_FiltersResults()
    {
        // Act
        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Select();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1);
    }

    [Fact]
    public void Where_And_ChainsConditions()
    {
        // Act
        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .And(p => p.Discontinued == false)
            .Select();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1 && p.Discontinued == false);
    }

    [Fact]
    public void Where_Or_ChainsConditions()
    {
        // Act
        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Or(p => p.CategoryId == 2)
            .Select();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1 || p.CategoryId == 2);
    }

    [Fact]
    public void Where_ComplexExpression_GeneratesCorrectSql()
    {
        // Act
        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1 && p.UnitPrice > 10)
            .ToSql();

        // Assert
        sql.Should().Contain("WHERE");
        sql.Should().Contain("category_id");
        sql.Should().Contain("unit_price");
    }

    [Fact]
    public void Where_NullComparison_GeneratesIsNull()
    {
        // Act
        var sql = _db.Connection.From<Product>()
            .Where(p => p.SupplierId == null)
            .ToSql();

        // Assert
        sql.Should().Contain("IS NULL");
    }
}
