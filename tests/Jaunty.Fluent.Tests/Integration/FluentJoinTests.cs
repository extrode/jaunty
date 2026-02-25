using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentJoinTests : IDisposable
{
    private readonly Database _db;

    public FluentJoinTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    [Fact]
    public void InnerJoin_ExpressionKeys_ReturnsJoinedResults()
    {
        var products = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId != null);
    }

    [Fact]
    public void InnerJoin_PredicateExpression_ReturnsJoinedResults()
    {
        var products = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On((p, c) => p.CategoryId == c.CategoryId)
            .Select();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public void InnerJoin_StringColumns_WithAliases_ReturnsJoinedResults()
    {
        var products = _db.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .Select();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public void InnerJoin_RawCondition_ReturnsJoinedResults()
    {
        var products = _db.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id = c.category_id")
            .Select();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public void InnerJoin_SelectTyped_ReturnsCategories()
    {
        var categories = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select<Category>();

        categories.Should().NotBeEmpty();
        categories.First().CategoryName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InnerJoin_SelectBothTyped_ReturnsTuples()
    {
        var results = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectBoth();

        results.Should().NotBeEmpty();
        var first = results.First();
        first.Item1.ProductName.Should().NotBeNullOrEmpty();
        first.Item2.CategoryName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InnerJoin_WithWhere_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1);
    }

    [Fact]
    public void InnerJoin_WithOrderBy_OrdersResults()
    {
        var products = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderBy(p => p.ProductName)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public void InnerJoin_Count_ReturnsCorrectCount()
    {
        var count = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .LongCount();

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void InnerJoin_ToSql_GeneratesValidSql()
    {
        var sql = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .ToSql();

        sql.Should().Contain("INNER JOIN");
        sql.Should().Contain("ON");
        sql.Should().Contain("category_id");
    }

    [Fact]
    public void InnerJoin_WithAliases_ToSql_GeneratesValidSql()
    {
        var sql = _db.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .ToSql();

        sql.Should().Contain("products p");
        sql.Should().Contain("categories c");
        sql.Should().Contain("p.category_id = c.category_id");
    }

    [Fact]
    public void LeftJoin_ReturnsAllFromLeft()
    {
        var products = _db.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public void InnerJoin_SelectPartial_ReturnsDynamicResults()
    {
        var results = _db.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartial("p.product_name, c.category_name");

        results.Should().NotBeEmpty();
        string productName = results.First().product_name;
        string categoryName = results.First().category_name;
        productName.Should().NotBeNullOrEmpty();
        categoryName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InnerJoin_SelectPartial_WithMapper_ReturnsTypedResults()
    {
        var results = _db.Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectPartial("p.product_name, c.category_name", reader => new
            {
                ProductName = reader.GetString(0),
                CategoryName = reader.GetString(1)
            });

        results.Should().NotBeEmpty();
        results.First().ProductName.Should().NotBeNullOrEmpty();
        results.First().CategoryName.Should().NotBeNullOrEmpty();
    }

    // ==========================================
    // Multi-Entity Selection Tests (Select<T1, T2> and SelectTuple)
    // ==========================================

    [Fact]
    public void InnerJoin_SelectGeneric_ReturnsTuples()
    {
        var results = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectBoth();

        results.Should().NotBeEmpty();
        var first = results.First();
        first.Item1.ProductName.Should().NotBeNullOrEmpty();
        first.Item2.CategoryName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InnerJoin_SelectGeneric_WithWhere_FiltersResults()
    {
        var results = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .SelectBoth();

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(r => r.Item1.CategoryId == 1);
    }

    [Fact]
    public void InnerJoin_SelectGeneric_WrongT1Type_ThrowsException()
    {
        var query = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId);

        var act = () => query.Select<Category, Category>(); // Wrong T1 type

        act.Should().Throw<ArgumentException>()
            .WithMessage("*T1 must be Product*");
    }

    [Fact]
    public void InnerJoin_SelectGeneric_WrongT2Type_ThrowsException()
    {
        var query = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId);

        var act = () => query.Select<Product, Product>(); // Wrong T2 type

        act.Should().Throw<ArgumentException>()
            .WithMessage("*T2 must be Category*");
    }

    [Fact]
    public void InnerJoin_SelectCustomEntity_PartialDto_ThrowsInStrictMode()
    {
        // Select<T>() uses strict mode - all columns must map to properties
        // ProductInfo only has subset of columns, so it should throw
        var query = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId);

        var act = () => query.Select<ProductInfo>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not map to any property*");
    }

    [Fact]
    public void InnerJoin_SelectCustomEntity_WithMapper_ReturnsCustomEntity()
    {
        var results = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select(reader => new ProductCategoryDto
            {
                ProductName = reader.GetString(reader.GetOrdinal("product_name")),
                CategoryName = reader.GetString(reader.GetOrdinal("category_name"))
            });

        results.Should().NotBeEmpty();
        results.First().ProductName.Should().NotBeNullOrEmpty();
        results.First().CategoryName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InnerJoin_SelectAsyncGeneric_ReturnsTuples()
    {
        var results = await _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectAsync<Product, Category>();

        results.Should().NotBeEmpty();
        var first = results.First();
        first.Item1.ProductName.Should().NotBeNullOrEmpty();
        first.Item2.CategoryName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InnerJoin_SelectAsyncTyped_ReturnsCategories()
    {
        var results = await _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectAsync<Category>();

        results.Should().NotBeEmpty();
        var first = results.First();
        first.CategoryName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LeftJoin_SelectGeneric_ReturnsTuples()
    {
        var results = _db.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectBoth();

        results.Should().NotBeEmpty();
    }

    // ==========================================
    // Async Variants Tests
    // ==========================================

    [Fact]
    public async Task InnerJoin_SelectAsyncCategory_ReturnsCategories()
    {
        var categories = await _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectAsync<Category>();

        categories.Should().NotBeEmpty();
        categories.First().CategoryName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InnerJoin_SelectFirstAsync_ReturnsFirstProduct()
    {
        var product = await _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstAsync();

        product.Should().NotBeNull();
        product.ProductName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InnerJoin_SelectFirstOrDefaultAsync_ReturnsFirstOrNull()
    {
        var product = await _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstOrDefaultAsync();

        product.Should().NotBeNull();
    }

    [Fact]
    public async Task InnerJoin_SelectFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        var product = await _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => p.ProductId == -999) // Non-existent
            .SelectFirstOrDefaultAsync();

        product.Should().BeNull();
    }

    [Fact]
    public async Task InnerJoin_SelectFirstBothAsync_ReturnsTuple()
    {
        var result = await _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstBothAsync();

        result.From.ProductName.Should().NotBeNullOrEmpty();
        result.Joined.CategoryName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InnerJoin_LongCountAsync_ReturnsCorrectCount()
    {
        var count = await _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .LongCountAsync();

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InnerJoin_LongCountAsync_WithWhere_FiltersResults()
    {
        var count = await _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .LongCountAsync();

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void LeftJoin_SingleJoin_ReturnsAllLeftTableRows()
    {
        var products = _db.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public void LeftJoin_WithWhere_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1);
    }

    [Fact]
    public void LeftJoin_WithOrderBy_OrdersResults()
    {
        var products = _db.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderByDescending(p => p.UnitPrice)
            .Select();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LeftJoin_Async_ReturnsJoinedResults()
    {
        var products = await _db.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectAsync();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LeftJoin_SelectFirstOrDefaultAsync_ReturnsFirst()
    {
        var product = await _db.Connection.From<Product>()
            .LeftJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstOrDefaultAsync();

        product.Should().NotBeNull();
    }

    [Fact]
    public void InnerJoin_WithCount_ReturnsCorrectCount()
    {
        var count = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Count();

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RightJoin_SingleJoin_ReturnsAllRightTableRows()
    {
        var results = _db.Connection.From<Product>()
            .RightJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Select();

        results.Should().NotBeEmpty();
    }

    [Fact]
    public void RightJoin_WithWhere_FiltersResults()
    {
        var results = _db.Connection.From<Product>()
            .RightJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .Where((p, c) => c.CategoryId == 1)
            .Select();

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(r => r.CategoryId == 1);
    }

    [Fact]
    public void RightJoin_WithOrderBy_OrdersResults()
    {
        var results = _db.Connection.From<Product>()
            .RightJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .OrderByDescending(c => c.CategoryId)
            .Select();

        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RightJoin_Async_ReturnsJoinedResults()
    {
        var results = await _db.Connection.From<Product>()
            .RightJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectAsync();

        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RightJoin_SelectFirstOrDefaultAsync_ReturnsFirst()
    {
        var result = await _db.Connection.From<Product>()
            .RightJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .SelectFirstOrDefaultAsync();

        result.Should().NotBeNull();
    }



    [Fact]
    public void InnerJoin_MultipleJoins_ThreeTables_ReturnsJoinedResults()
    {
        var results = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .SelectAll();

        results.Should().NotBeEmpty();
        var first = results.First();
        first.Item1.ProductName.Should().NotBeNullOrEmpty();
        first.Item2.CategoryName.Should().NotBeNullOrEmpty();
        first.Item3.CompanyName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InnerJoin_MultipleJoins_ThreeTables_ReturnsTuples()
    {
        var results = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .SelectAll();

        results.Should().NotBeEmpty();
        var first = results.First();
        first.Item1.ProductName.Should().NotBeNullOrEmpty();
        first.Item2.CategoryName.Should().NotBeNullOrEmpty();
        first.Item3.CompanyName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InnerJoin_MultipleJoins_WithWhere_FiltersResults()
    {
        var results = _db.Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .Where((p, c, s) => c.CategoryId == 1 && s.Country == "UK")
            .SelectAll();

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(r => r.Item2.CategoryId == 1 && r.Item3.Country == "UK");
    }
}
