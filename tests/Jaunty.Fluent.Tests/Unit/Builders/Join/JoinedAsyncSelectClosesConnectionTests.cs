using System.Data;
using System.Data.SQLite;
using System.IO;

using Jaunty.Fluent.Tests.Entities;

namespace Jaunty.Fluent.Tests.Unit.Builders.Join;

/// <summary>
/// AUD-R35-178. The arity-3 and arity-4 async multi-entity selects finished with a blocking
/// <c>Close()</c> on a connection they had opened with <c>OpenAsync</c> three lines above. These
/// tests pin the observable half of that contract - a connection the terminal opened is returned
/// closed - on a file-backed connection that starts closed, which the shared fixture's
/// always-open connection cannot exercise.
/// </summary>
public class JoinedAsyncSelectClosesConnectionTests : IDisposable
{
    private readonly string _path;
    private readonly SQLiteConnection _connection;

    public JoinedAsyncSelectClosesConnectionTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"jaunty_close_{Guid.NewGuid()}.db");
        _connection = new SQLiteConnection($"Data Source={_path}");
        _connection.Open();

        string script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "initialize_fluent_test_db.sql"));
        foreach (string statement in script.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.IsNullOrWhiteSpace(statement))
                continue;

            using var command = _connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }

        _connection.Close();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
        SQLiteConnection.ClearAllPools();

        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task ThreeTableSelectAllAsync_ReturnsTheConnectionClosed()
    {
        Assert.Equal(ConnectionState.Closed, _connection.State);

        var rows = await ((IDbConnection)_connection).From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .SelectAllAsync();

        Assert.NotEmpty(rows);
        Assert.Equal(ConnectionState.Closed, _connection.State);
    }

    [Fact]
    public async Task FourTableSelectAllAsync_ReturnsTheConnectionClosed()
    {
        Assert.Equal(ConnectionState.Closed, _connection.State);

        var rows = await ((IDbConnection)_connection).From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .LeftJoin<Product, Category, Supplier, Order>("o")
            .On("o.order_id > 0")
            .SelectAllAsync();

        Assert.NotEmpty(rows);
        Assert.Equal(ConnectionState.Closed, _connection.State);
    }
}
