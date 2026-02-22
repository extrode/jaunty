using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Jaunty;
using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Xunit;

namespace Jaunty.Tests.Integration.Core;

/// <summary>
/// Abstract base class for cross-provider integration tests covering
/// core Query, QueryPartial, QueryScalar, ExecuteScalar, Insert, Update, Delete.
/// Provider subclasses supply the connection and dialect-specific SQL helpers.
/// All write tests use transaction rollback for data isolation.
/// </summary>
public abstract class CoreIntegrationTestBase : IDisposable
{
    private readonly DbConnection _connection;

    protected DbConnection Connection => _connection;

    protected CoreIntegrationTestBase()
    {
        _connection = CreateConnection();
    }

    protected abstract DbConnection CreateConnection();

    /// <summary>SQL for selecting a limited number of rows. Override per dialect.</summary>
    protected virtual string SelectTopProducts(int n) =>
        $"SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products LIMIT {n}";

    /// <summary>SQL for counting all products.</summary>
    protected virtual string SelectCountProducts => "SELECT COUNT(*) FROM products";

    /// <summary>SQL for counting products in a category.</summary>
    protected virtual string SelectCountProductsByCategory =>
        "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId";

    /// <summary>
    /// The CLR type returned by COUNT(*) for this provider.
    /// SQL Server returns int; PostgreSQL and MySQL return long.
    /// </summary>
    protected virtual Type CountReturnType => typeof(long);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    #region QueryPartial (read-only)

    protected void QueryPartial_ReturnsResults_Core()
    {
        var sql = SelectTopProducts(5);
        var products = _connection.QueryPartial<ProductSummary>(sql);

        Assert.NotEmpty(products);
        Assert.True(products.Count <= 5);
        Assert.All(products, p =>
        {
            Assert.True(p.ProductId > 0);
            Assert.False(string.IsNullOrEmpty(p.ProductName));
        });
    }

    protected void QueryPartialFirst_ReturnsFirst_Core()
    {
        var sql = SelectTopProducts(1);
        var product = _connection.QueryPartialFirst<ProductSummary>(sql);

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }

    protected void QueryPartialFirstOrDefault_NoResults_ReturnsNull_Core()
    {
        var product = _connection.QueryPartialFirstOrDefault<ProductSummary>(SelectEmptyProductSql);

        Assert.Null(product);
    }

    protected void QueryPartialSingle_WithUniqueResult_ReturnsSingle_Core()
    {
        var product = _connection.QueryPartialSingle<ProductSummary>(SelectProductByIdSql, new { ProductId = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    #endregion

    #region QueryPartial Async (read-only)

    protected async Task QueryPartialAsync_ReturnsResults_Core()
    {
        var sql = SelectTopProducts(5);
        var products = await _connection.QueryPartialAsync<ProductSummary>(sql);

        Assert.NotEmpty(products);
        Assert.True(products.Count <= 5);
    }

    protected async Task QueryPartialFirstAsync_ReturnsFirst_Core()
    {
        var sql = SelectTopProducts(1);
        var product = await _connection.QueryPartialFirstAsync<ProductSummary>(sql);

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }

    #endregion

    #region QueryScalar / ExecuteScalar (read-only)

    protected void QueryScalar_ReturnsCount_Core()
    {
        var count = _connection.QueryScalar<int>(SelectCountProducts);

        Assert.True(count > 0);
    }

    protected void QueryScalar_WithParameters_ReturnsCount_Core()
    {
        var count = _connection.QueryScalar<int>(SelectCountProductsByCategory, new { CategoryId = 1 });

        Assert.True(count > 0);
    }

    protected void ExecuteScalar_ReturnsCount_Core()
    {
        var result = _connection.ExecuteScalar<int>(SelectCountProducts);

        Assert.True(result > 0);
    }

    protected async Task QueryScalarAsync_ReturnsCount_Core()
    {
        var count = await _connection.QueryScalarAsync<int>(SelectCountProducts);

        Assert.True(count > 0);
    }

    protected async Task ExecuteScalarAsync_ReturnsCount_Core()
    {
        var result = await _connection.ExecuteScalarAsync<int>(SelectCountProducts);

        Assert.True(result > 0);
    }

    /// <summary>SQL for selecting a category by ID.</summary>
    protected virtual string SelectCategoryByIdSql =>
        "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = 1";

    /// <summary>SQL for selecting category name only.</summary>
    protected virtual string SelectCategoryNameOnlySql =>
        "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = 1";

    /// <summary>SQL for selecting empty product result.</summary>
    protected virtual string SelectEmptyProductSql =>
        "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE 1 = 0";

    /// <summary>SQL for selecting product by ID.</summary>
    protected virtual string SelectProductByIdSql =>
        "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = @ProductId";

    #endregion

    #region Write operations with transaction rollback

    protected void Insert_And_Delete_WithTransaction_Core()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var category = new Category
            {
                CategoryId = 99999,
                CategoryName = "TestCategory",
                Description = "Integration test"
            };

            var result = _connection.Insert(category,
                CommandOptions<Category>.WithTransaction(transaction));

            Assert.True(result > 0);

            var deleteResult = _connection.Delete(category,
                CommandOptions<Category>.WithTransaction(transaction));

            Assert.True(deleteResult > 0);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    protected void Update_WithTransaction_Core()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var category = _connection.QueryPartialFirst<Category>(
                SelectCategoryByIdSql,
                options: CommandOptions<Category>.WithTransaction(transaction));

            Assert.NotNull(category);

            var originalName = category.CategoryName;
            category.CategoryName = "UpdatedCategory";

            var result = _connection.Update(category,
                CommandOptions<Category>.WithTransaction(transaction));

            Assert.True(result > 0);

            var updated = _connection.QueryPartialFirst<Category>(
                SelectCategoryNameOnlySql,
                options: CommandOptions<Category>.WithTransaction(transaction));

            Assert.Equal("UpdatedCategory", updated.CategoryName);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    protected async Task InsertAsync_And_DeleteAsync_WithTransaction_Core()
    {
        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var category = new Category
            {
                CategoryId = 99998,
                CategoryName = "TestCategoryAsync",
                Description = "Async integration test"
            };

            var result = await _connection.InsertAsync(category,
                CommandOptions<Category>.WithTransaction(transaction));

            Assert.True(result > 0);

            var deleteResult = await _connection.DeleteAsync(category,
                CommandOptions<Category>.WithTransaction(transaction));

            Assert.True(deleteResult > 0);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    #endregion
}
