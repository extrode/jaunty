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
    /// Gets stored procedure name. PostgreSQL folds unquoted names to lowercase.
    /// </summary>
    private static string SpName(string name, DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres ? name.ToLower() : name;

    /// <summary>
    /// Creates parameters for CategoryId. PostgreSQL uses p_category_id, MariaDB uses p_CategoryId.
    /// </summary>
    private static object CategoryParam(DialectInfo dialect, int id) =>
        dialect.Provider == DialectProvider.Postgres
            ? new { p_category_id = id }
            : dialect.Provider == DialectProvider.MariaDb
            ? new { p_CategoryId = id }
            : new { CategoryId = id };

    /// <summary>
    /// Creates parameters for ProductId. PostgreSQL uses p_product_id, MariaDB uses p_ProductId.
    /// </summary>
    private static object ProductParam(DialectInfo dialect, int id) =>
        dialect.Provider == DialectProvider.Postgres
            ? new { p_product_id = id }
            : dialect.Provider == DialectProvider.MariaDb
            ? new { p_ProductId = id }
            : new { ProductId = id };

    /// <summary>
    /// Creates parameters for UpdateProductPrice. PostgreSQL/MariaDB use p_ prefix with different casing.
    /// </summary>
    private static object UpdatePriceParam(DialectInfo dialect, int productId, decimal newPrice) =>
        dialect.Provider == DialectProvider.Postgres
            ? new { p_product_id = productId, p_new_price = newPrice }
            : dialect.Provider == DialectProvider.MariaDb
            ? new { p_ProductId = productId, p_NewPrice = newPrice }
            : new { ProductId = productId, NewPrice = newPrice };

    /// <summary>
    /// PostgreSQL uses INOUT instead of OUTPUT for output parameters.
    /// </summary>
    private static bool UsesInOutForOutput(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres;

    /// <summary>
    /// Output parameter names. PostgreSQL uses p_category_id, MariaDB uses p_CategoryId.
    /// </summary>
    private static string OutputCategoryParamName(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres ? "p_category_id" :
        dialect.Provider == DialectProvider.MariaDb ? "p_CategoryId" : "CategoryId";

    private static string OutputCountParamName(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres ? "p_product_count" :
        dialect.Provider == DialectProvider.MariaDb ? "p_ProductCount" : "ProductCount";

    #endregion

    #region ExecuteStoredProcedure (returns List<T>)

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedure_WithResults_ReturnsEntities(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var products = connection.ExecuteStoredProcedure<Product>(SpName("GetAllProducts", dialect));

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
            SpName("GetProductsByCategory", dialect),
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
        using var transaction = connection.BeginTransaction();
        try
        {
            var products = connection.ExecuteStoredProcedure<Product>(
                SpName("GetProductsByCategory", dialect),
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
            SpName("GetProductById", dialect),
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
        using var transaction = connection.BeginTransaction();
        try
        {
            var product = connection.ExecuteStoredProcedureFirst<Product>(
                SpName("GetProductById", dialect),
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
            connection.ExecuteStoredProcedureFirst<Product>(SpName("GetNoResults", dialect)));
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
            SpName("GetProductById", dialect),
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

        var product = connection.ExecuteStoredProcedureFirstOrDefault<Product>(SpName("GetNoResults", dialect));

        Assert.Null(product);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var transaction = connection.BeginTransaction();
        try
        {
            var product = connection.ExecuteStoredProcedureFirstOrDefault<Product>(
                SpName("GetProductById", dialect),
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

        var count = connection.ExecuteStoredProcedureScalar<int>(SpName("GetProductCount", dialect));

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
            SpName("GetProductCountByCategory", dialect),
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
        using var transaction = connection.BeginTransaction();
        try
        {
            var count = connection.ExecuteStoredProcedureScalar<int>(
                SpName("GetProductCountByCategory", dialect),
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
        using var transaction = connection.BeginTransaction();
        try
        {
            connection.ExecuteStoredProcedureNonQuery(
                SpName("UpdateProductPrice", dialect),
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

        connection.ExecuteStoredProcedureNonQuery(SpName("GetProductCountWithOutput", dialect), parameters);

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

        connection.ExecuteStoredProcedureNonQuery(SpName("GetProductCountWithOutput", dialect), parameters);

        Assert.True(parameters.HasValue(OutputCountParamName(dialect)));
    }

    #endregion
}
