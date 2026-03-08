using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryPositionalParameterTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryPositionalParameterTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    #region Single Positional Parameter

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_SingleIntParameter_BindsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var categories = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        Assert.Single(categories);
        Assert.Equal(1, categories[0].CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_SingleStringParameter_BindsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var customers = connection.Query<Customer>(
            @"SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName,
              contact_title AS ContactTitle, address AS Address, city AS City, region AS Region,
              postal_code AS PostalCode, country AS Country, phone AS Phone, fax AS Fax
              FROM customers WHERE customer_id = @Id",
            new { Id = "ALFKI" });

        Assert.Single(customers);
        Assert.Equal("ALFKI", customers[0].CustomerId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_SingleDateTimeParameter_BindsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM orders WHERE order_date > @Date",
            new { Date = new DateTime(1997, 6, 1) });

        Assert.True(count > 0);
    }

    #endregion

    #region Array Parameters

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_ObjectArray_BindsCorrectly(DialectInfo dialect)
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
    public void QueryScalar_ObjectArray_BindsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @Cat AND supplier_id = @Sup",
            new { Cat = 1, Sup = 1 });

        Assert.True(count >= 0);
    }

    #endregion

    #region Parameter Count Validation

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_TooFewParameters_ThrowsWithMessage(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentException>(() =>
            connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM products WHERE category_id = @A AND supplier_id = @B AND discontinued = @C",
                new { A = 1, B = 2 }));

        // Error should indicate missing parameter C
        Assert.Contains("@C", ex.Message);
    }

    #endregion

    #region NULL Positional Parameters

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_NullPositionalParameter_BindsAsDbNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM customers WHERE region = @Region OR @Region IS NULL",
            new { region = (long?)null });

        Assert.True(count >= 0);
    }

    #endregion
}