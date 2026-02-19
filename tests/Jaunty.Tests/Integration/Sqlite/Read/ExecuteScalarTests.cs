using Jaunty.Core;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Read;

public class ExecuteScalarTests : IDisposable
{
    private readonly Database _db;

    public ExecuteScalarTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void ExecuteScalar_Count_ReturnsValue()
    {
        var count = _db.Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM products");

        Assert.True(count > 0);
    }

    [Fact]
    public void ExecuteScalar_WithParameters_ReturnsValue()
    {
        var count = _db.Connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.True(count > 0);
    }

    [Fact]
    public void ExecuteScalar_WithOptionsOnly_Works()
    {
        var count = _db.Connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM products",
            CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }

    [Fact]
    public void ExecuteScalar_WithParametersAndOptions_Works()
    {
        var count = _db.Connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }

    [Fact]
    public void ExecuteScalar_StringValue_ReturnsString()
    {
        var name = _db.Connection.ExecuteScalar<string>(
            "SELECT product_name FROM products ORDER BY product_id LIMIT 1");

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Fact]
    public void ExecuteScalar_MaxValue_ReturnsCorrect()
    {
        var maxId = _db.Connection.ExecuteScalar<long>("SELECT MAX(product_id) FROM products");

        Assert.True(maxId > 0);
    }
}
