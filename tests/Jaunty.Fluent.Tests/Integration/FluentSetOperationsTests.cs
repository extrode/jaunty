using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for UNION, UNION ALL, EXCEPT, INTERSECT set operations.
/// </summary>
public class FluentSetOperationsTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentSetOperationsTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ==========================================
    // UNION Tests
    // ==========================================

    [Fact]
    public void Union_TwoQueries_ReturnsCombinedResultsWithoutDuplicates()
    {
        // Get products from category 1 UNION products from category 2
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Select();

        Assert.NotEmpty(results);
        // All results should be from category 1 or 2
        Assert.All(results, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2));
    }

    [Fact]
    public void Union_ToSql_GeneratesUnionKeyword()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .ToSql();

        Assert.Contains("UNION", sql);
        Assert.DoesNotContain("UNION ALL", sql);
    }

    [Fact]
    public void Union_WithOrderBy_OrdersEntireResult()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(string.Compare(results[i - 1].ProductName, results[i].ProductName) <= 0);
        }
    }

    [Fact]
    public void Union_WithOrderByDescending_OrdersEntireResultDescending()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderByDescending(p => p.ProductName)
            .Select();

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(string.Compare(results[i - 1].ProductName, results[i].ProductName) >= 0);
        }
    }

    [Fact]
    public void Union_WithTake_LimitsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Take(3)
            .Select();

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Union_ChainedMultipleTimes_CombinesAllQueries()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 3))
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2 || p.CategoryId == 3));
    }

    // ==========================================
    // UNION ALL Tests
    // ==========================================

    [Fact]
    public void UnionAll_TwoQueries_ReturnsCombinedResultsWithDuplicates()
    {
        // UNION ALL keeps duplicates (same products may appear multiple times)
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 1))
            .Select();

        // With UNION ALL on the same query, count should double
        var singleQueryCount = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Count();

        Assert.Equal(singleQueryCount * 2, results.Count);
    }

    [Fact]
    public void UnionAll_ToSql_GeneratesUnionAllKeyword()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .ToSql();

        Assert.Contains("UNION ALL", sql);
    }

    [Fact]
    public void UnionAll_WithOrderBy_OrdersEntireResult()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.UnitPrice)
            .Select();

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].UnitPrice <= results[i].UnitPrice);
        }
    }

    // ==========================================
    // EXCEPT Tests
    // ==========================================

    [Fact]
    public void Except_TwoQueries_ReturnsRowsNotInSecondQuery()
    {
        // Get products from category 1 that are NOT discontinued
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Except(_fixture.Connection.From<Product>().Where(p => p.Discontinued == true))
            .Select();

        Assert.NotEmpty(results);
        // All results should be from category 1 and not discontinued
        Assert.All(results, p => Assert.True(p.CategoryId == 1 && !p.Discontinued));
    }

    [Fact]
    public void Except_ToSql_GeneratesExceptKeyword()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Except(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .ToSql();

        Assert.Contains("EXCEPT", sql);
    }

    [Fact]
    public void Except_WithOrderBy_OrdersEntireResult()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Except(_fixture.Connection.From<Product>().Where(p => p.Discontinued == true))
            .OrderBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(string.Compare(results[i - 1].ProductName, results[i].ProductName) <= 0);
        }
    }

    // ==========================================
    // INTERSECT Tests
    // ==========================================

    [Fact]
    public void Intersect_TwoQueries_ReturnsOnlyCommonRows()
    {
        // Get products that are both in category 1 AND have low stock (< 50 units).
        // NOTE: reorder_level is never populated in the seed data (always NULL), so a
        // "ReorderLevel > 0" filter here would silently match zero rows; UnitsInStock is
        // used instead so the intersection actually has to filter real data.
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Intersect(_fixture.Connection.From<Product>().Where(p => p.UnitsInStock < 50))
            .Select();

        Assert.NotEmpty(results);
        // All results should be in category 1 AND have UnitsInStock < 50
        Assert.All(results, p => Assert.True(p.CategoryId == 1 && p.UnitsInStock < 50));
        // "Cheap Product" (category 1, stock 100) must be excluded - proves the intersect
        // actually filters rather than just returning the whole category-1 set.
        Assert.DoesNotContain(results, p => p.ProductId == 8);
    }

    [Fact]
    public void Intersect_ToSql_GeneratesIntersectKeyword()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Intersect(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 1))
            .ToSql();

        Assert.Contains("INTERSECT", sql);
    }

    [Fact]
    public void Intersect_WithOrderBy_OrdersEntireResult()
    {
        // Same non-vacuous filter as Intersect_TwoQueries_ReturnsOnlyCommonRows above -
        // reorder_level is always NULL in the seed data, so "ReorderLevel > 0" would
        // silently match zero rows and this test would pass over an empty result set.
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Intersect(_fixture.Connection.From<Product>().Where(p => p.UnitsInStock < 50))
            .OrderByDescending(p => p.ProductName)
            .Select();

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(string.Compare(results[i - 1].ProductName, results[i].ProductName) >= 0);
        }
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task Union_SelectAsync_ReturnsResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectAsync();

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task UnionAll_SelectAsync_ReturnsResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectAsync();

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task Except_SelectAsync_ReturnsResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Except(_fixture.Connection.From<Product>().Where(p => p.Discontinued == true))
            .SelectAsync();

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task Intersect_SelectAsync_ReturnsResults()
    {
        // Same non-vacuous filter as Intersect_TwoQueries_ReturnsOnlyCommonRows above -
        // reorder_level is always NULL in the seed data, so "ReorderLevel > 0" would
        // silently match zero rows and this test would pass over an empty result set.
        var results = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Intersect(_fixture.Connection.From<Product>().Where(p => p.UnitsInStock < 50))
            .SelectAsync();

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.True(p.CategoryId == 1 && p.UnitsInStock < 50));
    }

    // ==========================================
    // SelectFirst/SelectSingle Tests
    // ==========================================

    [Fact]
    public void Union_SelectFirst_ReturnsSingleResult()
    {
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectFirst();

        Assert.NotNull(result);
        Assert.True(result.CategoryId == 1 || result.CategoryId == 2);
    }

    [Fact]
    public void Union_SelectFirstOrDefault_ReturnsNullWhenNoResults()
    {
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == -999)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == -998))
            .SelectFirstOrDefault();

        Assert.Null(result);
    }

    // ==========================================
    // ThenBy Tests
    // ==========================================

    [Fact]
    public void Union_OrderByThenBy_OrdersByMultipleColumns()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.ProductName)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("category_id", sql);
        Assert.Contains("product_name", sql);
    }

    [Fact]
    public void Union_OrderByThenByDescending_OrdersCorrectly()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.CategoryId)
            .ThenByDescending(p => p.ProductName)
            .Select();

        Assert.NotEmpty(results);
    }

    // ==========================================
    // Skip/Take Tests
    // ==========================================

    [Fact]
    public void Union_SkipTake_PaginatesResults()
    {
        var allResults = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.ProductId)
            .Select();

        var pagedResults = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.ProductId)
            .Skip(2)
            .Take(3)
            .Select();

        Assert.Equal(3, pagedResults.Count);
        // Verify skip worked
        Assert.Equal(allResults[2].ProductId, pagedResults[0].ProductId);
    }

    // ==========================================
    // Mixed Operations Tests
    // ==========================================

    [Fact]
    public void Union_ThenExcept_ChainedOperations()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Except(_fixture.Connection.From<Product>().Where(p => p.Discontinued == true))
            .ToSql();

        Assert.Contains("UNION", sql);
        Assert.Contains("EXCEPT", sql);
    }

    [Fact]
    public void UnionAll_ThenIntersect_ChainedOperations()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Intersect(_fixture.Connection.From<Product>().Where(p => p.ReorderLevel > 0))
            .ToSql();

        Assert.Contains("UNION ALL", sql);
        Assert.Contains("INTERSECT", sql);
    }

    // ==========================================
    // Custom IQueryTerminal<T> implementations (non-QueryBuilder)
    // ==========================================

    [Fact]
    public void Union_CustomImplementationWithUnmergeableParameters_ThrowsNotSupportedException()
    {
        // AUD-R18: a custom IQueryTerminal<T> implementation whose ToSql() embeds a parameter
        // placeholder (@name) has no way for SetOperationBuilder to extract and merge that
        // parameter's value into the combined query, so it must fail loudly instead of
        // silently splicing in SQL with an unbound placeholder - mirrors
        // FluentSubqueryTests.WhereInSubquery_CustomImplementationWithUnmergeableParameters_ThrowsNotSupportedException.
        var other = new StubQueryTerminal<Product>(
            "SELECT * FROM products WHERE product_name = @name");

        var ex = Assert.Throws<NotSupportedException>(() =>
            _fixture.Connection.From<Product>()
                .Where(p => p.CategoryId == 1)
                .Union(other)
                .ToSql());

        Assert.Contains(nameof(Product), ex.Message);
    }

    [Fact]
    public void Union_CustomImplementationWithNoParameters_WorksCorrectly()
    {
        var other = new StubQueryTerminal<Product>("SELECT * FROM products WHERE category_id = 2");

        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(other)
            .ToSql();

        Assert.Contains("UNION", sql);
        Assert.Contains("SELECT * FROM products WHERE category_id = 2", sql);
    }

    // ==========================================
    // Operands/first query with pre-existing ORDER BY / Take / Skip (AUD-R21)
    // ==========================================

    [Fact]
    public void Union_OperandAlreadyHasOrderBy_ThrowsNotSupportedException()
    {
        // AUD-R21: an operand that already has its own ORDER BY applied would have that
        // ordering spliced verbatim into the middle of the combined statement instead of
        // applying to the combined result - reject it instead of emitting broken/misleading SQL.
        var orderedOperand = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 2)
            .OrderBy(p => p.ProductName);

        var ex = Assert.Throws<NotSupportedException>(() =>
            _fixture.Connection.From<Product>()
                .Where(p => p.CategoryId == 1)
                .Union(orderedOperand)
                .ToSql());

        Assert.Contains("OrderBy", ex.Message);
    }

    [Fact]
    public void Union_OperandAlreadyHasTake_ThrowsNotSupportedException()
    {
        var pagedOperand = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 2)
            .OrderBy(p => p.ProductId)
            .Take(5);

        var ex = Assert.Throws<NotSupportedException>(() =>
            _fixture.Connection.From<Product>()
                .Where(p => p.CategoryId == 1)
                .Union(pagedOperand)
                .ToSql());

        Assert.Contains("Take", ex.Message);
    }

    [Fact]
    public void Union_OperandCustomImplementationAlreadyHasOrderBy_ThrowsNotSupportedException()
    {
        var other = new StubQueryTerminal<Product>("SELECT * FROM products WHERE category_id = 2 ORDER BY product_name");

        var ex = Assert.Throws<NotSupportedException>(() =>
            _fixture.Connection.From<Product>()
                .Where(p => p.CategoryId == 1)
                .Union(other)
                .ToSql());

        Assert.Contains("OrderBy", ex.Message);
    }

    [Theory]
    [InlineData("SELECT * FROM products WHERE category_id = 2 LIMIT 5")]
    [InlineData("SELECT * FROM products WHERE category_id = 2 limit 5")]
    [InlineData("SELECT * FROM products WHERE category_id = 2 OFFSET 10 ROWS")]
    [InlineData("SELECT * FROM products WHERE category_id = 2 OFFSET 10 ROWS FETCH NEXT 5 ROWS ONLY")]
    [InlineData("SELECT TOP 5 * FROM products WHERE category_id = 2")]
    public void Union_OperandCustomImplementationAlreadyHasPagingWithoutOrderBy_ThrowsNotSupportedException(string sql)
    {
        // AUD-R31: the custom-terminal branch only searched for " ORDER BY ", so a custom
        // implementation that applied paging without ordering had its LIMIT/OFFSET/FETCH/TOP
        // spliced verbatim into the combined statement - the exact silent semantic corruption
        // this guard exists to prevent.
        var other = new StubQueryTerminal<Product>(sql);

        var ex = Assert.Throws<NotSupportedException>(() =>
            _fixture.Connection.From<Product>()
                .Where(p => p.CategoryId == 1)
                .Union(other)
                .ToSql());

        Assert.Contains("Take", ex.Message);
    }

    [Fact]
    public void Union_OperandCustomImplementationWithColumnNamedLikePagingKeyword_IsAccepted()
    {
        var other = new StubQueryTerminal<Product>("SELECT toplevel, limits FROM products WHERE category_id = 2");

        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(other)
            .ToSql();

        Assert.Contains("toplevel", sql);
    }

    [Fact]
    public void Union_FirstQueryAlreadyHasOrderBy_ThrowsNotSupportedException()
    {
        // The same guard applies to the first (left-hand) query in the chain: Union/UnionAll/
        // Except/Intersect are plain public methods on QueryBuilder<T>, reachable even after
        // OrderBy/Take/Skip has already been applied to that same builder instance.
        var firstQuery = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrderBy(p => p.ProductName);

        var ex = Assert.Throws<NotSupportedException>(() =>
            firstQuery.Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2)).ToSql());

        Assert.Contains("OrderBy", ex.Message);
    }

    [Fact]
    public void Union_OuterOrderByAfterUnion_StillWorksCorrectly()
    {
        // Confirms the fix doesn't break the documented/intended usage: OrderBy/Take/Skip on
        // the OUTER set-operation chain (after Union), applying to the combined result.
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.ProductName)
            .Take(5)
            .ToSql();

        Assert.Contains("UNION", sql);
        Assert.Contains("ORDER BY", sql);
    }
}