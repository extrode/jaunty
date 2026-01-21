using FluentAssertions;
using Jaunty.Fluent;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;
using Xunit;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for Fluent CTE (Common Table Expression) support.
/// </summary>
public class FluentCteTests : IDisposable
{
    private readonly Database _db;

    public FluentCteTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    #region Basic CTE Tests

    [Fact]
    public void Cte_SimpleQuery_ReturnsResults()
    {
        // Arrange & Act
        var products = _db.Connection.Cte<Product>("ExpensiveProducts")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .Select();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.UnitPrice > 50);
    }

    [Fact]
    public void Cte_ToSql_GeneratesCorrectSql()
    {
        // Arrange & Act
        var sql = _db.Connection.Cte<Product>("ExpensiveProducts")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .ToSql();

        // Assert
        sql.Should().StartWith("WITH ExpensiveProducts AS (");
        sql.Should().Contain("SELECT * FROM");
        sql.Should().Contain("products");
        sql.Should().Contain("unit_price");
        sql.Should().Contain(") SELECT * FROM ExpensiveProducts");
    }

    [Fact]
    public void Cte_WithAdditionalWhere_FiltersResults()
    {
        // Arrange & Act
        var products = _db.Connection.Cte<Product>("FilteredProducts")
            .As(q => q.Where(p => p.UnitPrice > 20))
            .Where(p => p.CategoryId == 1)
            .Select();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.UnitPrice > 20 && p.CategoryId == 1);
    }

    [Fact]
    public void Cte_WithAdditionalWhere_ToSql_GeneratesCorrectSql()
    {
        // Act
        var sql = _db.Connection.Cte<Product>("FilteredProducts")
            .As(q => q.Where(p => p.UnitPrice > 20))
            .Where(p => p.CategoryId == 1)
            .ToSql();

        // Assert
        sql.Should().Contain("WITH FilteredProducts AS (");
        sql.Should().Contain(") SELECT * FROM FilteredProducts WHERE");
        sql.Should().Contain("category_id");
    }

    #endregion

    #region CTE with Ordering and Pagination

    [Fact]
    public void Cte_WithOrderBy_ReturnsOrderedResults()
    {
        // Arrange & Act
        var products = _db.Connection.Cte<Product>("SortedProducts")
            .As(q => q.Where(p => p.UnitPrice > 10))
            .OrderBy(p => p.ProductName)
            .Select();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public void Cte_WithOrderByDescending_ReturnsDescendingResults()
    {
        // Arrange & Act
        var products = _db.Connection.Cte<Product>("SortedProducts")
            .As(q => q.Where(p => p.UnitPrice > 10))
            .OrderByDescending(p => p.UnitPrice)
            .Select();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().BeInDescendingOrder(p => p.UnitPrice);
    }

    [Fact]
    public void Cte_WithTake_LimitsResults()
    {
        // Arrange & Act
        var products = _db.Connection.Cte<Product>("LimitedProducts")
            .As(q => q.Where(p => p.UnitPrice > 5))
            .Take(5)
            .Select();

        // Assert
        products.Should().HaveCountLessThanOrEqualTo(5);
    }

    [Fact]
    public void Cte_WithSkipAndTake_PaginatesResults()
    {
        // Arrange & Act
        var products = _db.Connection.Cte<Product>("PaginatedProducts")
            .As(q => q.Where(p => p.UnitPrice > 5))
            .OrderBy(p => p.ProductId)
            .Skip(2)
            .Take(3)
            .Select();

        // Assert
        products.Should().HaveCountLessThanOrEqualTo(3);
    }

    #endregion

    #region CTE with And/Or

    [Fact]
    public void Cte_WithAndCondition_FiltersCorrectly()
    {
        // Arrange & Act
        var products = _db.Connection.Cte<Product>("MultiFilter")
            .As(q => q.Where(p => p.UnitPrice > 10))
            .Where(p => p.CategoryId == 1)
            .And(p => p.UnitsInStock > 0)
            .Select();

        // Assert
        products.Should().OnlyContain(p =>
            p.UnitPrice > 10 && p.CategoryId == 1 && p.UnitsInStock > 0);
    }

    [Fact]
    public void Cte_WithOrCondition_FiltersCorrectly()
    {
        // Arrange & Act
        var products = _db.Connection.Cte<Product>("OrFilter")
            .As(q => q.Where(p => p.UnitPrice > 100))
            .Where(p => p.CategoryId == 1)
            .Or(p => p.CategoryId == 2)
            .Select();

        // Assert
        products.Should().OnlyContain(p =>
            p.UnitPrice > 100 && (p.CategoryId == 1 || p.CategoryId == 2));
    }

    #endregion

    #region CTE with SelectFirst/SelectFirstOrDefault

    [Fact]
    public void Cte_SelectFirst_ReturnsSingleResult()
    {
        // Arrange & Act
        var product = _db.Connection.Cte<Product>("FirstProduct")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .OrderByDescending(p => p.UnitPrice)
            .SelectFirst();

        // Assert
        product.Should().NotBeNull();
        product.UnitPrice.Should().BeGreaterThan(50);
    }

    [Fact]
    public void Cte_SelectFirstOrDefault_WithNoResults_ReturnsNull()
    {
        // Arrange & Act
        var product = _db.Connection.Cte<Product>("NoProducts")
            .As(q => q.Where(p => p.UnitPrice > 999999))
            .SelectFirstOrDefault();

        // Assert
        product.Should().BeNull();
    }

    #endregion

    #region CTE Async Tests

    [Fact]
    public async Task Cte_SelectAsync_ReturnsResults()
    {
        // Arrange & Act
        var products = await _db.Connection.Cte<Product>("AsyncProducts")
            .As(q => q.Where(p => p.UnitPrice > 30))
            .SelectAsync();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.UnitPrice > 30);
    }

    #endregion

    #region CTE with Column-based Where

    [Fact]
    public void Cte_WhereWithColumnName_FiltersCorrectly()
    {
        // Arrange & Act
        var products = _db.Connection.Cte<Product>("ColumnFilter")
            .As(q => q.Where(p => p.UnitPrice > 10))
            .Where("category_id", 1)
            .Select();

        // Assert
        products.Should().OnlyContain(p => p.UnitPrice > 10 && p.CategoryId == 1);
    }

    #endregion
}
