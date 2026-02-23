using System.Data.Common;

using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Helpers.Dialects;

using Microsoft.Data.SqlClient;

using MySql.Data.MySqlClient;

using Npgsql;

namespace Jaunty.Tests.Integration.StoredProcedure;

public sealed class StoredProcedureDialectAsyncTests
{
    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureAsync_WithResults_ReturnsEntities(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var products = await connection.ExecuteStoredProcedureAsync<Product>("GetAllProducts");

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var products = await connection.ExecuteStoredProcedureAsync<Product>("GetProductsByCategory", CategoryParam(dialect, 1));

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            var products = await connection.ExecuteStoredProcedureAsync(
                "GetProductsByCategory",
                CategoryParam(dialect, 1),
                CommandOptions<Product>.WithTransaction(transaction));

            Assert.NotEmpty(products);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var product = await connection.ExecuteStoredProcedureFirstAsync<Product>("GetProductById", ProductParam(dialect, 1));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            var product = await connection.ExecuteStoredProcedureFirstAsync<Product>(
                "GetProductById",
                ProductParam(dialect, 1),
                CommandOptions<Product>.WithTransaction(transaction));

            Assert.NotNull(product);
            Assert.Equal(1, product.ProductId);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureFirstAsync_NoResults_Throws(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connection.ExecuteStoredProcedureFirstAsync<Product>("GetNoResults"));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var product = await connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>("GetProductById", ProductParam(dialect, 1));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var product = await connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>("GetNoResults");

        Assert.Null(product);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            var product = await connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>(
                "GetProductById",
                ProductParam(dialect, 1),
                CommandOptions<Product>.WithTransaction(transaction));

            Assert.NotNull(product);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureScalarAsync_ReturnsScalarValue(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var count = await connection.ExecuteStoredProcedureScalarAsync<int>("GetProductCount");

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var count = await connection.ExecuteStoredProcedureScalarAsync<int>("GetProductCountByCategory", CategoryParam(dialect, 1));

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            var count = await connection.ExecuteStoredProcedureScalarAsync<int>(
                "GetProductCountByCategory",
                CategoryParam(dialect, 1),
                CommandOptions<int>.WithTransaction(transaction));

            Assert.True(count > 0);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteStoredProcedureNonQueryAsync(
                "UpdateProductPrice",
                UpdatePriceParam(dialect, 1, 99.99m),
                CommandOptions.WithTransaction(transaction));
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var parameters = new SpParameters().AddInput(OutputCategoryParamName(dialect), 1);

        if (UsesInOutForOutput(dialect))
            parameters.AddInputOutput(OutputCountParamName(dialect), 0, DbType.Int32);
        else
            parameters.AddOutput(OutputCountParamName(dialect), DbType.Int32);

        await connection.ExecuteStoredProcedureNonQueryAsync("GetProductCountWithOutput", parameters);

        var count = parameters.Get<int>(OutputCountParamName(dialect));
        Assert.True(count > 0);
    }

    private static DbConnection CreateConnection(DialectInfo dialect)
    {
        return dialect.Provider switch
        {
            DialectProvider.SqlServer => new SqlConnection(TestConfiguration.SqlServerConnectionString),
            DialectProvider.Postgres => new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString),
            DialectProvider.MariaDb => new MySqlConnection(TestConfiguration.MariaDbConnectionString),
            _ => throw new InvalidOperationException($"Unsupported dialect provider for stored procedures: {dialect.Provider}")
        };
    }

    private static object CategoryParam(DialectInfo dialect, int id) => dialect.Provider switch
    {
        DialectProvider.Postgres => new { p_category_id = id },
        DialectProvider.MariaDb => new { p_CategoryId = id },
        _ => new { CategoryId = id }
    };

    private static object ProductParam(DialectInfo dialect, int id) => dialect.Provider switch
    {
        DialectProvider.Postgres => new { p_product_id = id },
        DialectProvider.MariaDb => new { p_ProductId = id },
        _ => new { ProductId = id }
    };

    private static object UpdatePriceParam(DialectInfo dialect, int productId, decimal newPrice) => dialect.Provider switch
    {
        DialectProvider.Postgres => new { p_product_id = productId, p_new_price = newPrice },
        DialectProvider.MariaDb => new { p_ProductId = productId, p_NewPrice = newPrice },
        _ => new { ProductId = productId, NewPrice = newPrice }
    };

    private static bool UsesInOutForOutput(DialectInfo dialect) => dialect.Provider == DialectProvider.Postgres;

    private static string OutputCategoryParamName(DialectInfo dialect) => dialect.Provider switch
    {
        DialectProvider.Postgres => "p_category_id",
        DialectProvider.MariaDb => "p_CategoryId",
        _ => "CategoryId"
    };

    private static string OutputCountParamName(DialectInfo dialect) => dialect.Provider switch
    {
        DialectProvider.Postgres => "p_product_count",
        DialectProvider.MariaDb => "p_ProductCount",
        _ => "ProductCount"
    };
}
