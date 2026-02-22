using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QuerySpecialCharacterTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QuerySpecialCharacterTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_DataWithSpecialCharacters_HandlesCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Some company names have special characters like apostrophes
        var customers = connection.Query<Customer>(
            @"SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName,
              contact_title AS ContactTitle, address AS Address, city AS City, region AS Region,
              postal_code AS PostalCode, country AS Country, phone AS Phone, fax AS Fax
              FROM customers WHERE company_name LIKE @Name",
            new { name = "%'%" });

        // Just verify it doesn't throw
        Assert.NotNull(customers);
    }
}


