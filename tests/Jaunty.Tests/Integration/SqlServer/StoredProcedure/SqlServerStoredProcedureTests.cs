using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Entities;
using Xunit;

namespace Jaunty.Tests.Integration.SqlServer.StoredProcedure;

/// <summary>
/// Tests for Jaunty stored procedure methods against SQL Server.
/// These tests require a SQL Server instance with a Northwind database
/// and the stored procedures created by data/sqlserver/create-stored-procedures.sql.
///
/// Configure via:
///   - Environment variable: JAUNTY_TEST_SQLSERVER
///   - Or appsettings.json: ConnectionStrings:SqlServer
/// </summary>
public class SqlServerStoredProcedureTests : IDisposable
{
    private readonly SqlConnection _connection;

    public SqlServerStoredProcedureTests()
    {
        _connection = new SqlConnection(TestConfiguration.SqlServerConnectionString);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    #region ExecuteStoredProcedure (returns List<T>)

    [SkipIfNoSqlServerFact]
    public void ExecuteStoredProcedure_WithResults_ReturnsEntities()
    {
        var products = _connection.ExecuteStoredProcedure<Product>("GetAllProducts");

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductId > 0));
    }

    [SkipIfNoSqlServerFact]
    public void ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults()
    {
        var products = _connection.ExecuteStoredProcedure<Product>(
            "GetProductsByCategory",
            new { CategoryId = 1 });

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [SkipIfNoSqlServerFact]
    public void ExecuteStoredProcedure_WithParametersAndOptions_Works()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var products = _connection.ExecuteStoredProcedure<Product>(
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

    #region ExecuteStoredProcedureFirst

    [SkipIfNoSqlServerFact]
    public void ExecuteStoredProcedureFirst_WithResults_ReturnsFirst()
    {
        var product = _connection.ExecuteStoredProcedureFirst<Product>(
            "GetProductById",
            new { ProductId = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [SkipIfNoSqlServerFact]
    public void ExecuteStoredProcedureFirst_WithParametersAndOptions_Works()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var product = _connection.ExecuteStoredProcedureFirst<Product>(
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
    public void ExecuteStoredProcedureFirst_NoResults_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _connection.ExecuteStoredProcedureFirst<Product>("GetNoResults"));
    }

    #endregion

    #region ExecuteStoredProcedureFirstOrDefault

    [SkipIfNoSqlServerFact]
    public void ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst()
    {
        var product = _connection.ExecuteStoredProcedureFirstOrDefault<Product>(
            "GetProductById",
            new { ProductId = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [SkipIfNoSqlServerFact]
    public void ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull()
    {
        var product = _connection.ExecuteStoredProcedureFirstOrDefault<Product>("GetNoResults");

        Assert.Null(product);
    }

    [SkipIfNoSqlServerFact]
    public void ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var product = _connection.ExecuteStoredProcedureFirstOrDefault<Product>(
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

    #region ExecuteStoredProcedureScalar

    [SkipIfNoSqlServerFact]
    public void ExecuteStoredProcedureScalar_ReturnsScalarValue()
    {
        var count = _connection.ExecuteStoredProcedureScalar<int>("GetProductCount");

        Assert.True(count > 0);
    }

    [SkipIfNoSqlServerFact]
    public void ExecuteStoredProcedureScalar_WithParameters_ReturnsValue()
    {
        var count = _connection.ExecuteStoredProcedureScalar<int>(
            "GetProductCountByCategory",
            new { CategoryId = 1 });

        Assert.True(count > 0);
    }

    [SkipIfNoSqlServerFact]
    public void ExecuteStoredProcedureScalar_WithParametersAndOptions_Works()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var count = _connection.ExecuteStoredProcedureScalar<int>(
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

    #region ExecuteStoredProcedureNonQuery

    [SkipIfNoSqlServerFact]
    public void ExecuteStoredProcedureNonQuery_ExecutesSuccessfully()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var rowsAffected = _connection.ExecuteStoredProcedureNonQuery(
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
    public void ExecuteStoredProcedureNonQuery_WithParameters_ExecutesSuccessfully()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var rowsAffected = _connection.ExecuteStoredProcedureNonQuery(
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

    #region ExecuteStoredProcedure with SpParameters (Output)

    [SkipIfNoSqlServerFact]
    public void ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue()
    {
        var parameters = new SpParameters()
            .AddInput("CategoryId", 1)
            .AddOutput("ProductCount", DbType.Int32);

        _connection.ExecuteStoredProcedureNonQuery("GetProductCountWithOutput", parameters);

        var count = parameters.Get<int>("ProductCount");
        Assert.True(count > 0);
    }

    [SkipIfNoSqlServerFact]
    public void SpParameters_HasValue_ReturnsTrueForOutputWithValue()
    {
        var parameters = new SpParameters()
            .AddInput("CategoryId", 1)
            .AddOutput("ProductCount", DbType.Int32);

        _connection.ExecuteStoredProcedureNonQuery("GetProductCountWithOutput", parameters);

        Assert.True(parameters.HasValue("ProductCount"));
    }

    #endregion
}
