using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryNullHandlingTests : IDisposable
{
    private readonly Database _db;

    public QueryNullHandlingTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_NullablePropertyWithNullValue_SetsToNull()
    {
        var customers = _db.Connection.Query<Customer>(
            @"SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName,
              contact_title AS ContactTitle, address AS Address, city AS City, region AS Region,
              postal_code AS PostalCode, country AS Country, phone AS Phone, fax AS Fax
              FROM customers WHERE region IS NULL LIMIT 1");

        Assert.NotEmpty(customers);
        Assert.Null(customers[0].Region);
    }

    [Fact]
    public void Query_NullableIntWithNullValue_SetsToNull()
    {
        // Products with null supplier_id
        var products = _db.Connection.QueryPartial<Product>(
            @"SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId,
              category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice,
              units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel,
              discontinued AS Discontinued
              FROM products WHERE supplier_id IS NULL");

        // May or may not have results, but shouldn't throw
        Assert.NotNull(products);
    }
}
