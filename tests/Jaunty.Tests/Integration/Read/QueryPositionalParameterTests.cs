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

    #region Single Named Parameter

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

    #region True Bare-Scalar Positional Parameters

    // These exercise the actual positional-parameter feature (README: `connection.Query(sql, 1)`):
    // a bare scalar value bound by ParameterBinder.BindScalar to the single @param the SQL text
    // parses out, independent of the parameter's name. Everything above this region uses named
    // anonymous objects (`new { Id = 1 }`), which is ordinary named-parameter binding instead.

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_BareIntScalar_BindsPositionally(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var categories = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            1);

        Assert.Single(categories);
        Assert.Equal(1, categories[0].CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_BareStringScalar_BindsPositionally(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var customers = connection.Query<Customer>(
            @"SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName,
              contact_title AS ContactTitle, address AS Address, city AS City, region AS Region,
              postal_code AS PostalCode, country AS Country, phone AS Phone, fax AS Fax
              FROM customers WHERE customer_id = @CustomerId",
            "ALFKI");

        Assert.Single(customers);
        Assert.Equal("ALFKI", customers[0].CustomerId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryScalar_BareScalar_IgnoresSqlParamName(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        // The SQL's parameter is named @WhateverName; BindScalar binds by position (the single
        // distinct parameter in the SQL), not by matching the bare value to a property name.
        var count = connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM categories WHERE category_id = @WhateverName",
            1);

        Assert.Equal(1, count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_BareScalar_WithMultipleSqlParams_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentException>(() =>
            connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM products WHERE category_id = @A AND supplier_id = @B",
                1));

        Assert.Contains("2 distinct parameters", ex.Message);
        Assert.Contains("A", ex.Message);
        Assert.Contains("B", ex.Message);
    }

    #endregion

    #region Named Object Multi-Property Parameters

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

        // Northwind's seed data has products matching category 1 / supplier 1.
        Assert.True(count > 0);
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

        // A null @Region makes "@Region IS NULL" true, matching every customer row.
        Assert.True(count > 0);
    }

    #endregion
}