using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for Fluent API WHERE clause functionality.
/// </summary>
public class FluentWhereTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentWhereTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void Where_StringColumn_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .Where("category_id", (short)1)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Fact]
    public void Where_Expression_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Fact]
    public void Where_And_ChainsConditions()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .And(p => p.Discontinued == false)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 && p.Discontinued == false));
    }

    [Fact]
    public void Where_StringColumn_ThenAndSameColumn_DoesNotThrowOnParameterCollision()
    {
        // Regression test: the string-column Where(column, value)/And(column, value) overloads
        // used to build their parameter name as an unconditional bare "@category_id", so using
        // both for the same column (e.g. built up conditionally by caller code) threw
        // ArgumentException("A parameter named '@category_id' has already been added.") from
        // ParameterCollection.Add instead of generating a valid (if unsatisfiable) query.
        var products = _fixture.Connection.From<Product>()
            .Where("category_id", (short)1)
            .And("category_id", (short)2)
            .Select();

        Assert.Empty(products);
    }

    [Fact]
    public void Where_Or_ChainsConditions()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Or(p => p.CategoryId == 2)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2));
    }

    [Fact]
    public void Where_ComplexExpression_GeneratesCorrectSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1 && p.UnitPrice > 10)
            .ToSql();

        Assert.Contains("WHERE", sql);
        Assert.Contains("category_id", sql);
        Assert.Contains("unit_price", sql);
    }

    [Fact]
    public void Where_NullComparison_GeneratesIsNull()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.SupplierId == null)
            .ToSql();

        Assert.Contains("IS NULL", sql);
    }

    // --- String Method Tests ---

    [Fact]
    public void Where_Contains_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.ProductName.Contains("Chef"))
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Matches(".*Chef.*", p.ProductName));
    }

    [Fact]
    public void Where_StartsWith_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.ProductName.StartsWith("Chef"))
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductName.StartsWith("Chef")));
    }

    [Fact]
    public void Where_EndsWith_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.ProductName.EndsWith("Syrup"))
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductName.EndsWith("Syrup")));
    }

    [Fact]
    public void Where_StringEquals_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.ProductName.Equals("Chai"))
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal("Chai", p.ProductName));
    }

    [Fact]
    public void Where_StringEquals_OrdinalIgnoreCase_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.ProductName.Equals("chai", StringComparison.OrdinalIgnoreCase))
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductName.Equals("Chai", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Where_Contains_ToSql_GeneratesLikePattern()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.ProductName.Contains("test"))
            .ToSql();

        // SQLite uses GLOB, others use LIKE
        Assert.True(sql.Contains("LIKE") || sql.Contains("GLOB"));
    }

    [Fact]
    public void Where_StartsWith_ToSql_GeneratesLikePattern()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.ProductName.StartsWith("test"))
            .ToSql();

        // SQLite uses GLOB, others use LIKE
        Assert.True(sql.Contains("LIKE") || sql.Contains("GLOB"));
    }

    [Fact]
    public void Where_EndsWith_ToSql_GeneratesLikePattern()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.ProductName.EndsWith("test"))
            .ToSql();

        // SQLite uses GLOB, others use LIKE
        Assert.True(sql.Contains("LIKE") || sql.Contains("GLOB"));
    }

    // --- WhereRaw Tests ---

    [Fact]
    public void WhereRaw_SimpleSql_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .WhereRaw("category_id = 1")
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Fact]
    public void WhereRaw_WithParameters_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .WhereRaw("category_id = @catId", new { catId = (short)1 })
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Fact]
    public void WhereRaw_ComplexCondition_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .WhereRaw("category_id IN (1, 2, 3)")
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2 || p.CategoryId == 3));
    }

    [Fact]
    public void WhereRaw_ToSql_GeneratesCorrectSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .WhereRaw("category_id = 1")
            .ToSql();

        Assert.Contains("WHERE", sql);
        Assert.Contains("category_id = 1", sql);
    }

    // --- AndRaw / OrRaw Tests ---

    [Fact]
    public void Where_AndRaw_ChainsConditions()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .AndRaw("discontinued = 0")
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 && p.Discontinued == false));
    }

    [Fact]
    public void Where_AndRaw_WithParameters_ChainsConditions()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .AndRaw("unit_price > @minPrice", new { minPrice = 10.0m })
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 && p.UnitPrice > 10.0m));
    }

    [Fact]
    public void Where_OrRaw_ChainsConditions()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrRaw("category_id = 2")
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2));
    }

    [Fact]
    public void Where_OrRaw_WithParameters_ChainsConditions()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrRaw("category_id = @catId", new { catId = (short)2 })
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2));
    }

    // --- Multiple Chain Tests ---

    [Fact]
    public void Where_MultipleAndOr_ChainsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .And(p => p.Discontinued == false)
            .And(p => p.UnitPrice > 5)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.CategoryId == 1 &&
            p.Discontinued == false &&
            p.UnitPrice > 5));
    }

    [Fact]
    public void Where_MultipleOr_ChainsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Or(p => p.CategoryId == 2)
            .Or(p => p.CategoryId == 3)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.CategoryId == 1 ||
            p.CategoryId == 2 ||
            p.CategoryId == 3));
    }

    [Fact]
    public void Where_MixedAndOrRaw_ChainsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .AndRaw("discontinued = 0")
            .OrRaw("category_id = 2")
            .Select();

        Assert.NotEmpty(products);
    }

    // --- Comparison Operators Tests ---

    [Fact]
    public void Where_GreaterThan_FiltersCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.UnitPrice > 20)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.UnitPrice > 20));
    }

    [Fact]
    public void Where_GreaterThanOrEqual_FiltersCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.UnitsInStock >= 50)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.UnitsInStock >= 50));
    }

    [Fact]
    public void Where_LessThan_FiltersCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.UnitPrice < 10)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.UnitPrice < 10));
    }

    [Fact]
    public void Where_LessThanOrEqual_FiltersCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.UnitsInStock <= 20)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.UnitsInStock <= 20));
    }

    [Fact]
    public void Where_NotEqual_FiltersCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId != 1)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId != 1));
    }

    // --- Boolean Tests ---

    [Fact]
    public void Where_BooleanTrue_FiltersCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == true)
            .Select();

        // May or may not have discontinued products
        Assert.All(products, p => Assert.True(p.Discontinued == true));
    }

    [Fact]
    public void Where_BooleanFalse_FiltersCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.Discontinued == false));
    }

    // --- Async Tests ---

    [Fact]
    public async Task Where_SelectAsync_FiltersCorrectly()
    {
        var products = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .SelectAsync();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Fact]
    public async Task Where_CountAsync_ReturnsFilteredCount()
    {
        var count = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .CountAsync();

        Assert.True(count > 0);
    }

    [Fact]
    public async Task WhereRaw_SelectAsync_FiltersCorrectly()
    {
        var products = await _fixture.Connection.From<Product>()
            .WhereRaw("category_id = 1")
            .SelectAsync();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    // --- Complex Nested Predicate Tests ---

    [Fact]
    public void Where_NestedAndOr_CombinesCorrectly()
    {
        // (CategoryId == 1 OR CategoryId == 2) AND Discontinued == false
        var products = _fixture.Connection.From<Product>()
            .Where(p => (p.CategoryId == 1 || p.CategoryId == 2) && p.Discontinued == false)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True((p.CategoryId == 1 || p.CategoryId == 2) && p.Discontinued == false));
    }

    [Fact]
    public void Where_DeeplyNested_CombinesCorrectly()
    {
        // ((CategoryId == 1 AND UnitPrice > 10) OR (CategoryId == 2 AND UnitPrice < 50)) AND Discontinued == false
        var products = _fixture.Connection.From<Product>()
            .Where(p => ((p.CategoryId == 1 && p.UnitPrice > 10) || (p.CategoryId == 2 && p.UnitPrice < 50)) && p.Discontinued == false)
            .Select();

        Assert.NotEmpty(products);
        // Verify the filter was applied
        Assert.All(products, p => Assert.True(p.Discontinued == false));
    }

    [Fact]
    public void Where_MultipleOrConditions_FiltersCorrectly()
    {
        // CategoryId == 1 OR CategoryId == 2 OR CategoryId == 3
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1 || p.CategoryId == 2 || p.CategoryId == 3)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId >= 1 && p.CategoryId <= 3));
    }

    [Fact]
    public void Where_MixedComparisons_FiltersCorrectly()
    {
        // CategoryId == 1 AND UnitPrice > 10 AND SupplierId != null
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1 && p.UnitPrice > 10 && p.SupplierId != null)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 && p.UnitPrice > 10 && p.SupplierId != null));
    }

    [Fact]
    public void Where_NotCondition_FiltersCorrectly()
    {
        // NOT (Discontinued == true)
        var products = _fixture.Connection.From<Product>()
            .Where(p => !(p.Discontinued == true))
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(!p.Discontinued));
    }

    [Fact]
    public void Where_ComplexWithNullCheck_FiltersCorrectly()
    {
        // (SupplierId == null OR SupplierId == 1) AND CategoryId == 1
        var products = _fixture.Connection.From<Product>()
            .Where(p => (p.SupplierId == null || p.SupplierId == 1) && p.CategoryId == 1)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True((p.SupplierId == null || p.SupplierId == 1) && p.CategoryId == 1));
    }

    [Fact]
    public void Where_ChainedWithAndOr_CombinesCorrectly()
    {
        // Chained: Where(...).And(...).Or(...)
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .And(p => p.UnitPrice > 10)
            .Or(p => p.CategoryId == 2)
            .Select();

        Assert.NotEmpty(products);
        // Verify results match the complex condition: (CategoryId == 1 AND UnitPrice > 10) OR CategoryId == 2
        Assert.All(products, p =>
            Assert.True((p.CategoryId == 1 && p.UnitPrice > 10) || p.CategoryId == 2));
    }

    [Fact]
    public void Where_MultipleStringConditions_CombinesCorrectly()
    {
        // ProductName.Contains("Chef") OR ProductName.Contains("Grand")
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.ProductName.Contains("Chef"))
            .Or(p => p.ProductName.Contains("Grand"))
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.ProductName.Contains("Chef") || p.ProductName.Contains("Grand")));
    }
}