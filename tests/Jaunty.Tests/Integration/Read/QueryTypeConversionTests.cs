using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryTypeConversionTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryTypeConversionTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryScalar_TypeMismatch_ThrowsException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Trying to read a string as a DateTime
        Assert.ThrowsAny<Exception>(() =>
            connection.QueryScalar<DateTime>("SELECT product_name FROM products WHERE product_id = 1"));
    }
}



