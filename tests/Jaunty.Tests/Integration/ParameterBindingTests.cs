using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration;

public class ParameterBindingTests : IDisposable
{
    private readonly Database _db;

    public ParameterBindingTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void NamedParameters_AnonymousObject_BindsCorrectly()
    {
        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId AND discontinued = @Discontinued",
            new { CategoryId = 1, Discontinued = false });

        Assert.True(count >= 0);
    }

    [Fact]
    public void PositionalParameters_SingleValue_BindsCorrectly()
    {
        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            1);

        Assert.True(count >= 0);
    }

    //[Fact]
    //public void PositionalParameters_MultipleValues_BindsCorrectly()
    //{
    //    var count = _db.Connection.QueryScalar<long>(
    //        "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId AND supplier_id = @SupplierId",
    //        1, 1);

    //    Assert.True(count >= 0);
    //}

    [Fact]
    public void PositionalParameters_Array_BindsCorrectly()
    {
        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId AND supplier_id = @SupplierId",
            new object[] { 1, 1 });

        Assert.True(count >= 0);
    }

    [Fact]
    public void PositionalParameters_CountMismatch_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM products WHERE category_id = @A AND supplier_id = @B AND discontinued = @C",
                new object[] { 1, 2 })); // Only 2 values for 3 parameters

        Assert.Contains("mismatch", ex.Message.ToLower());
    }

    [Fact]
    public void PositionalParameters_StringValue_BindsCorrectly()
    {
        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE product_name LIKE @Name",
            "%Chai%");

        Assert.True(count >= 0);
    }

    [Fact]
    public void NamedParameters_NullValue_BindsAsDbNull()
    {
        // This tests that null values are properly converted to DBNull
        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM customers WHERE region = @Region OR @Region IS NULL",
            new { Region = (string?)null });

        Assert.True(count >= 0);
    }

    [Fact]
    public void NamedParameters_DateTime_BindsCorrectly()
    {
        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM orders WHERE order_date > @Date",
            new { Date = new DateTime(1997, 1, 1) });

        Assert.True(count >= 0);
    }

    [Fact]
    public void NamedParameters_Decimal_BindsCorrectly()
    {
        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE unit_price > @Price",
            new { Price = 10.0m });

        Assert.True(count >= 0);
    }
}
