using System.Data;
using System.Data.SQLite;
using System.IO;

using Extrode.Jaunty.Fluent.Tests.Entities;

using Probe = Extrode.Jaunty.Fluent.Tests.Helpers.AsyncDisposal.SQLiteConnection;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Builders;

/// <summary>
/// AUD-R38-087 and AUD-R38-093. The arity-3/4 joined selects, the async insert and the grouped
/// selects released their command and reader with a blocking <c>Dispose</c>, and the grouped
/// selects closed a connection they had opened asynchronously with a blocking <c>Close</c>.
/// </summary>
public class AsyncTerminalDisposalTests : IDisposable
{
    private readonly string _path;
    private readonly Probe _probe;

    public AsyncTerminalDisposalTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"jaunty_dispose_{Guid.NewGuid()}.db");
        using (var seed = new SQLiteConnection($"Data Source={_path}"))
        {
            seed.Open();
            string script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "initialize_fluent_test_db.sql"));
            foreach (string statement in script.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(statement))
                    continue;

                using var command = seed.CreateCommand();
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }
        }

        _probe = new Probe(_path);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _probe.Dispose();
        SQLiteConnection.ClearAllPools();

        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
        }
    }

    private IDbConnection Connection => _probe;

    private void AssertReleasedAsynchronously(bool readsRows = true)
    {
        Assert.Contains("command.DisposeAsync", _probe.Released);
        Assert.DoesNotContain("command.Dispose", _probe.Released);
        if (readsRows)
        {
            Assert.Contains("reader.DisposeAsync", _probe.Released);
            Assert.DoesNotContain("reader.Dispose", _probe.Released);
        }
        Assert.Contains("connection.CloseAsync", _probe.Released);
        Assert.DoesNotContain("connection.Close", _probe.Released);
        Assert.Equal(ConnectionState.Closed, _probe.State);
    }

    [Fact]
    public async Task ThreeTableSelectAllAsync_ReleasesAsynchronously()
    {
        var rows = await Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .SelectAllAsync();

        Assert.NotEmpty(rows);
        AssertReleasedAsynchronously();
    }

    [Fact]
    public async Task FourTableSelectAllAsync_ReleasesAsynchronously()
    {
        var rows = await Connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On("p.category_id", "c.category_id")
            .InnerJoin<Supplier>("s")
            .On("p.supplier_id", "s.supplier_id")
            .LeftJoin<Product, Category, Supplier, Order>("o")
            .On("o.order_id > 0")
            .SelectAllAsync();

        Assert.NotEmpty(rows);
        AssertReleasedAsynchronously();
    }

    [Fact]
    public async Task InsertAsync_ReleasesAsynchronously()
    {
        long id = await Connection.Into<Product>()
            .Value(p => p.ProductName, "AsyncDisposalProbe")
            .Value("supplier_id", 1)
            .Value("category_id", 1)
            .Value(p => p.Discontinued, false)
            .InsertAsync();

        Assert.True(id > 0);
        AssertReleasedAsynchronously(readsRows: false);
    }

    [Fact]
    public async Task GroupedSelectAsync_ReleasesAsynchronously()
    {
        var results = await Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        AssertReleasedAsynchronously();
    }

    [Fact]
    public async Task GroupedJoinSelectAsync_ReleasesAsynchronously()
    {
        var results = await Connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        AssertReleasedAsynchronously();
    }

    [Fact]
    public async Task GroupedThreeTableJoinSelectAsync_ReleasesAsynchronously()
    {
        var results = await Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .GroupBy((p, c, s) => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        AssertReleasedAsynchronously();
    }

    [Fact]
    public async Task GroupedFourTableJoinSelectAsync_ReleasesAsynchronously()
    {
        var results = await Connection.From<Product>()
            .InnerJoin<Category>().On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>().On(p => p.SupplierId, s => s.SupplierId)
            .LeftJoin<Product, Category, Supplier, Order>().On("orders.order_id > 0")
            .GroupBy((p, c, s, o) => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        AssertReleasedAsynchronously();
    }
}
