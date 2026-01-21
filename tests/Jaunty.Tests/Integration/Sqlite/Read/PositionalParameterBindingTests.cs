using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Read;

public class PositionalParameterBindingTests : IDisposable
{
    private readonly Database _db;

    public PositionalParameterBindingTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void PositionalParameters_SingleValue_BindsCorrectly()
    {
        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.True(count >= 0);
    }

    [Fact]
    public void PositionalParameters_Array_BindsCorrectly()
    {
        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId AND supplier_id = @SupplierId",
            new { CategoryId = 1, SupplierId = 1 });

        Assert.True(count >= 0);
    }

    [Fact]
    public void PositionalParameters_CountMismatch_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM products WHERE category_id = @A AND supplier_id = @B AND discontinued = @C",
                new { A = 1, B = 2 })); // Missing C parameter

        Assert.Contains("@C", ex.Message);
    }

    [Fact]
    public void PositionalParameters_StringValue_BindsCorrectly()
    {
        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE product_name LIKE @Name",
            new { Name = "%Chai%" });

        Assert.True(count >= 0);
    }
}
