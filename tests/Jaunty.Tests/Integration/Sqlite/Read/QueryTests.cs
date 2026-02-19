using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Read;

public class QueryTests : IDisposable
{
    private readonly Database _db;

    public QueryTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_AllColumns_ReturnsEntities()
    {
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        Assert.NotEmpty(categories);
        Assert.All(categories, c => Assert.False(string.IsNullOrEmpty(c.CategoryName)));
    }

    [Fact]
    public void Query_WithNamedParameter_FiltersCorrectly()
    {
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.Single(categories);
        Assert.Equal(1, categories[0].CategoryId);
    }

    [Fact]
    public void Query_WithPositionalParameter_FiltersCorrectly()
    {
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new {id = 1});

        Assert.Single(categories);
    }

    [Fact]
    public void Query_WithMultiplePositionalParams_FiltersCorrectly()
    {
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id >= @Min AND category_id <= @Max",
            new { Min = 1, Max = 3 });

        Assert.Equal(3, categories.Count);
    }

    [Fact]
    public void Query_StrictMode_MissingColumn_Throws()
    {
        // Selecting only 2 columns but mapping to entity with 3 properties
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.Query<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories"));

        Assert.Contains("Description", ex.Message);
        Assert.Contains("Strict mapping failed", ex.Message);
    }

    [Fact]
    public void QueryPartial_MissingColumn_Allowed()
    {
        var summaries = _db.Connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.False(string.IsNullOrEmpty(s.ProductName)));
    }

    [Fact]
    public void QueryPartial_ExtraColumnsInResult_Ignored()
    {
        // Query returns more columns than entity has properties - should work
        var summaries = _db.Connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price FROM products");

        Assert.NotEmpty(summaries);
    }

    [Fact]
    public void Query_NoRows_ReturnsEmptyList()
    {
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { id = -999 });

        Assert.Empty(categories);
    }

    [Fact]
    public void Query_NullableProperty_HandlesNull()
    {
        var customers = _db.Connection.Query<Customer>(
            @"SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName, 
              contact_title AS ContactTitle, address AS Address, city AS City, region AS Region,
              postal_code AS PostalCode, country AS Country, phone AS Phone, fax AS Fax
              FROM customers WHERE customer_id = @Id",
            new { Id = "ALFKI" });

        Assert.Single(customers);
        // Region is nullable and may be null
    }

    [Fact]
    public void Query_DuplicateParameterInSql_WorksCorrectly()
    {
        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id OR category_id = @Id",
            new { Id = 1 });

        Assert.Single(categories);
    }

    [Fact]
    public void QueryPartial_WithOptions_ReturnsEntities()
    {
        var summaries = _db.Connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }

    [Fact]
    public void QueryPartial_WithParametersAndOptions_ReturnsFilteredEntities()
    {
        var summaries = _db.Connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @Id",
            new { Id = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }
}
