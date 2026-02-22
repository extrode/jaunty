using System.Data;

using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryArgumentValidationTests : IDisposable
{
    private readonly Database _db;

    public QueryArgumentValidationTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Query<Category>("SELECT * FROM categories"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void Query_NullSql_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.Query<Category>(null!));

        Assert.Contains("SQL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_EmptySql_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Connection.Query<Category>(""));

        Assert.Contains("SQL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_WhitespaceSql_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Connection.Query<Category>("   \t\n  "));

        Assert.Contains("SQL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QueryScalar_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.QueryScalar<int>("SELECT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryScalar_NullSql_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.QueryScalar<int>(null!));
    }
}
