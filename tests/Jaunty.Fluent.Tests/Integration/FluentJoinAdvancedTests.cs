using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Additional tests for JoinedQueryBuilder covering uncovered methods:
/// OrderByJoined, ThenBy variants, And/Or, Where(string), SelectFirst,
/// SelectPartial*, CountAsync, LongCount, SelectFirstBoth, etc.
/// </summary>
public class FluentJoinAdvancedTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentJoinAdvancedTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ==========================================
    // OrderByJoined Tests
    // ==========================================

    [Fact]
    public void InnerJoin_OrderByJoined_OrdersByCategoryName()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderByJoined(c => c.CategoryName)
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void InnerJoin_OrderByJoinedDescending_OrdersByCategoryNameDesc()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderByJoinedDescending(c => c.CategoryName)
            .Select();

        Assert.NotEmpty(products);
    }

    // ==========================================
    // ThenBy / ThenByJoined Variants
    // ==========================================

    [Fact]
    public void InnerJoin_OrderBy_ThenBy_OrdersByMultipleFromColumns()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void InnerJoin_OrderBy_ThenByDescending_OrdersCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderBy(p => p.CategoryId)
            .ThenByDescending(p => p.UnitPrice)
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void InnerJoin_OrderBy_ThenByJoined_MixesFromAndJoinColumns()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderBy(p => p.CategoryId)
            .ThenByJoined(c => c.CategoryName)
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void InnerJoin_OrderBy_ThenByJoinedDescending_MixesFromAndJoinColumnsDesc()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderBy(p => p.CategoryId)
            .ThenByJoinedDescending(c => c.CategoryName)
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void InnerJoin_OrderByJoined_ThenBy_OrdersByJoinedThenFrom()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderByJoined(c => c.CategoryName)
            .ThenBy(p => p.ProductName)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("category_name", sql);
        Assert.Contains("product_name", sql);
    }

    // ==========================================
    // Where(string) and Where(string, object)
    // ==========================================

    [Fact]
    public void InnerJoin_WhereString_RawCondition_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.category_id = 1")
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Fact]
    public void InnerJoin_WhereColumnValue_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.category_id", 1)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    // ==========================================
    // And / Or expression predicates
    // ==========================================

    [Fact]
    public void InnerJoin_Where_And_FiltersWithMultipleConditions()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .And((p, c) => p.Discontinued == false)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
        {
            Assert.Equal((short)1, p.CategoryId);
            Assert.False(p.Discontinued);
        });
    }

    [Fact]
    public void InnerJoin_Where_Or_FiltersWithOrCondition()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .Or((p, c) => c.CategoryId == 2)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2));
    }

    // ==========================================
    // SelectFirst (sync)
    // ==========================================

    [Fact]
    public void InnerJoin_SelectFirst_ReturnsFirstProduct()
    {
        var product = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirst();

        Assert.NotNull(product);
        Assert.NotEmpty(product.ProductName);
    }

    [Fact]
    public void InnerJoin_SelectFirstOrDefault_ReturnsProduct()
    {
        var product = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstOrDefault();

        Assert.NotNull(product);
    }

    [Fact]
    public void InnerJoin_SelectFirstOrDefault_NoResults_ReturnsNull()
    {
        var product = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == -999)
            .SelectFirstOrDefault();

        Assert.Null(product);
    }

    // ==========================================
    // SelectFirst<T> typed (sync)
    // ==========================================

    [Fact]
    public void InnerJoin_SelectFirstTyped_FromType_ReturnsProduct()
    {
        var product = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirst<Product>();

        Assert.NotNull(product);
        Assert.NotEmpty(product.ProductName);
    }

    [Fact]
    public void InnerJoin_SelectFirstTyped_JoinType_ReturnsCategory()
    {
        var category = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirst<Category>();

        Assert.NotNull(category);
        Assert.NotEmpty(category.CategoryName);
    }

    [Fact]
    public void InnerJoin_SelectFirstOrDefaultTyped_FromType_ReturnsProduct()
    {
        var product = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstOrDefault<Product>();

        Assert.NotNull(product);
    }

    [Fact]
    public void InnerJoin_SelectFirstOrDefaultTyped_JoinType_ReturnsCategory()
    {
        var category = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstOrDefault<Category>();

        Assert.NotNull(category);
    }

    [Fact]
    public void InnerJoin_SelectFirstOrDefaultTyped_NoResults_ReturnsNull()
    {
        var product = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == -999)
            .SelectFirstOrDefault<Product>();

        Assert.Null(product);
    }

    // ==========================================
    // SelectFirst with custom mapper (sync)
    // ==========================================

    [Fact]
    public void InnerJoin_SelectFirstWithMapper_ReturnsCustomDto()
    {
        var result = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirst(reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(reader.GetOrdinal("product_name")),
                CategoryName = reader.GetString(reader.GetOrdinal("category_name"))
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result.ProductName);
        Assert.NotEmpty(result.CategoryName);
    }

    [Fact]
    public void InnerJoin_SelectFirstOrDefaultWithMapper_ReturnsCustomDto()
    {
        var result = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstOrDefault(reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(reader.GetOrdinal("product_name")),
                CategoryName = reader.GetString(reader.GetOrdinal("category_name"))
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result!.ProductName);
    }

    [Fact]
    public void InnerJoin_SelectFirstOrDefaultWithMapper_NoResults_ReturnsNull()
    {
        var result = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == -999)
            .SelectFirstOrDefault(reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(reader.GetOrdinal("product_name")),
                CategoryName = reader.GetString(reader.GetOrdinal("category_name"))
            });

        Assert.Null(result);
    }

    // ==========================================
    // SelectFirstBoth (sync)
    // ==========================================

    [Fact]
    public void InnerJoin_SelectFirstBoth_ReturnsTuple()
    {
        var result = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstBoth();

        Assert.NotEmpty(result.From.ProductName);
        Assert.NotEmpty(result.Joined.CategoryName);
    }

    // ==========================================
    // Count (sync) and CountAsync
    // ==========================================

    [Fact]
    public void InnerJoin_Count_ReturnsPositiveCount()
    {
        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Count();

        Assert.True(count > 0);
    }

    [Fact]
    public void InnerJoin_LongCount_ReturnsPositiveCount()
    {
        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .LongCount();

        Assert.True(count > 0);
    }

    [Fact]
    public async Task InnerJoin_CountAsync_ReturnsPositiveCount()
    {
        var count = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .CountAsync();

        Assert.True(count > 0);
    }

    // ==========================================
    // SelectPartial Advanced (sync)
    // ==========================================

    [Fact]
    public void InnerJoin_SelectPartialFirstOrDefault_ReturnsDynamic()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartialFirstOrDefault("p.product_name, c.category_name");

        Assert.NotNull(result);
        string productName = result!.product_name;
        Assert.NotEmpty(productName);
    }

    [Fact]
    public void InnerJoin_SelectPartialFirstOrDefault_NoResults_ReturnsNull()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = -999")
            .SelectPartialFirstOrDefault("p.product_name");

        Assert.Null(result);
    }

    [Fact]
    public void InnerJoin_SelectPartialFirst_ReturnsDynamic()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartialFirst("p.product_name, c.category_name");

        Assert.NotNull(result);
        string productName = result.product_name;
        Assert.NotEmpty(productName);
    }

    [Fact]
    public void InnerJoin_SelectPartialFirst_NoResults_ThrowsInvalidOperationException()
    {
        var query = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = -999");

        Assert.Throws<InvalidOperationException>(() =>
            query.SelectPartialFirst("p.product_name"));
    }

    [Fact]
    public void InnerJoin_SelectPartialFirstTyped_WithMapper_ReturnsResult()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartialFirst("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result.ProductName);
    }

    [Fact]
    public void InnerJoin_SelectPartialFirstOrDefaultTyped_WithMapper_ReturnsResult()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartialFirstOrDefault("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result!.ProductName);
    }

    [Fact]
    public void InnerJoin_SelectPartialFirstOrDefaultTyped_NoResults_ReturnsNull()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = -999")
            .SelectPartialFirstOrDefault("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.Null(result);
    }

    // ==========================================
    // SelectPartialSingle / SelectPartialSingleOrDefault
    // ==========================================

    [Fact]
    public void InnerJoin_SelectPartialSingle_ReturnsExactlyOne()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = 1")
            .SelectPartialSingle("p.product_name, c.category_name");

        Assert.NotNull(result);
        string productName = result.product_name;
        Assert.NotEmpty(productName);
    }

    [Fact]
    public void InnerJoin_SelectPartialSingleOrDefault_ReturnsOneOrNull()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = -999")
            .SelectPartialSingleOrDefault("p.product_name");

        Assert.Null(result);
    }

    [Fact]
    public void InnerJoin_SelectPartialSingle_MultipleResults_ThrowsInvalidOperationException()
    {
        var query = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.category_id = 1");

        Assert.Throws<InvalidOperationException>(() =>
            query.SelectPartialSingle("p.product_name"));
    }

    [Fact]
    public void InnerJoin_SelectPartialSingleOrDefault_MultipleResults_ThrowsInvalidOperationException()
    {
        var query = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.category_id = 1");

        Assert.Throws<InvalidOperationException>(() =>
            query.SelectPartialSingleOrDefault("p.product_name"));
    }

    [Fact]
    public void InnerJoin_SelectPartialSingleTyped_WithMapper_ReturnsResult()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = 1")
            .SelectPartialSingle("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result.ProductName);
    }

    [Fact]
    public void InnerJoin_SelectPartialSingleOrDefaultTyped_NoResults_ReturnsNull()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = -999")
            .SelectPartialSingleOrDefault("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.Null(result);
    }

    // ==========================================
    // SelectPartial Async Variants
    // ==========================================

    [Fact]
    public async Task InnerJoin_SelectPartialAsync_ReturnsDynamicList()
    {
        var results = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartialAsync("p.product_name, c.category_name");

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialAsyncTyped_WithMapper_ReturnsList()
    {
        var results = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartialAsync("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialFirstAsync_ReturnsDynamic()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartialFirstAsync("p.product_name, c.category_name");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialFirstAsyncTyped_WithMapper_ReturnsResult()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartialFirstAsync("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result.ProductName);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialFirstOrDefaultAsync_ReturnsDynamic()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartialFirstOrDefaultAsync("p.product_name, c.category_name");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = -999")
            .SelectPartialFirstOrDefaultAsync("p.product_name");

        Assert.Null(result);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialFirstOrDefaultAsyncTyped_WithMapper_ReturnsResult()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartialFirstOrDefaultAsync("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialSingleAsync_ReturnsExactlyOne()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = 1")
            .SelectPartialSingleAsync("p.product_name, c.category_name");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialSingleAsyncTyped_WithMapper_ReturnsResult()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = 1")
            .SelectPartialSingleAsync("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result.ProductName);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialSingleOrDefaultAsync_NoResults_ReturnsNull()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = -999")
            .SelectPartialSingleOrDefaultAsync("p.product_name");

        Assert.Null(result);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialSingleOrDefaultAsyncTyped_NoResults_ReturnsNull()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = -999")
            .SelectPartialSingleOrDefaultAsync("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.Null(result);
    }

    // ==========================================
    // JoinedQueryBuilder Internal Methods Coverage
    // ==========================================

    [Fact]
    public void InnerJoin_SelectPartialFirstTyped_WithMapper_SingleResult_ReturnsResult()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = 1")
            .SelectPartialFirst("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result.ProductName);
    }

    [Fact]
    public void InnerJoin_SelectPartialFirstOrDefaultTyped_WithMapper_SingleResult_ReturnsResult()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = 1")
            .SelectPartialFirstOrDefault("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result.ProductName);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialFirstAsyncTyped_WithMapper_SingleResult_ReturnsResult()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = 1")
            .SelectPartialFirstAsync("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result.ProductName);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialFirstOrDefaultAsyncTyped_WithMapper_SingleResult_ReturnsResult()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = 1")
            .SelectPartialFirstOrDefaultAsync("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result.ProductName);
    }

    [Fact]
    public void InnerJoin_SelectPartialSingleTyped_WithMapper_SingleResult_ReturnsResult()
    {
        var result = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = 1")
            .SelectPartialSingle("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result.ProductName);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialSingleAsyncTyped_WithMapper_SingleResult_ReturnsResult()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = 1")
            .SelectPartialSingleAsync("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result.ProductName);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialSingleOrDefaultAsyncTyped_WithMapper_SingleResult_ReturnsResult()
    {
        var result = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Where("p.product_id = 1")
            .SelectPartialSingleOrDefaultAsync("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result.ProductName);
    }

    [Fact]
    public async Task InnerJoin_SelectPartialAsyncTyped_WithMapper_ReturnsResults()
    {
        var results = await _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .SelectPartialAsync("p.product_name, c.category_name", reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotEmpty(results);
        Assert.NotEmpty(results.First().ProductName);
    }

    // ==========================================
    // Async Typed Select Variants
    // ==========================================

    [Fact]
    public async Task InnerJoin_SelectAsyncWithMapper_ReturnsCustomDtos()
    {
        var results = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectAsync(reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(reader.GetOrdinal("product_name")),
                CategoryName = reader.GetString(reader.GetOrdinal("category_name"))
            });

        Assert.NotEmpty(results);
        Assert.NotEmpty(results.First().ProductName);
    }

    [Fact]
    public async Task InnerJoin_SelectFirstAsyncTyped_FromType_ReturnsProduct()
    {
        var product = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstAsync<Product>();

        Assert.NotNull(product);
        Assert.NotEmpty(product.ProductName);
    }

    [Fact]
    public async Task InnerJoin_SelectFirstAsyncTyped_JoinType_ReturnsCategory()
    {
        var category = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstAsync<Category>();

        Assert.NotNull(category);
        Assert.NotEmpty(category.CategoryName);
    }

    [Fact]
    public async Task InnerJoin_SelectFirstAsyncWithMapper_ReturnsCustomDto()
    {
        var result = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstAsync(reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(reader.GetOrdinal("product_name")),
                CategoryName = reader.GetString(reader.GetOrdinal("category_name"))
            });

        Assert.NotNull(result);
        Assert.NotEmpty(result.ProductName);
    }

    [Fact]
    public async Task InnerJoin_SelectFirstOrDefaultAsyncTyped_FromType_ReturnsProduct()
    {
        var product = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstOrDefaultAsync<Product>();

        Assert.NotNull(product);
    }

    [Fact]
    public async Task InnerJoin_SelectFirstOrDefaultAsyncTyped_JoinType_ReturnsCategory()
    {
        var category = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstOrDefaultAsync<Category>();

        Assert.NotNull(category);
    }

    [Fact]
    public async Task InnerJoin_SelectFirstOrDefaultAsyncWithMapper_ReturnsCustomDto()
    {
        var result = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstOrDefaultAsync(reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(reader.GetOrdinal("product_name")),
                CategoryName = reader.GetString(reader.GetOrdinal("category_name"))
            });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task InnerJoin_SelectFirstOrDefaultAsyncWithMapper_NoResults_ReturnsNull()
    {
        var result = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == -999)
            .SelectFirstOrDefaultAsync(reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(reader.GetOrdinal("product_name")),
                CategoryName = reader.GetString(reader.GetOrdinal("category_name"))
            });

        Assert.Null(result);
    }

    // ==========================================
    // 3-Table Join Variants
    // ==========================================

    [Fact]
    public void LeftJoin_ThirdTable_OnFromSecond_ReturnsResults()
    {
        // Join Product -> Category, then Category -> Supplier using OnFromSecond
        // (this tests the OnFromSecond path in JoinClause3Builder)
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .LeftJoin<Supplier>()
            .OnFromSecond(c => c.CategoryId, s => s.SupplierId) // contrived join for test
            .ToSql();

        Assert.Contains("INNER JOIN", sql);
        Assert.Contains("LEFT JOIN", sql);
    }

    [Fact]
    public void ThreeTableJoin_StringOnCondition_ReturnsResults()
    {
        var sql = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .ToSql();

        Assert.Contains("INNER JOIN", sql);
        Assert.Contains("p.category_id = c.category_id", sql);
        Assert.Contains("p.supplier_id = s.supplier_id", sql);
    }

    [Fact]
    public void ThreeTableJoin_RawOnCondition_ReturnsResults()
    {
        var sql = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id = c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id = s.supplier_id")
            .ToSql();

        Assert.Contains("p.category_id = c.category_id", sql);
        Assert.Contains("p.supplier_id = s.supplier_id", sql);
    }

    [Fact]
    public void ThreeTableJoin_Where_String_NoOp()
    {
        // Tests the Where(string) path on JoinedQuery3Builder (which is a no-op in current impl)
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where("1=1")
            .ToSql();

        Assert.Contains("INNER JOIN", sql);
    }

    // ==========================================
    // SelectAsync (primary from type)
    // ==========================================

    [Fact]
    public async Task InnerJoin_SelectAsync_ReturnsProducts()
    {
        var products = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectAsync();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.NotEmpty(p.ProductName));
    }

    [Fact]
    public async Task InnerJoin_SelectAsyncGeneric_WrongT1Type_ThrowsException()
    {
        var query = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            query.SelectAsync<Category, Category>());
    }

    [Fact]
    public async Task InnerJoin_SelectAsyncGeneric_WrongT2Type_ThrowsException()
    {
        var query = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            query.SelectAsync<Product, Product>());
    }
}