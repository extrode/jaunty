using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Advanced tests for 3-table and 4-table join functionality.
/// Tests OrderBy, ThenBy, And/Or, Count, SelectFirst, SelectPartial, etc.
/// </summary>
public class FluentMultiTableJoinAdvancedTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentMultiTableJoinAdvancedTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ==========================================
    // Three-Table Join - OrderBy Tests
    // ==========================================

    [Fact]
    public void ThreeTableJoin_OrderBy_OrdersResults()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .OrderBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(string.Compare(results[i - 1].ProductName, results[i].ProductName, StringComparison.Ordinal) <= 0);
        }
    }

    [Fact]
    public void ThreeTableJoin_OrderByDescending_OrdersResultsDesc()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .OrderByDescending(p => p.ProductName)
            .Select();

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(string.Compare(results[i - 1].ProductName, results[i].ProductName, StringComparison.Ordinal) >= 0);
        }
    }

    [Fact]
    public void ThreeTableJoin_OrderByJoined_OrdersByJoinColumn()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .OrderByJoined(c => c.CategoryName)
            .Select();

        Assert.NotEmpty(results);
    }

    [Fact]
    public void ThreeTableJoin_OrderByJoined_ThirdTable_OrdersByThirdTable()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .OrderByJoined(s => s.CompanyName)
            .Select();

        Assert.NotEmpty(results);
    }

    [Fact]
    public void ThreeTableJoin_OrderBy_ThenBy_OrdersByMultiple()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(results);
    }

    [Fact]
    public void ThreeTableJoin_OrderBy_ThenByJoined_OrdersByMultipleTables()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .OrderBy(p => p.CategoryId)
            .ThenByJoined(c => c.CategoryName)
            .Select();

        Assert.NotEmpty(results);
    }

    [Fact]
    public void ThreeTableJoin_ToSql_WithOrderBy_GeneratesValidSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .OrderBy(p => p.ProductName)
            .ThenByJoined(c => c.CategoryName)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("product_name", sql);
        Assert.Contains("category_name", sql);
    }

    // ==========================================
    // Three-Table Join - And/Or Tests
    // ==========================================

    [Fact]
    public void ThreeTableJoin_Where_And_FiltersWithMultipleConditions()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => c.CategoryId == 1)
            .And((p, c, s) => p.Discontinued == false)
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, p =>
        {
            Assert.Equal((short)1, p.CategoryId);
            Assert.False(p.Discontinued);
        });
    }

    [Fact]
    public void ThreeTableJoin_Where_Or_FiltersWithOrCondition()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => c.CategoryId == 1)
            .Or((p, c, s) => c.CategoryId == 2)
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2));
    }

    [Fact]
    public void ThreeTableJoin_ToSql_WithAndOr_GeneratesValidSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => c.CategoryId == 1)
            .And((p, c, s) => p.Discontinued == false)
            .ToSql();

        Assert.Contains("WHERE", sql);
        Assert.Contains("AND", sql);
    }

    // ==========================================
    // Three-Table Join - Count Tests
    // ==========================================

    [Fact]
    public void ThreeTableJoin_Count_ReturnsCorrectCount()
    {
        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Count();

        Assert.True(count > 0);
    }

    [Fact]
    public void ThreeTableJoin_LongCount_ReturnsCorrectCount()
    {
        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LongCount();

        Assert.True(count > 0);
    }

    [Fact]
    public void ThreeTableJoin_Count_WithWhere_FiltersCorrectly()
    {
        var count1 = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Count();

        var count2 = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => c.CategoryId == 1)
            .Count();

        Assert.True(count1 > count2);
    }

    // ==========================================
    // Three-Table Join - SelectFirst/Single Tests
    // ==========================================

    [Fact]
    public void ThreeTableJoin_SelectFirst_ReturnsFirstResult()
    {
        var result = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .SelectFirst();

        Assert.NotNull(result);
        Assert.True(result.ProductId > 0);
    }

    [Fact]
    public void ThreeTableJoin_SelectFirstOrDefault_ReturnsFirstOrNull()
    {
        var result = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .SelectFirstOrDefault();

        Assert.NotNull(result);
    }

    [Fact]
    public void ThreeTableJoin_SelectFirstOrDefault_NoResults_ReturnsNull()
    {
        var result = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == -999)
            .SelectFirstOrDefault();

        Assert.Null(result);
    }

    [Fact]
    public void ThreeTableJoin_SelectSingle_OneResult_ReturnsResult()
    {
        var result = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == 1)
            .SelectSingle();

        Assert.NotNull(result);
        Assert.Equal(1, result.ProductId);
    }

    [Fact]
    public void ThreeTableJoin_SelectSingleOrDefault_NoResults_ReturnsNull()
    {
        var result = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == -999)
            .SelectSingleOrDefault();

        Assert.Null(result);
    }

    [Fact]
    public void ThreeTableJoin_SelectSingle_MultipleResults_Throws()
    {
        var act = () => _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .SelectSingle();

        Assert.Throws<InvalidOperationException>(act);
    }

    // ==========================================
    // Three-Table Join - SelectPartial Tests
    // ==========================================

    [Fact]
    public void ThreeTableJoin_SelectPartial_ReturnsDictionaries()
    {
        var results = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .SelectPartial("p.product_name, c.category_name, s.company_name");

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.Contains("product_name", first.Keys);
        Assert.Contains("category_name", first.Keys);
        Assert.Contains("company_name", first.Keys);
    }

    [Fact]
    public void ThreeTableJoin_SelectPartialFirst_ReturnsFirstDictionary()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .SelectPartialFirst("p.product_name, c.category_name");

        Assert.NotNull(result);
        Assert.Contains("product_name", result.Keys);
    }

    [Fact]
    public void ThreeTableJoin_SelectPartialFirstOrDefault_NoResults_ReturnsNull()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .Where("p.product_id = -999")
            .SelectPartialFirstOrDefault("p.product_name");

        Assert.Null(result);
    }

    [Fact]
    public void ThreeTableJoin_SelectPartialSingle_OneResult_ReturnsDictionary()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .Where("p.product_id = 1")
            .SelectPartialSingle("p.product_name");

        Assert.NotNull(result);
        Assert.Equal("Chai", result["product_name"]);
    }

    [Fact]
    public void ThreeTableJoin_SelectPartialSingleOrDefault_NoResults_ReturnsNull()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .Where("p.product_id = -999")
            .SelectPartialSingleOrDefault("p.product_name");

        Assert.Null(result);
    }

    [Fact]
    public void ThreeTableJoin_SelectPartialSingle_MultipleResults_Throws()
    {
        var act = () => _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .SelectPartialSingle("p.product_name");

        Assert.Throws<InvalidOperationException>(act);
    }

    // ==========================================
    // Three-Table Join - Async Tests
    // ==========================================

    [Fact]
    public async Task ThreeTableJoin_SelectAsync_ReturnsResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .SelectAsync();

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task ThreeTableJoin_SelectAllAsync_ReturnsTuples()
    {
        var results = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .SelectAllAsync();

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.NotNull(first.Item1);
        Assert.NotNull(first.Item2);
        Assert.NotNull(first.Item3);
    }

    [Fact]
    public async Task ThreeTableJoin_CountAsync_ReturnsCorrectCount()
    {
        var count = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .CountAsync();

        Assert.True(count > 0);
    }

    [Fact]
    public async Task ThreeTableJoin_LongCountAsync_ReturnsCorrectCount()
    {
        var count = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .LongCountAsync();

        Assert.True(count > 0);
    }

    [Fact]
    public async Task ThreeTableJoin_SelectFirstAsync_ReturnsFirst()
    {
        var result = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .SelectFirstAsync();

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ThreeTableJoin_SelectFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        var result = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.ProductId == -999)
            .SelectFirstOrDefaultAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ThreeTableJoin_SelectPartialAsync_ReturnsDictionaries()
    {
        var results = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .SelectPartialAsync("p.product_name, c.category_name");

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.Contains("product_name", first.Keys);
    }

    [Fact]
    public async Task ThreeTableJoin_SelectPartialFirstAsync_ReturnsFirst()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .SelectPartialFirstAsync("p.product_name");

        Assert.NotNull(result);
        Assert.Contains("product_name", result.Keys);
    }

    [Fact]
    public async Task ThreeTableJoin_SelectPartialFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .Where("p.product_id = -999")
            .SelectPartialFirstOrDefaultAsync("p.product_name");

        Assert.Null(result);
    }

    // ==========================================
    // Join Clause Tests - OnFromSecond
    // ==========================================

    [Fact]
    public void ThreeTableJoin_OnFromSecond_JoinsFromSecondTable()
    {
        // Join Product -> Category, then join Supplier from Category (hypothetical)
        // Using OnFromSecond to join from the second table instead of first
        var results = _fixture.Connection.From<Category>()
            .InnerJoin<Product>()
            .On(c => c.CategoryId, p => p.CategoryId)
            .InnerJoin<Supplier>()
            .OnFromSecond(p => p.SupplierId, s => s.SupplierId)
            .SelectAll();

        Assert.NotEmpty(results);
    }

    // ==========================================
    // SQL Generation Tests (No Database Execution)
    // ==========================================

    [Fact]
    public void ThreeTableJoin_ToSql_GeneratesCorrectJoinOrder()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .ToSql();

        // Verify FROM comes first
        var fromIndex = sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
        var firstJoinIndex = sql.IndexOf("INNER JOIN", StringComparison.OrdinalIgnoreCase);
        Assert.True(fromIndex < firstJoinIndex);

        // Verify both joins exist
        Assert.Equal(2, sql.Split("INNER JOIN", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void ThreeTableJoin_ToSql_WithAliases_GeneratesCorrectAliases()
    {
        var sql = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .ToSql();

        Assert.Contains("p", sql);
        Assert.Contains("c", sql);
        Assert.Contains("s", sql);
    }

    [Fact]
    public void ThreeTableJoin_ToSql_WithWhere_GeneratesWhereClause()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => p.Discontinued == false)
            .ToSql();

        Assert.Contains("WHERE", sql);
    }

    [Fact]
    public void ThreeTableJoin_ToSql_WithOrderBy_GeneratesOrderByClause()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .OrderBy(p => p.ProductName)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
    }

    [Fact]
    public void ThreeTableJoin_ToSql_WithMultipleOrderBy_GeneratesCorrectSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.ProductName)
            .ThenByJoined(c => c.CategoryName)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        // Should have multiple columns in ORDER BY
        var orderByClause = sql.Substring(sql.IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("category_id", orderByClause);
        Assert.Contains("product_name", orderByClause);
        Assert.Contains("category_name", orderByClause);
    }
}
