using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentJoinTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentJoinTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void InnerJoin_ExpressionKeys_ReturnsJoinedResults()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId != null));
    }

    [Fact]
    public void InnerJoin_PredicateExpression_ReturnsJoinedResults()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId)
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void InnerJoin_StringColumns_WithAliases_ReturnsJoinedResults()
    {
        var products = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void InnerJoin_RawCondition_ReturnsJoinedResults()
    {
        var products = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id = c.category_id")
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void InnerJoin_SelectTyped_ReturnsCategories()
    {
        var categories = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select<Category>();

        Assert.NotEmpty(categories);
        Assert.NotEmpty(categories.First().CategoryName);
    }

    [Fact]
    public void InnerJoin_SelectBothTyped_ReturnsTuples()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectBoth();

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.NotEmpty(first.Item1.ProductName);
        Assert.NotEmpty(first.Item2.CategoryName);
    }

    [Fact]
    public void InnerJoin_WithWhere_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Fact]
    public void InnerJoin_WithOrderBy_OrdersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) <= 0);
        }
    }

    [Fact]
    public void InnerJoin_Count_ReturnsCorrectCount()
    {
        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .LongCount();

        Assert.True(count > 0);
    }

    [Fact]
    public void InnerJoin_ToSql_GeneratesValidSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .ToSql();

        Assert.Contains("INNER JOIN", sql);
        Assert.Contains("ON", sql);
        Assert.Contains("category_id", sql);
    }

    [Fact]
    public void InnerJoin_WithAliases_ToSql_GeneratesValidSql()
    {
        var sql = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .ToSql();

        Assert.Contains("products p", sql);
        Assert.Contains("categories c", sql);
        Assert.Contains("p.category_id = c.category_id", sql);
    }

    [Fact]
    public void LeftJoin_ReturnsAllFromLeft()
    {
        var products = _fixture.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void InnerJoin_SelectPartial_ReturnsDynamicResults()
    {
        var results = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartial("p.product_name, c.category_name");

        Assert.NotEmpty(results);
        string? productName = results.First()["product_name"] as string;
        string? categoryName = results.First()["category_name"] as string;
        Assert.NotEmpty(productName);
        Assert.NotEmpty(categoryName);
    }

    [Fact]
    public void InnerJoin_SelectPartial_WithMapper_ReturnsTypedResults()
    {
        var results = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartial("p.product_name, c.category_name", reader => new
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        Assert.NotEmpty(results);
        Assert.NotEmpty(results.First().ProductName);
        Assert.NotEmpty(results.First().CategoryName);
    }

    [Fact]
    public void InnerJoin_SelectPartial_WithDuplicateColumnName_ThrowsInvalidOperationException()
    {
        var query = _fixture.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            query.SelectPartial("p.product_name, c.category_name AS product_name"));
        Assert.Contains("product_name", ex.Message);
    }

    [Fact]
    public void InnerJoin_SelectCustomType_WithAmbiguousColumn_ThrowsInvalidOperationException()
    {
        var query = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId);

        // products.category_id and categories.category_id collide under SELECT *.
        var ex = Assert.Throws<InvalidOperationException>(() => query.Select<ProductInfo>());
        Assert.Contains("category_id", ex.Message);
    }

    // ==========================================
    // Multi-Entity Selection Tests (Select<T1, T2> and SelectTuple)
    // ==========================================

    [Fact]
    public void InnerJoin_SelectGeneric_ReturnsTuples()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectBoth();

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.NotEmpty(first.Item1.ProductName);
        Assert.NotEmpty(first.Item2.CategoryName);
    }

    [Fact]
    public void InnerJoin_SelectGeneric_WithWhere_FiltersResults()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .SelectBoth();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal((short)1, r.Item1.CategoryId));
    }

    [Fact]
    public void InnerJoin_SelectGeneric_WrongT1Type_ThrowsException()
    {
        var query = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId);

        var act = () => query.Select<Category, Category>(); // Wrong T1 type

        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Contains("T1 must be Product", ex.Message);
    }

    [Fact]
    public void InnerJoin_SelectGeneric_WrongT2Type_ThrowsException()
    {
        var query = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId);

        var act = () => query.Select<Product, Product>(); // Wrong T2 type

        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Contains("T2 must be Category", ex.Message);
    }

    [Fact]
    public void InnerJoin_SelectCustomEntity_WithMapper_ReturnsCustomEntity()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select(reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(reader.GetOrdinal("product_name")),
                CategoryName = reader.GetString(reader.GetOrdinal("category_name"))
            });

        Assert.NotEmpty(results);
        Assert.NotEmpty(results.First().ProductName);
        Assert.NotEmpty(results.First().CategoryName);
    }

    [Fact]
    public async Task InnerJoin_SelectAsyncGeneric_ReturnsTuples()
    {
        var results = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectAsync<Product, Category>();

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.NotEmpty(first.Item1.ProductName);
        Assert.NotEmpty(first.Item2.CategoryName);
    }

    [Fact]
    public async Task InnerJoin_SelectAsyncTyped_ReturnsCategories()
    {
        var results = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectAsync<Category>();

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.NotEmpty(first.CategoryName);
    }

    [Fact]
    public async Task InnerJoin_SelectAsyncCustomType_WithAmbiguousColumn_ThrowsInvalidOperationException()
    {
        var query = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId);

        // products.category_id and categories.category_id collide under SELECT *.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => query.SelectAsync<ProductInfo>());
        Assert.Contains("category_id", ex.Message);
    }

    [Fact]
    public void LeftJoin_SelectGeneric_ReturnsTuples()
    {
        var results = _fixture.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectBoth();

        Assert.NotEmpty(results);
    }

    // ==========================================
    // Async Variants Tests
    // ==========================================

    [Fact]
    public async Task InnerJoin_SelectAsyncCategory_ReturnsCategories()
    {
        var categories = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectAsync<Category>();

        Assert.NotEmpty(categories);
        Assert.NotEmpty(categories.First().CategoryName);
    }

    [Fact]
    public async Task InnerJoin_SelectFirstAsync_ReturnsFirstProduct()
    {
        var product = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstAsync();

        Assert.NotNull(product);
        Assert.NotEmpty(product.ProductName);
    }

    [Fact]
    public async Task InnerJoin_SelectFirstOrDefaultAsync_ReturnsFirstOrNull()
    {
        var product = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstOrDefaultAsync();

        Assert.NotNull(product);
    }

    [Fact]
    public async Task InnerJoin_SelectFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        var product = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == -999) // Non-existent
            .SelectFirstOrDefaultAsync();

        Assert.Null(product);
    }

    [Fact]
    public async Task InnerJoin_SelectSingleAsync_OneResult_ReturnsProduct()
    {
        var product = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == 1)
            .SelectSingleAsync();

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [Fact]
    public async Task InnerJoin_SelectSingleOrDefaultAsync_NoResults_ReturnsNull()
    {
        var product = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == -999) // Non-existent
            .SelectSingleOrDefaultAsync();

        Assert.Null(product);
    }

    [Fact]
    public async Task InnerJoin_SelectFirstBothAsync_ReturnsTuple()
    {
        var result = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstBothAsync();

        Assert.NotEmpty(result.From.ProductName);
        Assert.NotEmpty(result.Joined.CategoryName);
    }

    [Fact]
    public async Task InnerJoin_LongCountAsync_ReturnsCorrectCount()
    {
        var count = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .LongCountAsync();

        Assert.True(count > 0);
    }

    [Fact]
    public async Task InnerJoin_LongCountAsync_WithWhere_FiltersResults()
    {
        var count = await _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .LongCountAsync();

        Assert.True(count > 0);
    }

    [Fact]
    public void LeftJoin_SingleJoin_ReturnsAllLeftTableRows()
    {
        var products = _fixture.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public void LeftJoin_WithWhere_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Fact]
    public void LeftJoin_WithOrderBy_OrdersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderByDescending(p => p.UnitPrice)
            .Select();

        Assert.NotEmpty(products);
    }

    [Fact]
    public async Task LeftJoin_Async_ReturnsJoinedResults()
    {
        var products = await _fixture.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectAsync();

        Assert.NotEmpty(products);
    }

    [Fact]
    public async Task LeftJoin_SelectFirstOrDefaultAsync_ReturnsFirst()
    {
        var product = await _fixture.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstOrDefaultAsync();

        Assert.NotNull(product);
    }

    [Fact]
    public void InnerJoin_WithCount_ReturnsCorrectCount()
    {
        var count = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Count();

        Assert.True(count > 0);
    }

    [Fact]
    public void RightJoin_SingleJoin_ReturnsAllRightTableRows()
    {
        var results = _fixture.Connection.From<Product>()
            .RightJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select();

        Assert.NotEmpty(results);
    }

    [Fact]
    public void RightJoin_WithWhere_FiltersResults()
    {
        var results = _fixture.Connection.From<Product>()
            .RightJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal((short)1, r.CategoryId));
    }

    [Fact]
    public void RightJoin_WithOrderBy_OrdersResults()
    {
        var results = _fixture.Connection.From<Product>()
            .RightJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderByDescending(c => c.CategoryId)
            .Select();

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task RightJoin_Async_ReturnsJoinedResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .RightJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectAsync();

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task RightJoin_SelectFirstOrDefaultAsync_ReturnsFirst()
    {
        var result = await _fixture.Connection.From<Product>()
            .RightJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstOrDefaultAsync();

        Assert.NotNull(result);
    }



    [Fact]
    public void InnerJoin_MultipleJoins_ThreeTables_ReturnsTuples()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .SelectAll();

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.NotEmpty(first.Item1.ProductName);
        Assert.NotEmpty(first.Item2.CategoryName);
        Assert.NotEmpty(first.Item3.CompanyName);
    }

    [Fact]
    public void InnerJoin_MultipleJoins_WithWhere_FiltersResults()
    {
        var results = _fixture.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => c.CategoryId == 1 && s.Country == "UK")
            .SelectAll();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Item2.CategoryId == 1 && r.Item3.Country == "UK"));
    }
}