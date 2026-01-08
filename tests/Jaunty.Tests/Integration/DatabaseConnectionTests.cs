using System.Data;

using FluentAssertions;

using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration
{
    public class DatabaseConnectionTests : IClassFixture<Database>
    {
        private IDbConnection db;

        public DatabaseConnectionTests()
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
}
