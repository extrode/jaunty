using System;
using System.Data;
using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Xunit;

namespace Jaunty.Tests.Integration.StoredProcedure;

/// <summary>
/// Abstract base class containing all stored procedure sync test logic.
/// Provider subclasses supply the connection and parameter naming conventions.
/// </summary>
public abstract class StoredProcedureTestBase : IDisposable
{
    private readonly IDbConnection _connection;

    protected StoredProcedureTestBase()
    {
        _connection = CreateConnection();
    }

    protected abstract IDbConnection CreateConnection();

    /// <summary>
    /// Creates a parameter object for CategoryId. Override for PostgreSQL p_ prefix.
    /// </summary>
    protected virtual object CategoryParam(int id) => new { CategoryId = id };

    /// <summary>
    /// Creates a parameter object for ProductId. Override for PostgreSQL p_ prefix.
    /// </summary>
    protected virtual object ProductParam(int id) => new { ProductId = id };

    /// <summary>
    /// Creates a parameter object for UpdateProductPrice. Override for PostgreSQL p_ prefix.
    /// </summary>
    protected virtual object UpdatePriceParam(int productId, decimal newPrice) =>
        new { ProductId = productId, NewPrice = newPrice };

    /// <summary>
    /// Whether this provider uses INOUT instead of OUTPUT for output params.
    /// PostgreSQL uses INOUT; SQL Server and MySQL use OUTPUT.
    /// </summary>
    protected virtual bool UsesInOutForOutput => false;

    /// <summary>
    /// Parameter names for output parameter test. Override for PostgreSQL p_ prefix.
    /// </summary>
    protected virtual string OutputCategoryParamName => "CategoryId";
    protected virtual string OutputCountParamName => "ProductCount";

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    #region ExecuteStoredProcedure (returns List<T>)

    protected void ExecuteStoredProcedure_WithResults_ReturnsEntities_Core()
    {
        var products = _connection.ExecuteStoredProcedure<Product>("GetAllProducts");

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductId > 0));
    }

    protected void ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults_Core()
    {
        var products = _connection.ExecuteStoredProcedure<Product>(
            "GetProductsByCategory",
            CategoryParam(1));

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    protected void ExecuteStoredProcedure_WithParametersAndOptions_Works_Core()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var products = _connection.ExecuteStoredProcedure<Product>(
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

    #region ExecuteStoredProcedureFirst

    protected void ExecuteStoredProcedureFirst_WithResults_ReturnsFirst_Core()
    {
        var product = _connection.ExecuteStoredProcedureFirst<Product>(
            "GetProductById",
            ProductParam(1));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    protected void ExecuteStoredProcedureFirst_WithParametersAndOptions_Works_Core()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var product = _connection.ExecuteStoredProcedureFirst<Product>(
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

    protected void ExecuteStoredProcedureFirst_NoResults_Throws_Core()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _connection.ExecuteStoredProcedureFirst<Product>("GetNoResults"));
    }

    #endregion

    #region ExecuteStoredProcedureFirstOrDefault

    protected void ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst_Core()
    {
        var product = _connection.ExecuteStoredProcedureFirstOrDefault<Product>(
            "GetProductById",
            ProductParam(1));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    protected void ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull_Core()
    {
        var product = _connection.ExecuteStoredProcedureFirstOrDefault<Product>("GetNoResults");

        Assert.Null(product);
    }

    protected void ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works_Core()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var product = _connection.ExecuteStoredProcedureFirstOrDefault<Product>(
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

    #region ExecuteStoredProcedureScalar

    protected void ExecuteStoredProcedureScalar_ReturnsScalarValue_Core()
    {
        var count = _connection.ExecuteStoredProcedureScalar<int>("GetProductCount");

        Assert.True(count > 0);
    }

    protected void ExecuteStoredProcedureScalar_WithParameters_ReturnsValue_Core()
    {
        var count = _connection.ExecuteStoredProcedureScalar<int>(
            "GetProductCountByCategory",
            CategoryParam(1));

        Assert.True(count > 0);
    }

    protected void ExecuteStoredProcedureScalar_WithParametersAndOptions_Works_Core()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var count = _connection.ExecuteStoredProcedureScalar<int>(
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

    #region ExecuteStoredProcedureNonQuery

    protected void ExecuteStoredProcedureNonQuery_ExecutesSuccessfully_Core()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            _connection.ExecuteStoredProcedureNonQuery(
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

    #region SpParameters (Output)

    protected void ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue_Core()
    {
        var parameters = new SpParameters()
            .AddInput(OutputCategoryParamName, 1);

        if (UsesInOutForOutput)
            parameters.AddInputOutput(OutputCountParamName, 0, DbType.Int32);
        else
            parameters.AddOutput(OutputCountParamName, DbType.Int32);

        _connection.ExecuteStoredProcedureNonQuery("GetProductCountWithOutput", parameters);

        var count = parameters.Get<int>(OutputCountParamName);
        Assert.True(count > 0);
    }

    protected void SpParameters_HasValue_ReturnsTrueForOutputWithValue_Core()
    {
        var parameters = new SpParameters()
            .AddInput(OutputCategoryParamName, 1);

        if (UsesInOutForOutput)
            parameters.AddInputOutput(OutputCountParamName, 0, DbType.Int32);
        else
            parameters.AddOutput(OutputCountParamName, DbType.Int32);

        _connection.ExecuteStoredProcedureNonQuery("GetProductCountWithOutput", parameters);

        Assert.True(parameters.HasValue(OutputCountParamName));
    }

    #endregion
}
