using System.Data;

using FluentAssertions;

using Jaunty;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Infrastructure;

public class QueryDatabaseConnectionTests : IClassFixture<Database>
{
    private IDbConnection db;

    public QueryDatabaseConnectionTests()
    {
        db = new Database().Connection;
    }

    [Fact]
    public void TestDatabaseConnection()
    {
        long result = db.QueryScalar<long>("SELECT 1");
        result.Should().Be(1);
    }
}