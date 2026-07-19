using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class ExecuteScalarAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public ExecuteScalarAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task ExecuteScalarAsync_Count_ReturnsValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        if (dialect.Provider == DialectProvider.SqlServer)
        {
            var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM products");
            Assert.True(count > 0);
        }
        else
        {
            var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM products");
            Assert.True(count > 0);
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task ExecuteScalarAsync_WithParameters_ReturnsValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        if (dialect.Provider == DialectProvider.SqlServer)
        {
            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Products WHERE CategoryId = @CategoryId",
            new { CategoryId = 1 });
            Assert.True(count > 0);
        }
        else
        {
            var count = await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });
            Assert.True(count > 0);
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task ExecuteScalarAsync_WithOptionsOnly_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        if (dialect.Provider == DialectProvider.SqlServer)
        {
            var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM products");
            Assert.True(count > 0);
        }
        else
        {
            using var otherConnection = _fixture.GetDbConnection(dialect);
            var count = await otherConnection
                .ExecuteScalarAsync<long>("SELECT COUNT(*) FROM products",
                    CommandOptions.WithTimeout(30));
            Assert.True(count > 0);
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task ExecuteScalarAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            var count = await connection.ExecuteScalarAsync(
                "SELECT COUNT(*) FROM Products WHERE CategoryId = @CategoryId",
                        new { CategoryId = 1 },
            CommandOptions<int>.WithTimeout(30));
            Assert.True(count > 0);
        }
        else
        {
            var count = await connection.ExecuteScalarAsync(
                "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<long>.WithTimeout(30));
            Assert.True(count > 0);
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task ExecuteScalarAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            var count = await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM Products",
                cts.Token);
            Assert.True(count > 0);
        }
        else
        {
            var count = await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM products",
                cts.Token);
            Assert.True(count > 0);
        }
    }
}