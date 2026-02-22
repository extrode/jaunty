using System.Data;

using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryConnectionStateTests : IDisposable
{
    private readonly Database _db;

    public QueryConnectionStateTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_ConnectionAlreadyOpen_LeavesOpen()
    {
        _db.Connection.Open();
        var initialState = _db.Connection.State;

        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        Assert.Equal(initialState, _db.Connection.State);
        Assert.NotEmpty(categories);
    }

    [Fact]
    public void Query_ConnectionClosed_OpensAndCloses()
    {
        // Connection starts closed
        Assert.Equal(ConnectionState.Closed, _db.Connection.State);

        var categories = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        // Should be closed again after query
        Assert.Equal(ConnectionState.Closed, _db.Connection.State);
        Assert.NotEmpty(categories);
    }
}
