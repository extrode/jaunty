using Jaunty.Core;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class ExecuteScalarAsyncTests : IDisposable
{
    private readonly Database _db;

    public ExecuteScalarAsyncTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [SkipSQLiteAsyncFact]
    public async Task ExecuteScalarAsync_Count_ReturnsValue()
    {
        var count = await _db.Connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM products");

        Assert.True(count > 0);
    }

    [SkipSQLiteAsyncFact]
    public async Task ExecuteScalarAsync_WithParameters_ReturnsValue()
    {
        var count = await _db.Connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.True(count > 0);
    }

    [SkipSQLiteAsyncFact]
    public async Task ExecuteScalarAsync_WithOptionsOnly_Works()
    {
        var count = await _db.Connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM products",
            CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }

    [SkipSQLiteAsyncFact]
    public async Task ExecuteScalarAsync_WithParametersAndOptions_Works()
    {
        var count = await _db.Connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }
}

