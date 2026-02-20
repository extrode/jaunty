using System;
using System.Data;
using System.Threading.Tasks;
using Npgsql;
using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Entities;
using Xunit;

namespace Jaunty.Tests.Integration.Postgres.StoredProcedure;

/// <summary>
/// Async tests for Jaunty stored procedure methods against PostgreSQL.
/// </summary>
public class PostgresStoredProcedureAsyncTests : IDisposable
{
    private readonly NpgsqlConnection _connection;

    public PostgresStoredProcedureAsyncTests()
    {
        _connection = new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    #region ExecuteStoredProcedureAsync (returns List<T>)

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureAsync_WithResults_ReturnsEntities()
    {
        var products = await _connection.ExecuteStoredProcedureAsync<Product>("GetAllProducts");

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductId > 0));
    }

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults()
    {
        var products = await _connection.ExecuteStoredProcedureAsync<Product>(
            "GetProductsByCategory",
            new { p_category_id = 1 });

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureAsync_WithParametersAndOptions_Works()
    {
        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var products = await _connection.ExecuteStoredProcedureAsync<Product>(
                "GetProductsByCategory",
                new { p_category_id = 1 },
                CommandOptions<Product>.WithTransaction(transaction));

            Assert.NotEmpty(products);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    #endregion

    #region ExecuteStoredProcedureFirstAsync

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst()
    {
        var product = await _connection.ExecuteStoredProcedureFirstAsync<Product>(
            "GetProductById",
            new { p_product_id = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works()
    {
        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var product = await _connection.ExecuteStoredProcedureFirstAsync<Product>(
                "GetProductById",
                new { p_product_id = 1 },
                CommandOptions<Product>.WithTransaction(transaction));

            Assert.NotNull(product);
            Assert.Equal(1, product.ProductId);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureFirstAsync_NoResults_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _connection.ExecuteStoredProcedureFirstAsync<Product>("GetNoResults"));
    }

    #endregion

    #region ExecuteStoredProcedureFirstOrDefaultAsync

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst()
    {
        var product = await _connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>(
            "GetProductById",
            new { p_product_id = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        var product = await _connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>("GetNoResults");

        Assert.Null(product);
    }

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works()
    {
        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var product = await _connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>(
                "GetProductById",
                new { p_product_id = 1 },
                CommandOptions<Product>.WithTransaction(transaction));

            Assert.NotNull(product);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    #endregion

    #region ExecuteStoredProcedureScalarAsync

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureScalarAsync_ReturnsScalarValue()
    {
        var count = await _connection.ExecuteStoredProcedureScalarAsync<int>("GetProductCount");

        Assert.True(count > 0);
    }

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue()
    {
        var count = await _connection.ExecuteStoredProcedureScalarAsync<int>(
            "GetProductCountByCategory",
            new { p_category_id = 1 });

        Assert.True(count > 0);
    }

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works()
    {
        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var count = await _connection.ExecuteStoredProcedureScalarAsync<int>(
                "GetProductCountByCategory",
                new { p_category_id = 1 },
                CommandOptions<int>.WithTransaction(transaction));

            Assert.True(count > 0);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    #endregion

    #region ExecuteStoredProcedureNonQueryAsync

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully()
    {
        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();
        try
        {
            await _connection.ExecuteStoredProcedureNonQueryAsync(
                "UpdateProductPrice",
                new { p_product_id = 1, p_new_price = 99.99m },
                CommandOptions.WithTransaction(transaction));
        }
        finally
        {
            transaction.Rollback();
        }
    }

    #endregion

    #region ExecuteStoredProcedure with SpParameters (Output via INOUT) - Async

    [SkipIfNoPostgresFact]
    public async Task ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue()
    {
        var parameters = new SpParameters()
            .AddInput("p_category_id", 1)
            .AddInputOutput("p_product_count", 0, DbType.Int32);

        await _connection.ExecuteStoredProcedureNonQueryAsync("GetProductCountWithOutput", parameters);

        var count = parameters.Get<int>("p_product_count");
        Assert.True(count > 0);
    }

    #endregion
}
