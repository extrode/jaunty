using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_AllColumns_ReturnsEntities(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var categories = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        Assert.NotEmpty(categories);
        Assert.All(categories, c => Assert.False(string.IsNullOrEmpty(c.CategoryName)));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithNamedParameter_FiltersCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var categories = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.Single(categories);
        Assert.Equal(1, categories[0].CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithPositionalParameter_FiltersCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var categories = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { id = 1 });

        Assert.Single(categories);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithMultiplePositionalParams_FiltersCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var categories = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id >= @Min AND category_id <= @Max",
            new { Min = 1, Max = 3 });

        Assert.Equal(3, categories.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_StrictMode_MissingColumn_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            connection.Query<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories"));

        Assert.Contains("Description", ex.Message);
        Assert.Contains("Strict mapping failed", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartial_MissingColumn_Allowed(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var summaries = connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.False(string.IsNullOrEmpty(s.ProductName)));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartial_ExtraColumnsInResult_Ignored(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var summaries = connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price FROM products");

        Assert.NotEmpty(summaries);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_NoRows_ReturnsEmptyList(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var categories = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { id = -999 });

        Assert.Empty(categories);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_NullableProperty_HandlesNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var customers = connection.Query<Customer>(
            @"SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName,
              contact_title AS ContactTitle, address AS Address, city AS City, region AS Region,
              postal_code AS PostalCode, country AS Country, phone AS Phone, fax AS Fax
              FROM customers WHERE customer_id = @Id",
            new { Id = "ALFKI" });

        Assert.Single(customers);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_DuplicateParameterInSql_WorksCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var categories = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id OR category_id = @Id",
            new { Id = 1 });

        Assert.Single(categories);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartial_WithOptions_ReturnsEntities(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var summaries = connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartial_WithParametersAndOptions_ReturnsFilteredEntities(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var summaries = connection.QueryPartial<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @Id",
            new { Id = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }
}

