using System.Data;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.StoredProcedure;

/// <summary>
/// Integration tests for stored procedure execution across all supported databases.
/// Uses dialect-specific attributes to run the same test logic against different databases.
/// </summary>
public class StoredProcedureTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public StoredProcedureTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    #region Helper Methods

    /// <summary>
    /// Creates parameters for CategoryId. PostgreSQL uses p_ prefix.
    /// </summary>
    private static object CategoryParam(DialectInfo dialect, int id) =>
        dialect.Provider == DialectProvider.Postgres
            ? new { p_category_id = id }
            : new { CategoryId = id };

    /// <summary>
    /// Creates parameters for ProductId. PostgreSQL uses p_ prefix.
    /// </summary>
    private static object ProductParam(DialectInfo dialect, int id) =>
        dialect.Provider == DialectProvider.Postgres
            ? new { p_product_id = id }
            : new { ProductId = id };

    /// <summary>
    /// Creates parameters for UpdateProductPrice. PostgreSQL uses p_ prefix.
    /// </summary>
    private static object UpdatePriceParam(DialectInfo dialect, int productId, decimal newPrice) =>
        dialect.Provider == DialectProvider.Postgres
            ? new { p_product_id = productId, p_new_price = newPrice }
            : new { ProductId = productId, NewPrice = newPrice };

    /// <summary>
    /// PostgreSQL uses INOUT instead of OUTPUT for output parameters.
    /// </summary>
    private static bool UsesInOutForOutput(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres;

    /// <summary>
    /// Output parameter names. PostgreSQL uses p_ prefix.
    /// </summary>
    private static string OutputCategoryParamName(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres ? "p_category_id" : "CategoryId";

    private static string OutputCountParamName(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres ? "p_product_count" : "ProductCount";

    #endregion

    #region ExecuteStoredProcedure (returns List<T>)

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedure_WithResults_ReturnsEntities(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var products = connection.ExecuteStoredProcedure<Product>("GetAllProducts");

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var products = connection.ExecuteStoredProcedure<Product>(
            "GetProductsByCategory",
            CategoryParam(dialect, 1));

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedure_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var products = connection.ExecuteStoredProcedure<Product>(
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

    #endregion

    #region ExecuteStoredProcedureFirst

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureFirst_WithResults_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.ExecuteStoredProcedureFirst<Product>(
            "GetProductById",
            ProductParam(dialect, 1));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureFirst_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var product = connection.ExecuteStoredProcedureFirst<Product>(
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
    public void ExecuteStoredProcedureFirst_NoResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        Assert.Throws<InvalidOperationException>(() =>
            connection.ExecuteStoredProcedureFirst<Product>("GetNoResults"));
    }

    #endregion

    #region ExecuteStoredProcedureFirstOrDefault

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.ExecuteStoredProcedureFirstOrDefault<Product>(
            "GetProductById",
            ProductParam(dialect, 1));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.ExecuteStoredProcedureFirstOrDefault<Product>("GetNoResults");

        Assert.Null(product);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var product = connection.ExecuteStoredProcedureFirstOrDefault<Product>(
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

    #endregion

    #region ExecuteStoredProcedureScalar

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureScalar_ReturnsScalarValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = connection.ExecuteStoredProcedureScalar<int>("GetProductCount");

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureScalar_WithParameters_ReturnsValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = connection.ExecuteStoredProcedureScalar<int>(
            "GetProductCountByCategory",
            CategoryParam(dialect, 1));

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureScalar_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var count = connection.ExecuteStoredProcedureScalar<int>(
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

    #endregion

    #region ExecuteStoredProcedureNonQuery

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureNonQuery_ExecutesSuccessfully(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            connection.ExecuteStoredProcedureNonQuery(
                "UpdateProductPrice",
                UpdatePriceParam(dialect, 1, 99.99m),
                CommandOptions.WithTransaction(transaction));
        }
        finally
        {
            transaction.Rollback();
        }
    }

    #endregion

    #region SpParameters (Output)

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var parameters = new SpParameters()
            .AddInput(OutputCategoryParamName(dialect), 1);

        if (UsesInOutForOutput(dialect))
            parameters.AddInputOutput(OutputCountParamName(dialect), 0, DbType.Int32);
        else
            parameters.AddOutput(OutputCountParamName(dialect), DbType.Int32);

        connection.ExecuteStoredProcedureNonQuery("GetProductCountWithOutput", parameters);

        var count = parameters.Get<int>(OutputCountParamName(dialect));
        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void SpParameters_HasValue_ReturnsTrueForOutputWithValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var parameters = new SpParameters()
            .AddInput(OutputCategoryParamName(dialect), 1);

        if (UsesInOutForOutput(dialect))
            parameters.AddInputOutput(OutputCountParamName(dialect), 0, DbType.Int32);
        else
            parameters.AddOutput(OutputCountParamName(dialect), DbType.Int32);

        connection.ExecuteStoredProcedureNonQuery("GetProductCountWithOutput", parameters);

        Assert.True(parameters.HasValue(OutputCountParamName(dialect)));
    }

    #endregion
}
