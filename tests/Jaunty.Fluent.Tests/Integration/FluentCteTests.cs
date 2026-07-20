using Jaunty.Fluent;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

using Xunit;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for Fluent CTE (Common Table Expression) support.
/// </summary>
public class FluentCteTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentCteTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    #region Basic CTE Tests

    [Fact]
    public void Cte_SimpleQuery_ReturnsResults()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("ExpensiveProducts")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .Select();

        // Assert
        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.UnitPrice > 50));
    }

    [Fact]
    public void Cte_ToSql_GeneratesCorrectSql()
    {
        // Arrange & Act
        var sql = _fixture.Connection.Cte<Product>("ExpensiveProducts")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .ToSql();

        // Assert
        Assert.StartsWith("WITH ExpensiveProducts AS (", sql);
        Assert.Contains("SELECT * FROM", sql);
        Assert.Contains("products", sql);
        Assert.Contains("unit_price", sql);
        Assert.Contains(") SELECT * FROM ExpensiveProducts", sql);
    }

    [Fact]
    public void Cte_WithAdditionalWhere_FiltersResults()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("FilteredProducts")
            .As(q => q.Where(p => p.UnitPrice > 20))
            .Where(p => p.CategoryId == 1)
            .Select();

        // Assert
        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.UnitPrice > 20 && p.CategoryId == 1));
    }

    [Fact]
    public void Cte_WithAdditionalWhere_ToSql_GeneratesCorrectSql()
    {
        // Act
        var sql = _fixture.Connection.Cte<Product>("FilteredProducts")
            .As(q => q.Where(p => p.UnitPrice > 20))
            .Where(p => p.CategoryId == 1)
            .ToSql();

        // Assert
        Assert.Contains("WITH FilteredProducts AS (", sql);
        Assert.Contains(") SELECT * FROM FilteredProducts WHERE", sql);
        Assert.Contains("category_id", sql);
    }

    #endregion

    #region CTE with Ordering and Pagination

    [Fact]
    public void Cte_WithOrderBy_ReturnsOrderedResults()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("SortedProducts")
            .As(q => q.Where(p => p.UnitPrice > 10))
            .OrderBy(p => p.ProductName)
            .Select();

        // Assert
        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) <= 0);
        }
    }

    [Fact]
    public void Cte_WithOrderByDescending_ReturnsDescendingResults()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("SortedProducts")
            .As(q => q.Where(p => p.UnitPrice > 10))
            .OrderByDescending(p => p.UnitPrice)
            .Select();

        // Assert
        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(products[i - 1].UnitPrice >= products[i].UnitPrice);
        }
    }

    [Fact]
    public void Cte_WithTake_LimitsResults()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("LimitedProducts")
            .As(q => q.Where(p => p.UnitPrice > 5))
            .Take(5)
            .Select();

        // Assert
        Assert.True(products.Count <= 5);
    }

    [Fact]
    public void Cte_WithSkipAndTake_PaginatesResults()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("PaginatedProducts")
            .As(q => q.Where(p => p.UnitPrice > 5))
            .OrderBy(p => p.ProductId)
            .Skip(2)
            .Take(3)
            .Select();

        // Assert
        Assert.True(products.Count <= 3);
    }

    #endregion

    #region CTE with And/Or

    [Fact]
    public void Cte_WithAndCondition_FiltersCorrectly()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("MultiFilter")
            .As(q => q.Where(p => p.UnitPrice > 10))
            .Where(p => p.CategoryId == 1)
            .And(p => p.UnitsInStock > 0)
            .Select();

        // Assert
        Assert.All(products, p =>
            Assert.True(p.UnitPrice > 10 && p.CategoryId == 1 && p.UnitsInStock > 0));
    }

    [Fact]
    public void Cte_WithOrCondition_FiltersCorrectly()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("OrFilter")
            .As(q => q.Where(p => p.UnitPrice > 100))
            .Where(p => p.CategoryId == 1)
            .Or(p => p.CategoryId == 2)
            .Select();

        // Assert
        Assert.All(products, p =>
            Assert.True(p.UnitPrice > 100 && (p.CategoryId == 1 || p.CategoryId == 2)));
    }

    [Fact]
    public void Cte_ChainedWithOrAnd_AppliesCorrectPrecedence()
    {
        // Chained: Where(...).Or(...).And(...) - regression test for AND/OR precedence
        // (round 10): must evaluate as (CategoryId == 1 OR CategoryId == 2) AND UnitPrice > 10,
        // not CategoryId == 1 OR (CategoryId == 2 AND UnitPrice > 10) per SQL's native
        // AND-before-OR precedence. Seed data has a CategoryId == 1 row with UnitPrice == 5,
        // which the buggy unparenthesized form would incorrectly let through.
        var products = _fixture.Connection.Cte<Product>("ChainedOrAnd")
            .As(q => q.Where(p => p.UnitsInStock >= 0))
            .Where(p => p.CategoryId == 1)
            .Or(p => p.CategoryId == 2)
            .And(p => p.UnitPrice > 10)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True((p.CategoryId == 1 || p.CategoryId == 2) && p.UnitPrice > 10));
    }

    #endregion

    #region CTE with SelectFirst/SelectFirstOrDefault

    [Fact]
    public void Cte_SelectFirst_ReturnsSingleResult()
    {
        // Arrange & Act
        var product = _fixture.Connection.Cte<Product>("FirstProduct")
            .As(q => q.Where(p => p.UnitPrice > 50))
            .OrderByDescending(p => p.UnitPrice)
            .SelectFirst();

        // Assert
        Assert.NotNull(product);
        Assert.True(product.UnitPrice > 50);
    }

    [Fact]
    public void Cte_SelectFirstOrDefault_WithNoResults_ReturnsNull()
    {
        // Arrange & Act
        var product = _fixture.Connection.Cte<Product>("NoProducts")
            .As(q => q.Where(p => p.UnitPrice > 999999))
            .SelectFirstOrDefault();

        // Assert
        Assert.Null(product);
    }

    #endregion

    #region CTE Async Tests

    [Fact]
    public async Task Cte_SelectAsync_ReturnsResults()
    {
        // Arrange & Act
        var products = await _fixture.Connection.Cte<Product>("AsyncProducts")
            .As(q => q.Where(p => p.UnitPrice > 30))
            .SelectAsync();

        // Assert
        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.UnitPrice > 30));
    }

    #endregion

    #region CTE with Column-based Where

    [Fact]
    public void Cte_WhereWithColumnName_FiltersCorrectly()
    {
        // Arrange & Act
        var products = _fixture.Connection.Cte<Product>("ColumnFilter")
            .As(q => q.Where(p => p.UnitPrice > 10))
            .Where("category_id", 1)
            .Select();

        // Assert
        Assert.All(products, p => Assert.True(p.UnitPrice > 10 && p.CategoryId == 1));
    }

    #endregion

    #region CTE with As(IWhereClause) Overload

    [Fact]
    public void Cte_AsIWhereClause_ReturnsResults()
    {
        // Arrange & Act: Use As with IWhereClause overload
        var products = _fixture.Connection.Cte<Product>("FilteredProducts")
            .As(_fixture.Connection.From<Product>().Where(p => p.UnitPrice > 30))
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.UnitPrice > 30));
    }

    [Fact]
    public void Cte_AsIWhereClause_ToSql_GeneratesCorrectSql()
    {
        // Act
        var sql = _fixture.Connection.Cte<Product>("FilteredProducts")
            .As(_fixture.Connection.From<Product>().Where(p => p.UnitPrice > 30))
            .ToSql();

        // Assert
        Assert.Contains("WITH FilteredProducts AS (", sql);
        Assert.Contains(") SELECT * FROM FilteredProducts", sql);
    }

    #endregion
}