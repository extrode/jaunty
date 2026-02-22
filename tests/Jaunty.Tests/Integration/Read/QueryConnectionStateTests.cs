using System.Data;

using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryConnectionStateTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryConnectionStateTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_ConnectionAlreadyOpen_LeavesOpen(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        connection.Open();
        var initialState = connection.State;

        var categories = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        Assert.Equal(initialState, connection.State);
        Assert.NotEmpty(categories);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_ConnectionClosed_OpensAndCloses(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Connection starts closed
        Assert.Equal(ConnectionState.Closed, connection.State);

        var categories = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        // Should be closed again after query
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.NotEmpty(categories);
    }
}


