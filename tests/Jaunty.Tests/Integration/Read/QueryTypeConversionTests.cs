using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryTypeConversionTests : IDisposable
{
    private readonly Database _db;

    public QueryTypeConversionTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void QueryScalar_TypeMismatch_ThrowsException()
    {
        // Trying to read a string as a DateTime
        Assert.ThrowsAny<Exception>(() =>
            _db.Connection.QueryScalar<DateTime>("SELECT product_name FROM products WHERE product_id = 1"));
    }
}

