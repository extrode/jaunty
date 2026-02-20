using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Entities;
using Xunit;

namespace Jaunty.Tests.Integration.SqlServer.StoredProcedure;

/// <summary>
/// Async tests for Jaunty stored procedure methods against SQL Server.
/// </summary>
public class SqlServerStoredProcedureAsyncTests : IDisposable
{
    private readonly SqlConnection _connection;

    public SqlServerStoredProcedureAsyncTests()
    {
        _connection = new SqlConnection(TestConfiguration.SqlServerConnectionString);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    #region ExecuteStoredProcedureAsync (returns List<T>)

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureAsync_WithResults_ReturnsEntities()
    {
        var products = await _connection.ExecuteStoredProcedureAsync<Product>("GetAllProducts");

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductId > 0));
    }

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults()
    {
        var products = await _connection.ExecuteStoredProcedureAsync<Product>(
            "GetProductsByCategory",
            new { CategoryId = 1 });

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureAsync_WithParametersAndOptions_Works()
    {
        await _connection.OpenAsync();
        using var transaction = (SqlTransaction)_connection.BeginTransaction();
        try
        {
            var products = await _connection.ExecuteStoredProcedureAsync<Product>(
                "GetProductsByCategory",
                new { CategoryId = 1 },
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

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst()
    {
        var product = await _connection.ExecuteStoredProcedureFirstAsync<Product>(
            "GetProductById",
            new { ProductId = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works()
    {
        await _connection.OpenAsync();
        using var transaction = (SqlTransaction)_connection.BeginTransaction();
        try
        {
            var product = await _connection.ExecuteStoredProcedureFirstAsync<Product>(
                "GetProductById",
                new { ProductId = 1 },
                CommandOptions<Product>.WithTransaction(transaction));

            Assert.NotNull(product);
            Assert.Equal(1, product.ProductId);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureFirstAsync_NoResults_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _connection.ExecuteStoredProcedureFirstAsync<Product>("GetNoResults"));
    }

    #endregion

    #region ExecuteStoredProcedureFirstOrDefaultAsync

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst()
    {
        var product = await _connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>(
            "GetProductById",
            new { ProductId = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        var product = await _connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>("GetNoResults");

        Assert.Null(product);
    }

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works()
    {
        await _connection.OpenAsync();
        using var transaction = (SqlTransaction)_connection.BeginTransaction();
        try
        {
            var product = await _connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>(
                "GetProductById",
                new { ProductId = 1 },
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

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureScalarAsync_ReturnsScalarValue()
    {
        var count = await _connection.ExecuteStoredProcedureScalarAsync<int>("GetProductCount");

        Assert.True(count > 0);
    }

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue()
    {
        var count = await _connection.ExecuteStoredProcedureScalarAsync<int>(
            "GetProductCountByCategory",
            new { CategoryId = 1 });

        Assert.True(count > 0);
    }

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works()
    {
        await _connection.OpenAsync();
        using var transaction = (SqlTransaction)_connection.BeginTransaction();
        try
        {
            var count = await _connection.ExecuteStoredProcedureScalarAsync<int>(
                "GetProductCountByCategory",
                new { CategoryId = 1 },
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

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully()
    {
        await _connection.OpenAsync();
        using var transaction = (SqlTransaction)_connection.BeginTransaction();
        try
        {
            var rowsAffected = await _connection.ExecuteStoredProcedureNonQueryAsync(
                "UpdateProductPrice",
                new { ProductId = 1, NewPrice = 99.99m },
                CommandOptions.WithTransaction(transaction));

            Assert.True(rowsAffected >= 0);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureNonQueryAsync_WithParameters_ExecutesSuccessfully()
    {
        await _connection.OpenAsync();
        using var transaction = (SqlTransaction)_connection.BeginTransaction();
        try
        {
            var rowsAffected = await _connection.ExecuteStoredProcedureNonQueryAsync(
                "UpdateProductPrice",
                new { ProductId = 1, NewPrice = 50.00m },
                CommandOptions.WithTransaction(transaction));

            Assert.True(rowsAffected >= 0);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    #endregion

    #region ExecuteStoredProcedure with SpParameters (Output) - Async

    [SkipIfNoSqlServerFact]
    public async Task ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue()
    {
        var parameters = new SpParameters()
            .AddInput("CategoryId", 1)
            .AddOutput("ProductCount", DbType.Int32);

        await _connection.ExecuteStoredProcedureNonQueryAsync("GetProductCountWithOutput", parameters);

        var count = parameters.Get<int>("ProductCount");
        Assert.True(count > 0);
    }

    #endregion
}
