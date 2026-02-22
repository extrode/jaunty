using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QuerySqlErrorTests : IDisposable
{
    private readonly Database _db;

    public QuerySqlErrorTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_InvalidSql_ThrowsException()
    {
        Assert.ThrowsAny<Exception>(() =>
            _db.Connection.Query<Category>("SELECT * FROM nonexistent_table"));
    }

    [Fact]
    public void Query_SyntaxError_ThrowsException()
    {
        Assert.ThrowsAny<Exception>(() =>
            _db.Connection.Query<Category>("SELEC * FRO categories"));
    }

    [Fact]
    public void QueryScalar_InvalidSql_ThrowsException()
    {
        Assert.ThrowsAny<Exception>(() =>
            _db.Connection.QueryScalar<int>("SELECT COUNT(*) FROM nonexistent_table"));
    }
}
