using System.Linq.Expressions;

using Jaunty.Fluent;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Additional tests for QueryBuilder covering uncovered methods:
/// DeleteAll, DeleteAllAsync, UpdateAll, UpdateAllAsync,
/// Update/Delete validation, WhereRaw And/Or chaining,
/// string-based Set/Where for updates, string-based OrderBy after Where/Distinct,
/// SelectPartialSingle, SelectPartialSingleOrDefault (sync + async + expression),
/// Distinct with Where, LongCount, Or(string), And(string) with null values,
/// SelectX alias methods.
/// </summary>
public class FluentQueryBuilderAdvancedTests : IDisposable
{
    private readonly InMemoryDatabase _db;

    public FluentQueryBuilderAdvancedTests()
    {
        _db = new InMemoryDatabase();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ==========================================
    // DeleteAll / DeleteAllAsync
    // ==========================================

    [Fact]
    public void DeleteAll_DeletesAllRows()
    {
        // Verify we have data first
        var countBefore = _db.Connection.From<Product>().Count();
        Assert.True(countBefore > 0);

        var deleted = _db.Connection.From<Product>().DeleteAll();

        Assert.Equal(countBefore, deleted);

        var countAfter = _db.Connection.From<Product>().Count();
        Assert.Equal(0, countAfter);
    }

    [Fact]
    public async Task DeleteAllAsync_DeletesAllRows()
    {
        var countBefore = _db.Connection.From<Product>().Count();
        Assert.True(countBefore > 0);

        var deleted = await _db.Connection.From<Product>().DeleteAllAsync();

        Assert.Equal(countBefore, deleted);

        var countAfter = _db.Connection.From<Product>().Count();
        Assert.Equal(0, countAfter);
    }

    // ==========================================
    // UpdateAll / UpdateAllAsync
    // ==========================================

    [Fact]
    public void UpdateAll_UpdatesAllRows()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)99)
            .UpdateAll();

        Assert.True(rowsUpdated > 0);

        var products = _db.Connection.From<Product>().Select();
        Assert.All(products, p => Assert.Equal((short)99, p.ReorderLevel));
    }

    [Fact]
    public async Task UpdateAllAsync_UpdatesAllRows()
    {
        var rowsUpdated = await _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)88)
            .UpdateAllAsync();

        Assert.True(rowsUpdated > 0);

        var products = _db.Connection.From<Product>().Select();
        Assert.All(products, p => Assert.Equal((short)88, p.ReorderLevel));
    }

    // ==========================================
    // Update with string-based Set/Where
    // ==========================================

    [Fact]
    public void Update_SetStringColumn_UpdatesCorrectly()
    {
        var rowsUpdated = ((ISetClause<Product>)_db.Connection.From<Product>()
            .Set("unit_price", 111.11m))
            .Where(p => p.ProductId == 1)
            .Update();

        Assert.Equal(1, rowsUpdated);
        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == 1)
            .SelectFirst();
        Assert.Equal(111.11m, product.UnitPrice);
    }

    [Fact]
    public void Update_SetStringColumnChained_UpdatesCorrectly()
    {
        var rowsUpdated = ((ISetClause<Product>)_db.Connection.From<Product>()
            .Set("unit_price", 222.22m))
            .Set("reorder_level", (short)42)
            .Where(p => p.ProductId == 1)
            .Update();

        Assert.Equal(1, rowsUpdated);
        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == 1)
            .SelectFirst();
        Assert.Equal(222.22m, product.UnitPrice);
        Assert.Equal((short)42, product.ReorderLevel);
    }

    [Fact]
    public void Update_WhereStringColumn_UpdatesCorrectly()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 333.33m)
            .Where("product_id", 1)
            .Update();

        Assert.Equal(1, rowsUpdated);
        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == 1)
            .SelectFirst();
        Assert.Equal(333.33m, product.UnitPrice);
    }

    [Fact]
    public void Update_WhereRaw_UpdatesCorrectly()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 444.44m)
            .WhereRaw("product_id = 1")
            .Update();

        Assert.Equal(1, rowsUpdated);
    }

    [Fact]
    public void Update_WhereRawWithParameters_UpdatesCorrectly()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 555.55m)
            .WhereRaw("product_id = @pid", new { pid = 1 })
            .Update();

        Assert.Equal(1, rowsUpdated);
    }

    [Fact]
    public void Update_ToSql_WhereClause_ReturnsCorrectSql()
    {
        var sql = _db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 100m)
            .Where(p => p.ProductId == 1)
            .ToSql();

        Assert.Contains("UPDATE", sql);
        Assert.Contains("SET", sql);
        Assert.Contains("WHERE", sql);
    }

    // ==========================================
    // Update And/Or chaining on IUpdateWhereClause
    // ==========================================

    [Fact]
    public void Update_WhereAndString_UpdatesWithMultipleConditions()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)77)
            .Where("category_id", 1)
            .And("product_name", "Chai")
            .Update();

        Assert.Equal(1, rowsUpdated);
    }

    [Fact]
    public void Update_WhereAndExpression_UpdatesWithMultipleConditions()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)66)
            .Where(p => p.CategoryId == 1)
            .And(p => p.ProductName == "Chai")
            .Update();

        Assert.Equal(1, rowsUpdated);
    }

    [Fact]
    public void Update_WhereAndRaw_UpdatesWithMultipleConditions()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)55)
            .Where(p => p.CategoryId == 1)
            .AndRaw("product_name = 'Chai'")
            .Update();

        Assert.Equal(1, rowsUpdated);
    }

    [Fact]
    public void Update_WhereAndRawWithParams_UpdatesWithMultipleConditions()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)44)
            .Where(p => p.CategoryId == 1)
            .AndRaw("product_name = @name", new { name = "Chai" })
            .Update();

        Assert.Equal(1, rowsUpdated);
    }

    [Fact]
    public void Update_WhereOrString_UpdatesWithOrCondition()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)33)
            .Where("product_name", "Chai")
            .Or("product_name", "Chang")
            .Update();

        Assert.Equal(2, rowsUpdated);
    }

    [Fact]
    public void Update_WhereOrExpression_UpdatesMatchingRows()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)22)
            .Where(p => p.ProductName == "Chai")
            .Or(p => p.CategoryId == 2)
            .Update();

        // Chai (category 1) + Aniseed Syrup (category 2) = 2
        Assert.Equal(2, rowsUpdated);
    }

    [Fact]
    public void Update_WhereOrRaw_UpdatesWithOrCondition()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)11)
            .Where(p => p.ProductName == "Chai")
            .OrRaw("product_name = 'Chang'")
            .Update();

        Assert.Equal(2, rowsUpdated);
    }

    [Fact]
    public void Update_WhereOrRawWithParams_UpdatesWithOrCondition()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)5)
            .Where(p => p.ProductName == "Chai")
            .OrRaw("product_name = @name", new { name = "Chang" })
            .Update();

        Assert.Equal(2, rowsUpdated);
    }

    [Fact]
    public void Update_WhereAndNull_GeneratesIsNull()
    {
        var sql = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)0)
            .Where("category_id", 1)
            .And("quantity_per_unit", null)
            .ToSql();

        Assert.Contains("IS NULL", sql);
    }

    [Fact]
    public void Update_WhereOrNull_GeneratesIsNull()
    {
        var sql = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)0)
            .Where("category_id", 1)
            .Or("quantity_per_unit", null)
            .ToSql();

        Assert.Contains("IS NULL", sql);
        Assert.Contains("OR", sql);
    }

    [Fact]
    public void Update_WhereStringNull_GeneratesIsNull()
    {
        var sql = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)0)
            .Where("quantity_per_unit", null)
            .ToSql();

        Assert.Contains("IS NULL", sql);
    }

    // ==========================================
    // Update AndIn / AndNotIn / OrIn / OrNotIn
    // ==========================================

    [Fact]
    public void Update_WhereAndIn_UpdatesMatchingRows()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)99)
            .Where(p => p.CategoryId == 1)
            .AndIn(p => p.ProductName, new[] { "Chai", "Chang" })
            .Update();

        Assert.Equal(2, rowsUpdated);
    }

    [Fact]
    public void Update_WhereAndNotIn_UpdatesMatchingRows()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)88)
            .Where(p => p.CategoryId == 1)
            .AndNotIn(p => p.ProductName, new[] { "Chai" })
            .Update();

        Assert.Equal(1, rowsUpdated); // Only "Chang" in category 1
    }

    [Fact]
    public void Update_WhereOrIn_UpdatesMatchingRows()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)77)
            .Where(p => p.ProductName == "Chai")
            .OrIn(p => p.ProductName, new[] { "Aniseed Syrup" })
            .Update();

        Assert.Equal(2, rowsUpdated);
    }

    [Fact]
    public void Update_WhereOrNotIn_UpdatesMatchingRows()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.ReorderLevel, (short)66)
            .Where(p => p.ProductName == "Chai")
            .OrNotIn(p => p.ProductName, new[] { "Chai", "Chang" })
            .Update();

        Assert.Equal(2, rowsUpdated); // Chai + Aniseed Syrup
    }

    // ==========================================
    // Set(object) chaining from ISetClause
    // ==========================================

    [Fact]
    public void Update_SetAnonymousObject_FromISetClause_UpdatesCorrectly()
    {
        var rowsUpdated = _db.Connection.From<Product>()
            .Set(p => p.UnitPrice, 10m)
            .Set(new { ReorderLevel = (short)42 })
            .Where(p => p.ProductId == 1)
            .Update();

        Assert.Equal(1, rowsUpdated);
        var product = _db.Connection.From<Product>()
            .Where(p => p.ProductId == 1)
            .SelectFirst();
        Assert.Equal(10m, product.UnitPrice);
        Assert.Equal((short)42, product.ReorderLevel);
    }
}

/// <summary>
/// Query-focused tests for QueryBuilder methods using the shared fixture (read-only).
/// These cover: string-based OrderBy, Distinct ordering, LongCount,
/// SelectPartialSingle/SelectPartialSingleOrDefault, And/Or with string columns,
/// and SelectX aliases.
/// </summary>
public class FluentQueryBuilderReadOnlyTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentQueryBuilderReadOnlyTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ==========================================
    // String-based OrderBy after Where
    // ==========================================

    [Fact]
    public void Where_OrderByString_OrdersByRawColumn()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrderBy("product_name")
            .Select();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) <= 0);
        }
    }

    [Fact]
    public void Where_OrderByDescendingString_OrdersByRawColumnDesc()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrderByDescending("product_name")
            .Select();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) >= 0);
        }
    }

    [Fact]
    public void From_OrderByString_OrdersByRawColumn()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderBy("product_name")
            .Select();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) <= 0);
        }
    }

    [Fact]
    public void From_OrderByDescendingString_OrdersByRawColumnDesc()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderByDescending("product_name")
            .Select();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) >= 0);
        }
    }

    // ==========================================
    // ThenBy / ThenByDescending with string
    // ==========================================

    [Fact]
    public void OrderBy_ThenByString_OrdersByMultipleColumns()
    {
        var sql = _fixture.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenBy("product_name")
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("product_name", sql);
    }

    [Fact]
    public void OrderBy_ThenByDescendingString_OrdersByMultipleColumns()
    {
        var sql = _fixture.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenByDescending("unit_price")
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("unit_price", sql);
        Assert.Contains("DESC", sql);
    }

    // ==========================================
    // Distinct with OrderBy
    // ==========================================

    [Fact]
    public void Distinct_OrderByString_OrdersByRawColumn()
    {
        var sql = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderBy("product_name")
            .ToSql();

        Assert.Contains("DISTINCT", sql);
        Assert.Contains("ORDER BY", sql);
    }

    [Fact]
    public void Distinct_OrderByDescendingString_OrdersByRawColumnDesc()
    {
        var sql = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderByDescending("product_name")
            .ToSql();

        Assert.Contains("DISTINCT", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("DESC", sql);
    }

    [Fact]
    public void Distinct_OrderByExpression_OrdersByColumn()
    {
        var sql = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderBy(p => p.ProductName)
            .ToSql();

        Assert.Contains("DISTINCT", sql);
        Assert.Contains("ORDER BY", sql);
    }

    [Fact]
    public void Distinct_OrderByDescendingExpression_OrdersByColumnDesc()
    {
        var sql = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderByDescending(p => p.UnitPrice)
            .ToSql();

        Assert.Contains("DISTINCT", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("DESC", sql);
    }

    [Fact]
    public void Distinct_Where_FiltersDistinctResults()
    {
        var products = _fixture.Connection.From<Product>()
            .Distinct()
            .Where(p => p.CategoryId == 1)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Fact]
    public void Distinct_WhereString_FiltersDistinctResults()
    {
        var products = _fixture.Connection.From<Product>()
            .Distinct()
            .Where("category_id", 1)
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void Distinct_TakeSkip_PaginatesDistinctResults()
    {
        var allDistinct = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderBy(p => p.ProductId)
            .Select();

        var firstPage = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderBy(p => p.ProductId)
            .Take(3)
            .Select();

        var pagedSkip = _fixture.Connection.From<Product>()
            .Distinct()
            .OrderBy(p => p.ProductId)
            .Skip(3)
            .Take(3)
            .Select();

        Assert.Equal(3, firstPage.Count);
        Assert.Equal(3, pagedSkip.Count);
        Assert.NotEqual(firstPage.First().ProductId, pagedSkip.First().ProductId);
        Assert.Equal(allDistinct[3].ProductId, pagedSkip.First().ProductId);
    }

    [Fact]
    public void Distinct_Skip_SkipsDistinctResults()
    {
        var sql = _fixture.Connection.From<Product>()
            .Distinct()
            .Skip(5)
            .ToSql();

        Assert.Contains("DISTINCT", sql);
    }

    // ==========================================
    // LongCount
    // ==========================================

    [Fact]
    public void LongCount_ReturnsProductCount()
    {
        var count = _fixture.Connection.From<Product>().LongCount();

        Assert.True(count > 0);
    }

    [Fact]
    public void LongCount_WithSelector_CountsNonNullValues()
    {
        var count = _fixture.Connection.From<Product>()
            .LongCount(p => p.SupplierId);

        Assert.True(count > 0);
    }

    [Fact]
    public async Task LongCountAsync_ReturnsProductCount()
    {
        var count = await _fixture.Connection.From<Product>().LongCountAsync();

        Assert.True(count > 0);
    }

    [Fact]
    public async Task LongCountAsync_WithSelector_CountsNonNullValues()
    {
        var count = await _fixture.Connection.From<Product>()
            .LongCountAsync(p => p.SupplierId);

        Assert.True(count > 0);
    }

    // ==========================================
    // Count with Selector (sync)
    // ==========================================

    [Fact]
    public void Count_WithSelector_CountsNonNullValues()
    {
        var count = _fixture.Connection.From<Product>()
            .Count(p => p.SupplierId);

        Assert.True(count > 0);
    }

    // ==========================================
    // SelectPartialSingle / SelectPartialSingleOrDefault (string columns)
    // ==========================================

    [Fact]
    public void SelectPartialSingle_StringColumns_ReturnsExactlyOne()
    {
        var first = _fixture.Connection.From<Product>().SelectFirst();

        var product = _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == first.ProductId)
            .SelectPartialSingle("product_id", "product_name");

        Assert.NotNull(product);
        Assert.Equal(first.ProductId, product.ProductId);
    }

    [Fact]
    public void SelectPartialSingleOrDefault_StringColumns_NoMatch_ReturnsNull()
    {
        var product = _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectPartialSingleOrDefault("product_id", "product_name");

        Assert.Null(product);
    }

    [Fact]
    public void SelectPartialSingle_StringColumns_MultipleMatches_Throws()
    {
        var act = () => _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .SelectPartialSingle("product_id");

        Assert.Throws<InvalidOperationException>(act);
    }

    // ==========================================
    // SelectPartialSingle / SelectPartialSingleOrDefault (expression columns)
    // ==========================================

    [Fact]
    public void SelectPartialSingle_ExpressionColumns_ReturnsExactlyOne()
    {
        var first = _fixture.Connection.From<Product>().SelectFirst();

        var product = _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == first.ProductId)
            .SelectPartialSingle(p => p.ProductId, p => p.ProductName);

        Assert.NotNull(product);
        Assert.Equal(first.ProductId, product.ProductId);
    }

    [Fact]
    public void SelectPartialSingleOrDefault_ExpressionColumns_NoMatch_ReturnsNull()
    {
        var product = _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectPartialSingleOrDefault(p => p.ProductId);

        Assert.Null(product);
    }

    // ==========================================
    // SelectPartialFirstOrDefault (string) with no match
    // ==========================================

    [Fact]
    public void SelectPartialFirstOrDefault_StringColumns_NoMatch_ReturnsNull()
    {
        var product = _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectPartialFirstOrDefault("product_id", "product_name");

        Assert.Null(product);
    }

    // ==========================================
    // Async Partial Variants (expressions)
    // ==========================================

    [Fact]
    public async Task SelectPartialFirstAsync_Expression_ReturnsProduct()
    {
        var product = await _fixture.Connection.From<Product>()
            .SelectPartialFirstAsync(new Expression<Func<Product, object?>>[]
            {
                p => p.ProductId,
                p => p.ProductName
            });

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }

    [Fact]
    public async Task SelectPartialFirstOrDefaultAsync_Expression_NoMatch_ReturnsNull()
    {
        var product = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectPartialFirstOrDefaultAsync(new Expression<Func<Product, object?>>[]
            {
                p => p.ProductId
            });

        Assert.Null(product);
    }

    [Fact]
    public async Task SelectPartialSingleAsync_Expression_ReturnsExactlyOne()
    {
        var first = _fixture.Connection.From<Product>().SelectFirst();

        var product = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == first.ProductId)
            .SelectPartialSingleAsync(new Expression<Func<Product, object?>>[]
            {
                p => p.ProductId,
                p => p.ProductName
            });

        Assert.NotNull(product);
        Assert.Equal(first.ProductId, product.ProductId);
    }

    [Fact]
    public async Task SelectPartialSingleOrDefaultAsync_Expression_NoMatch_ReturnsNull()
    {
        var product = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectPartialSingleOrDefaultAsync(new Expression<Func<Product, object?>>[]
            {
                p => p.ProductId
            });

        Assert.Null(product);
    }

    // ==========================================
    // Async Partial Variants (string columns)
    // ==========================================

    [Fact]
    public async Task SelectPartialFirstAsync_String_ReturnsProduct()
    {
        var product = await _fixture.Connection.From<Product>()
            .SelectPartialFirstAsync(new[] { "product_id", "product_name" });

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }

    [Fact]
    public async Task SelectPartialFirstOrDefaultAsync_String_NoMatch_ReturnsNull()
    {
        var product = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectPartialFirstOrDefaultAsync(new[] { "product_id" });

        Assert.Null(product);
    }

    [Fact]
    public async Task SelectPartialSingleAsync_String_ReturnsExactlyOne()
    {
        var first = _fixture.Connection.From<Product>().SelectFirst();

        var product = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == first.ProductId)
            .SelectPartialSingleAsync(new[] { "product_id", "product_name" });

        Assert.NotNull(product);
        Assert.Equal(first.ProductId, product.ProductId);
    }

    [Fact]
    public async Task SelectPartialSingleOrDefaultAsync_String_NoMatch_ReturnsNull()
    {
        var product = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectPartialSingleOrDefaultAsync(new[] { "product_id" });

        Assert.Null(product);
    }

    // ==========================================
    // SelectSingleOrDefaultAsync
    // ==========================================

    [Fact]
    public async Task SelectSingleOrDefaultAsync_NoMatch_ReturnsNull()
    {
        var product = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectSingleOrDefaultAsync();

        Assert.Null(product);
    }

    // ==========================================
    // Where(string, object) / And(string, object) / Or(string, object)
    // ==========================================

    [Fact]
    public void Where_StringColumn_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .Where("category_id", 1)
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void Where_StringColumnNull_GeneratesIsNull()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where("supplier_id", null)
            .ToSql();

        Assert.Contains("IS NULL", sql);
    }

    [Fact]
    public void And_StringColumn_AddsAndCondition()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .And("discontinued", 0)
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void And_StringColumnNull_GeneratesIsNull()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .And("supplier_id", null)
            .ToSql();

        Assert.Contains("IS NULL", sql);
        Assert.Contains("AND", sql);
    }

    [Fact]
    public void Or_StringColumn_AddsOrCondition()
    {
        var products = _fixture.Connection.From<Product>()
            .Where("category_id", 1)
            .Or("category_id", 2)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2));
    }

    [Fact]
    public void Or_StringColumnNull_GeneratesIsNull()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Or("supplier_id", null)
            .ToSql();

        Assert.Contains("IS NULL", sql);
        Assert.Contains("OR", sql);
    }

    // ==========================================
    // WhereRaw And/Or chaining
    // ==========================================

    [Fact]
    public void WhereRaw_And_ChainsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .WhereRaw("category_id = 1")
            .And(p => p.Discontinued == false)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Fact]
    public void AndRaw_ChainsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .AndRaw("discontinued = 0")
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void AndRaw_WithParameters_ChainsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .AndRaw("product_name = @name", new { name = "Chai" })
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void OrRaw_ChainsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrRaw("category_id = 2")
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2));
    }

    [Fact]
    public void OrRaw_WithParameters_ChainsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrRaw("category_id = @catId", new { catId = (short)2 })
            .Select();

        Assert.NotEmpty(products);
    }

    // ==========================================
    // Or expression
    // ==========================================

    [Fact]
    public void Or_Expression_FiltersWithOrCondition()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Or(p => p.CategoryId == 2)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2));
    }

    // ==========================================
    // SelectX Alias Methods
    // ==========================================

    [Fact]
    public void SelectCount_WithSelector_IsSameAsCount()
    {
        var count = _fixture.Connection.From<Product>().Count(p => p.SupplierId);
        var selectCount = _fixture.Connection.From<Product>().SelectCount(p => p.SupplierId);

        Assert.Equal(count, selectCount);
    }

    [Fact]
    public void SelectSum_IsSameAsSum()
    {
        var sum = _fixture.Connection.From<Product>().Sum(p => p.SupplierId);
        var selectSum = _fixture.Connection.From<Product>().SelectSum(p => p.SupplierId);

        Assert.Equal(sum, selectSum);
    }

    [Fact]
    public void SelectAvg_IsSameAsAvg()
    {
        var avg = _fixture.Connection.From<Product>().Avg(p => p.SupplierId);
        var selectAvg = _fixture.Connection.From<Product>().SelectAvg(p => p.SupplierId);

        Assert.Equal(avg, selectAvg);
    }

    [Fact]
    public void SelectMin_IsSameAsMin()
    {
        var min = _fixture.Connection.From<Product>().Min(p => p.SupplierId);
        var selectMin = _fixture.Connection.From<Product>().SelectMin(p => p.SupplierId);

        Assert.Equal(min, selectMin);
    }

    [Fact]
    public void SelectMax_IsSameAsMax()
    {
        var max = _fixture.Connection.From<Product>().Max(p => p.SupplierId);
        var selectMax = _fixture.Connection.From<Product>().SelectMax(p => p.SupplierId);

        Assert.Equal(max, selectMax);
    }

    // ==========================================
    // Async SelectX Alias Methods
    // ==========================================

    [Fact]
    public async Task SelectCountAsync_WithSelector_IsSameAsCountAsync()
    {
        var count = await _fixture.Connection.From<Product>().CountAsync(p => p.SupplierId);
        var selectCount = await _fixture.Connection.From<Product>().SelectCountAsync(p => p.SupplierId);

        Assert.Equal(count, selectCount);
    }

    [Fact]
    public async Task SelectSumAsync_IsSameAsSumAsync()
    {
        var sum = await _fixture.Connection.From<Product>().SumAsync(p => p.SupplierId);
        var selectSum = await _fixture.Connection.From<Product>().SelectSumAsync(p => p.SupplierId);

        Assert.Equal(sum, selectSum);
    }

    [Fact]
    public async Task SelectAvgAsync_IsSameAsAvgAsync()
    {
        var avg = await _fixture.Connection.From<Product>().AvgAsync(p => p.SupplierId);
        var selectAvg = await _fixture.Connection.From<Product>().SelectAvgAsync(p => p.SupplierId);

        Assert.Equal(avg, selectAvg);
    }

    [Fact]
    public async Task SelectMinAsync_IsSameAsMinAsync()
    {
        var min = await _fixture.Connection.From<Product>().MinAsync(p => p.SupplierId);
        var selectMin = await _fixture.Connection.From<Product>().SelectMinAsync(p => p.SupplierId);

        Assert.Equal(min, selectMin);
    }

    [Fact]
    public async Task SelectMaxAsync_IsSameAsMaxAsync()
    {
        var max = await _fixture.Connection.From<Product>().MaxAsync(p => p.SupplierId);
        var selectMax = await _fixture.Connection.From<Product>().SelectMaxAsync(p => p.SupplierId);

        Assert.Equal(max, selectMax);
    }

    // ==========================================
    // From Skip/Take (without Where)
    // ==========================================

    [Fact]
    public void From_Take_LimitsResults()
    {
        var products = _fixture.Connection.From<Product>()
            .Take(5)
            .Select();

        Assert.True(products.Count <= 5);
    }

    [Fact]
    public void From_Skip_SkipsResults()
    {
        var allProducts = _fixture.Connection.From<Product>().Select();
        var skipped = _fixture.Connection.From<Product>()
            .Skip(2)
            .Select();

        Assert.Equal(allProducts.Count - 2, skipped.Count);
    }

    // ==========================================
    // OrderBy Take/Skip
    // ==========================================

    [Fact]
    public void OrderBy_Take_LimitsOrderedResults()
    {
        var products = _fixture.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .Take(5)
            .Select();

        Assert.True(products.Count <= 5);
    }

    [Fact]
    public void OrderBy_Skip_SkipsOrderedResults()
    {
        var sql = _fixture.Connection.From<Product>()
            .OrderBy(p => p.ProductName)
            .Skip(5)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
    }

    // ==========================================
    // QueryBuilder SelectPartial* Methods Coverage
    // ==========================================

    [Fact]
    public void From_SelectPartial_WithStringColumns_ReturnsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .SelectPartial("product_id", "product_name");

        Assert.NotEmpty(results);
    }

    [Fact]
    public void From_SelectPartialFirst_WithStringColumns_ReturnsFirst()
    {
        var result = _fixture.Connection.From<Product>()
            .SelectPartialFirst("product_id", "product_name");

        Assert.NotNull(result);
    }

    [Fact]
    public void From_SelectPartialFirstOrDefault_WithStringColumns_ReturnsFirstOrDefault()
    {
        var result = _fixture.Connection.From<Product>()
            .SelectPartialFirstOrDefault("product_id", "product_name");

        Assert.NotNull(result);
    }

    [Fact]
    public void From_SelectPartialSingle_WithStringColumns_ReturnsSingle()
    {
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == 1)
            .SelectPartialSingle("product_id", "product_name");

        Assert.NotNull(result);
    }

    [Fact]
    public void From_SelectPartialSingleOrDefault_WithStringColumns_NoMatch_ReturnsNull()
    {
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectPartialSingleOrDefault("product_id", "product_name");

        Assert.Null(result);
    }

    [Fact]
    public void From_SelectPartial_WithExpressionColumns_ReturnsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .SelectPartial(p => p.ProductId, p => p.ProductName);

        Assert.NotEmpty(results);
    }

    [Fact]
    public void From_SelectPartialFirst_WithExpressionColumns_ReturnsFirst()
    {
        var result = _fixture.Connection.From<Product>()
            .SelectPartialFirst(p => p.ProductId, p => p.ProductName);

        Assert.NotNull(result);
    }

    [Fact]
    public void From_SelectPartialFirstOrDefault_WithExpressionColumns_ReturnsFirstOrDefault()
    {
        var result = _fixture.Connection.From<Product>()
            .SelectPartialFirstOrDefault(p => p.ProductId, p => p.ProductName);

        Assert.NotNull(result);
    }

    [Fact]
    public void From_SelectPartialSingle_WithExpressionColumns_ReturnsSingle()
    {
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == 1)
            .SelectPartialSingle(p => p.ProductId, p => p.ProductName);

        Assert.NotNull(result);
    }

    [Fact]
    public void From_SelectPartialSingleOrDefault_WithExpressionColumns_NoMatch_ReturnsNull()
    {
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectPartialSingleOrDefault(p => p.ProductId, p => p.ProductName);

        Assert.Null(result);
    }

    [Fact]
    public async Task From_SelectPartialAsync_WithStringColumns_ReturnsResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .SelectPartialAsync(new[] { "product_id", "product_name" });

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task From_SelectPartialFirstAsync_WithStringColumns_ReturnsFirst()
    {
        var result = await _fixture.Connection.From<Product>()
            .SelectPartialFirstAsync(new[] { "product_id", "product_name" });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task From_SelectPartialFirstOrDefaultAsync_WithStringColumns_ReturnsFirstOrDefault()
    {
        var result = await _fixture.Connection.From<Product>()
            .SelectPartialFirstOrDefaultAsync(new[] { "product_id", "product_name" });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task From_SelectPartialSingleAsync_WithStringColumns_ReturnsSingle()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == 1)
            .SelectPartialSingleAsync(new[] { "product_id", "product_name" });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task From_SelectPartialSingleOrDefaultAsync_WithStringColumns_NoMatch_ReturnsNull()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectPartialSingleOrDefaultAsync(new[] { "product_id", "product_name" });

        Assert.Null(result);
    }

    [Fact]
    public async Task From_SelectPartialAsync_WithExpressionColumns_ReturnsResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .SelectPartialAsync(new Expression<Func<Product, object?>>[]
            {
                p => p.ProductId,
                p => p.ProductName
            });

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task From_SelectPartialFirstAsync_WithExpressionColumns_ReturnsFirst()
    {
        var result = await _fixture.Connection.From<Product>()
            .SelectPartialFirstAsync(new Expression<Func<Product, object?>>[]
            {
                p => p.ProductId,
                p => p.ProductName
            });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task From_SelectPartialFirstOrDefaultAsync_WithExpressionColumns_ReturnsFirstOrDefault()
    {
        var result = await _fixture.Connection.From<Product>()
            .SelectPartialFirstOrDefaultAsync(new Expression<Func<Product, object?>>[]
            {
                p => p.ProductId,
                p => p.ProductName
            });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task From_SelectPartialSingleAsync_WithExpressionColumns_ReturnsSingle()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == 1)
            .SelectPartialSingleAsync(new Expression<Func<Product, object?>>[]
            {
                p => p.ProductId,
                p => p.ProductName
            });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task From_SelectPartialSingleOrDefaultAsync_WithExpressionColumns_NoMatch_ReturnsNull()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .SelectPartialSingleOrDefaultAsync(new Expression<Func<Product, object?>>[]
            {
                p => p.ProductId,
                p => p.ProductName
            });

        Assert.Null(result);
    }

    [Fact]
    public void From_OrderBy_ThenByString_OrdersByMultipleColumns()
    {
        var results = _fixture.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenBy("product_name")
            .Select();

        Assert.NotEmpty(results);
    }

    [Fact]
    public void From_OrderBy_ThenByDescendingString_OrdersByMultipleColumns()
    {
        var results = _fixture.Connection.From<Product>()
            .OrderBy(p => p.CategoryId)
            .ThenByDescending("unit_price")
            .Select();

        Assert.NotEmpty(results);
    }
}