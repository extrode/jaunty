using System;
using System.Data;
using Npgsql;
using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Entities;
using Xunit;

namespace Jaunty.Tests.Integration.Postgres.StoredProcedure;

/// <summary>
/// Tests for Jaunty stored procedure methods against PostgreSQL.
/// These tests require a PostgreSQL instance with a Northwind database
/// and the functions created by data/postgres/create-stored-procedures.sql.
///
/// Configure via:
///   - Environment variable: JAUNTY_TEST_POSTGRESQL
///   - Or appsettings.json: ConnectionStrings:PostgreSql
/// </summary>
public class PostgresStoredProcedureTests : IDisposable
{
    private readonly NpgsqlConnection _connection;

    public PostgresStoredProcedureTests()
    {
        _connection = new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    #region ExecuteStoredProcedure (returns List<T>)

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedure_WithResults_ReturnsEntities()
    {
        var products = _connection.ExecuteStoredProcedure<Product>("GetAllProducts");

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductId > 0));
    }

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults()
    {
        var products = _connection.ExecuteStoredProcedure<Product>(
            "GetProductsByCategory",
            new { p_category_id = 1 });

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedure_WithParametersAndOptions_Works()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var products = _connection.ExecuteStoredProcedure<Product>(
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

    #region ExecuteStoredProcedureFirst

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureFirst_WithResults_ReturnsFirst()
    {
        var product = _connection.ExecuteStoredProcedureFirst<Product>(
            "GetProductById",
            new { p_product_id = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureFirst_WithParametersAndOptions_Works()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var product = _connection.ExecuteStoredProcedureFirst<Product>(
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
    public void ExecuteStoredProcedureFirst_NoResults_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _connection.ExecuteStoredProcedureFirst<Product>("GetNoResults"));
    }

    #endregion

    #region ExecuteStoredProcedureFirstOrDefault

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst()
    {
        var product = _connection.ExecuteStoredProcedureFirstOrDefault<Product>(
            "GetProductById",
            new { p_product_id = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull()
    {
        var product = _connection.ExecuteStoredProcedureFirstOrDefault<Product>("GetNoResults");

        Assert.Null(product);
    }

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var product = _connection.ExecuteStoredProcedureFirstOrDefault<Product>(
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

    #region ExecuteStoredProcedureScalar

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureScalar_ReturnsScalarValue()
    {
        var count = _connection.ExecuteStoredProcedureScalar<int>("GetProductCount");

        Assert.True(count > 0);
    }

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureScalar_WithParameters_ReturnsValue()
    {
        var count = _connection.ExecuteStoredProcedureScalar<int>(
            "GetProductCountByCategory",
            new { p_category_id = 1 });

        Assert.True(count > 0);
    }

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureScalar_WithParametersAndOptions_Works()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            var count = _connection.ExecuteStoredProcedureScalar<int>(
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

    #region ExecuteStoredProcedureNonQuery

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureNonQuery_ExecutesSuccessfully()
    {
        _connection.Open();
        using var transaction = _connection.BeginTransaction();
        try
        {
            _connection.ExecuteStoredProcedureNonQuery(
                "UpdateProductPrice",
                new { p_product_id = 1, p_new_price = 99.99m },
                CommandOptions.WithTransaction(transaction));

            // PostgreSQL VOID functions return -1 from ExecuteNonQuery
            // Just verifying no exception is thrown
        }
        finally
        {
            transaction.Rollback();
        }
    }

    #endregion

    #region ExecuteStoredProcedure with SpParameters (Output via INOUT)

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue()
    {
        var parameters = new SpParameters()
            .AddInput("p_category_id", 1)
            .AddInputOutput("p_product_count", 0, DbType.Int32);

        _connection.ExecuteStoredProcedureNonQuery("GetProductCountWithOutput", parameters);

        var count = parameters.Get<int>("p_product_count");
        Assert.True(count > 0);
    }

    #endregion
}
