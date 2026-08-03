using System.Data;

using Jaunty.Fluent.Tests.Entities;

namespace Jaunty.Fluent.Tests.Unit.Builders;

/// <summary>
/// AUD-R35-180 and AUD-R35-181, over the same fake PostgreSQL connection
/// <see cref="InsertBuilderPostgresIdentityTests"/> uses.
/// </summary>
public class InsertBuilderIdentityScalarTests
{
    // ------------------------------------------------------------------
    // AUD-R35-180 - ToSql() showed the INSERT without the identity statement that actually runs
    // ------------------------------------------------------------------

    [Fact]
    public void ToSql_IdentityEntity_ShowsTheStatementThatActuallyExecutes()
    {
        using var connection = new NpgsqlConnection();

        string sql = connection.Into<Product>().Values(NewProductEntity()).ToSql();

        connection.Into<Product>().Values(NewProductEntity()).Insert();

        Assert.Contains("RETURNING product_id", sql);
        Assert.Equal(connection.LastCommandText, sql);
    }

    [Fact]
    public void ToSql_NonIdentityEntity_IsUnchanged()
    {
        using var connection = new NpgsqlConnection();

        string sql = connection.Into<Category>()
            .Values(new Category { CategoryId = 99, CategoryName = "TestCategory" })
            .ToSql();

        connection.Into<Category>()
            .Values(new Category { CategoryId = 99, CategoryName = "TestCategory" })
            .Insert();

        Assert.DoesNotContain("RETURNING", sql);
        Assert.Equal(connection.LastCommandText, sql);
    }

    // ------------------------------------------------------------------
    // AUD-R35-181 - a NULL identity scalar threw InvalidCastException after the row went in
    // ------------------------------------------------------------------

    [Fact]
    public void Insert_IdentityScalarIsDbNull_ReturnsZeroInsteadOfThrowing()
    {
        using var connection = new NpgsqlConnection { ScalarResult = DBNull.Value };

        long id = connection.Into<Product>().Values(NewProductEntity()).Insert();

        Assert.Equal(0L, id);
        Assert.True(connection.ExecuteScalarCalled);
    }

    [Fact]
    public async Task InsertAsync_IdentityScalarIsDbNull_ReturnsZeroInsteadOfThrowing()
    {
        using var connection = new NpgsqlConnection { ScalarResult = DBNull.Value };

        long id = await connection.Into<Product>().Values(NewProductEntity()).InsertAsync();

        Assert.Equal(0L, id);
        Assert.True(connection.ExecuteScalarCalled);
    }

    [Fact]
    public void Insert_IdentityScalarIsNull_ReturnsZero()
    {
        using var connection = new NpgsqlConnection { ScalarResult = null };

        long id = connection.Into<Product>().Values(NewProductEntity()).Insert();

        Assert.Equal(0L, id);
    }

    [Fact]
    public void Insert_IdentityScalarIsAValue_StillReturnsIt()
    {
        using var connection = new NpgsqlConnection { ScalarResult = 42 };

        long id = connection.Into<Product>().Values(NewProductEntity()).Insert();

        Assert.Equal(42L, id);
    }

    [Fact]
    public async Task InsertAsync_IdentityScalarIsAValue_StillReturnsIt()
    {
        using var connection = new NpgsqlConnection { ScalarResult = 42L };

        long id = await connection.Into<Product>().Values(NewProductEntity()).InsertAsync();

        Assert.Equal(42L, id);
    }

    [Fact]
    public async Task InsertAsync_ClosesTheConnectionItOpened()
    {
        using var connection = new NpgsqlConnection();

        Assert.Equal(ConnectionState.Closed, connection.State);

        await connection.Into<Product>().Values(NewProductEntity()).InsertAsync();

        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    private static Product NewProductEntity() => new()
    {
        ProductName = "Widget",
        SupplierId = 1,
        CategoryId = (short)1,
        QuantityPerUnit = "10 boxes",
        UnitPrice = 9.99m,
        UnitsInStock = (short)10,
        Discontinued = false
    };
}
