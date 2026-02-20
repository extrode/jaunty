using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Xunit;

namespace Jaunty.Tests.Integration.StoredProcedure;

/// <summary>
/// Abstract base class containing all stored procedure async test logic.
/// Provider subclasses supply the connection and parameter naming conventions.
/// </summary>
public abstract class StoredProcedureAsyncTestBase : IDisposable
{
    private readonly DbConnection _connection;

    protected StoredProcedureAsyncTestBase()
    {
        _connection = CreateConnection();
    }

    protected abstract DbConnection CreateConnection();

    protected virtual object CategoryParam(int id) => new { CategoryId = id };
    protected virtual object ProductParam(int id) => new { ProductId = id };
    protected virtual object UpdatePriceParam(int productId, decimal newPrice) =>
        new { ProductId = productId, NewPrice = newPrice };

    protected virtual bool UsesInOutForOutput => false;
    protected virtual string OutputCategoryParamName => "CategoryId";
    protected virtual string OutputCountParamName => "ProductCount";

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    #region ExecuteStoredProcedureAsync (returns List<T>)

    protected async Task ExecuteStoredProcedureAsync_WithResults_ReturnsEntities_Core()
    {
        var products = await _connection.ExecuteStoredProcedureAsync<Product>("GetAllProducts");

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductId > 0));
    }

    protected async Task ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults_Core()
    {
        var products = await _connection.ExecuteStoredProcedureAsync<Product>(
            "GetProductsByCategory",
            CategoryParam(1));

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    protected async Task ExecuteStoredProcedureAsync_WithParametersAndOptions_Works_Core()
    {
        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var products = await _connection.ExecuteStoredProcedureAsync<Product>(
                "GetProductsByCategory",
                CategoryParam(1),
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

    protected async Task ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst_Core()
    {
        var product = await _connection.ExecuteStoredProcedureFirstAsync<Product>(
            "GetProductById",
            ProductParam(1));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    protected async Task ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works_Core()
    {
        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var product = await _connection.ExecuteStoredProcedureFirstAsync<Product>(
                "GetProductById",
                ProductParam(1),
                CommandOptions<Product>.WithTransaction(transaction));

            Assert.NotNull(product);
            Assert.Equal(1, product.ProductId);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    protected async Task ExecuteStoredProcedureFirstAsync_NoResults_Throws_Core()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _connection.ExecuteStoredProcedureFirstAsync<Product>("GetNoResults"));
    }

    #endregion

    #region ExecuteStoredProcedureFirstOrDefaultAsync

    protected async Task ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst_Core()
    {
        var product = await _connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>(
            "GetProductById",
            ProductParam(1));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    protected async Task ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull_Core()
    {
        var product = await _connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>("GetNoResults");

        Assert.Null(product);
    }

    protected async Task ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works_Core()
    {
        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var product = await _connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>(
                "GetProductById",
                ProductParam(1),
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

    protected async Task ExecuteStoredProcedureScalarAsync_ReturnsScalarValue_Core()
    {
        var count = await _connection.ExecuteStoredProcedureScalarAsync<int>("GetProductCount");

        Assert.True(count > 0);
    }

    protected async Task ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue_Core()
    {
        var count = await _connection.ExecuteStoredProcedureScalarAsync<int>(
            "GetProductCountByCategory",
            CategoryParam(1));

        Assert.True(count > 0);
    }

    protected async Task ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works_Core()
    {
        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var count = await _connection.ExecuteStoredProcedureScalarAsync<int>(
                "GetProductCountByCategory",
                CategoryParam(1),
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

    protected async Task ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully_Core()
    {
        await _connection.OpenAsync();
        using var transaction = _connection.BeginTransaction();
        try
        {
            await _connection.ExecuteStoredProcedureNonQueryAsync(
                "UpdateProductPrice",
                UpdatePriceParam(1, 99.99m),
                CommandOptions.WithTransaction(transaction));
        }
        finally
        {
            transaction.Rollback();
        }
    }

    #endregion

    #region SpParameters (Output) - Async

    protected async Task ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue_Core()
    {
        var parameters = new SpParameters()
            .AddInput(OutputCategoryParamName, 1);

        if (UsesInOutForOutput)
            parameters.AddInputOutput(OutputCountParamName, 0, DbType.Int32);
        else
            parameters.AddOutput(OutputCountParamName, DbType.Int32);

        await _connection.ExecuteStoredProcedureNonQueryAsync("GetProductCountWithOutput", parameters);

        var count = parameters.Get<int>(OutputCountParamName);
        Assert.True(count > 0);
    }

    #endregion
}
