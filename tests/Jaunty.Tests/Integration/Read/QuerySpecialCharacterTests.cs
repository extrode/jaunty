using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QuerySpecialCharacterTests : IDisposable
{
    private readonly Database _db;

    public QuerySpecialCharacterTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_DataWithSpecialCharacters_HandlesCorrectly()
    {
        // Some company names have special characters like apostrophes
        var customers = _db.Connection.Query<Customer>(
            @"SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName,
              contact_title AS ContactTitle, address AS Address, city AS City, region AS Region,
              postal_code AS PostalCode, country AS Country, phone AS Phone, fax AS Fax
              FROM customers WHERE company_name LIKE @Name",
            new { name = "%'%" });

        // Just verify it doesn't throw
        Assert.NotNull(customers);
    }
}