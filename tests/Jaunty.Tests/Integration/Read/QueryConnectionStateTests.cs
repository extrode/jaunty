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
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_ConnectionAlreadyOpen_LeavesOpen(DialectInfo dialect)
    {
        using var connection = _fixture.GetClosedConnection(dialect);
        connection.Open();
        var initialState = connection.State;

        var categories = connection.Query<Category>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT CategoryId, CategoryName, Description FROM Categories"
                : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        Assert.Equal(initialState, connection.State);
        Assert.NotEmpty(categories);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_ConnectionClosed_OpensAndCloses(DialectInfo dialect)
    {
        using var connection = _fixture.GetClosedConnection(dialect);
        // Connection starts closed
        Assert.Equal(ConnectionState.Closed, connection.State);

        var categories = connection.Query<Category>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT CategoryId, CategoryName, Description FROM Categories"
                : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        // Should be closed again after query
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.NotEmpty(categories);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_ConnectionAlreadyOpen_LeavesOpen(DialectInfo dialect)
    {
        using var connection = _fixture.GetClosedConnection(dialect);
        connection.Open();
        var initialState = connection.State;

        var categories = await connection.QueryAsync<Category>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT CategoryId, CategoryName, Description FROM Categories"
                : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        Assert.Equal(initialState, connection.State);
        Assert.NotEmpty(categories);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_ConnectionClosed_OpensAndCloses(DialectInfo dialect)
    {
        using var connection = _fixture.GetClosedConnection(dialect);
        // Connection starts closed
        Assert.Equal(ConnectionState.Closed, connection.State);

        var categories = await connection.QueryAsync<Category>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT CategoryId, CategoryName, Description FROM Categories"
                : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        // Should be closed again after query
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.NotEmpty(categories);
    }
}