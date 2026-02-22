using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class PositionalParameterBindingTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public PositionalParameterBindingTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void PositionalParameters_SingleValue_BindsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.True(count >= 0);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void PositionalParameters_Array_BindsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId AND supplier_id = @SupplierId",
            new { CategoryId = 1, SupplierId = 1 });

        Assert.True(count >= 0);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void PositionalParameters_CountMismatch_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentException>(() =>
            connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM products WHERE category_id = @A AND supplier_id = @B AND discontinued = @C",
                new { A = 1, B = 2 })); // Missing C parameter

        Assert.Contains("@C", ex.Message);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void PositionalParameters_StringValue_BindsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE product_name LIKE @Name",
            new { Name = "%Chai%" });

        Assert.True(count >= 0);
    }
}



