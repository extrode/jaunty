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

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories";

        var categories = connection.Query<Category>(sql);

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

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId = @CategoryId"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @CategoryId";

        var categories = connection.Query<Category>(sql, new { CategoryId = 1 });

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

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId = @Id"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id";

        var categories = connection.Query<Category>(sql, new { id = 1 });

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

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId >= @Min AND CategoryId <= @Max"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id >= @Min AND category_id <= @Max";

        var categories = connection.Query<Category>(sql, new { Min = 1, Max = 3 });

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

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            connection.Query<Category>(sql));

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

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT ProductId, ProductName FROM Products"
            : "SELECT product_id AS ProductId, product_name AS ProductName FROM products";

        var summaries = connection.QueryPartial<ProductSummary>(sql);

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

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT ProductId, ProductName, UnitPrice FROM Products"
            : "SELECT product_id AS ProductId, product_name AS ProductName, unit_price FROM products";

        var summaries = connection.QueryPartial<ProductSummary>(sql);

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

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId = @Id"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id";

        var categories = connection.Query<Category>(sql, new { id = -999 });

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

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? @"SELECT CustomerId, CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax FROM customers WHERE customer_id = @Id"
            : @"SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName, contact_title AS ContactTitle, address AS Address, city AS City, region AS Region, postal_code AS PostalCode, country AS Country, phone AS Phone, fax AS Fax FROM customers WHERE customer_id = @Id";

        var customers = connection.Query<Customer>(sql, new { Id = "ALFKI" });

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

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId = @Id OR CategoryId = @Id"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id OR category_id = @Id";

        var categories = connection.Query<Category>(sql, new { Id = 1 });

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

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT ProductId, ProductName FROM Products"
            : "SELECT product_id AS ProductId, product_name AS ProductName FROM products";

        var summaries = connection.QueryPartial<ProductSummary>(sql, CommandOptions<ProductSummary>.WithTimeout(30));

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

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT ProductId, ProductName FROM Products WHERE category_id = @Id"
            : "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @Id";

        var summaries = connection.QueryPartial<ProductSummary>(sql, new { Id = 1 }, CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }
}

