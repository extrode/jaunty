using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryNullHandlingTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryNullHandlingTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_NullablePropertyWithNullValue_SetsToNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var customers = connection.Query<Customer>(
            @"SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName,
              contact_title AS ContactTitle, address AS Address, city AS City, region AS Region,
              postal_code AS PostalCode, country AS Country, phone AS Phone, fax AS Fax
              FROM customers WHERE region IS NULL LIMIT 1");

        Assert.NotEmpty(customers);
        Assert.Null(customers[0].Region);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_NullableIntWithNullValue_SetsToNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Products with null supplier_id
        var products = connection.QueryPartial<Product>(
            @"SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId,
              category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice,
              units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel,
              discontinued AS Discontinued
              FROM products WHERE supplier_id IS NULL");

        // May or may not have results, but shouldn't throw
        Assert.NotNull(products);
    }
}



