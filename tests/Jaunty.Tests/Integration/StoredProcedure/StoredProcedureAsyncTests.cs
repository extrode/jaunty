using Jaunty.Core;
using Jaunty.StoredProcedure;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.StoredProcedure;

/// <summary>
/// Async integration tests for stored procedure execution across all supported databases.
/// Uses dialect-specific attributes to run the same test logic against different databases.
/// </summary>
public class StoredProcedureAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public StoredProcedureAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    #region Helper Methods

    private static string SpName(string name, DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres ? name.ToLower() : name;

    private static object CategoryParam(DialectInfo dialect, int id) =>
        dialect.Provider == DialectProvider.Postgres
            ? new { p_category_id = id }
            : dialect.Provider == DialectProvider.MariaDb
            ? new { p_CategoryId = id }
            : new { CategoryId = id };

    private static object ProductParam(DialectInfo dialect, int id) =>
        dialect.Provider == DialectProvider.Postgres
            ? new { p_product_id = id }
            : dialect.Provider == DialectProvider.MariaDb
            ? new { p_ProductId = id }
            : new { ProductId = id };

    private static object UpdatePriceParam(DialectInfo dialect, int productId, decimal newPrice) =>
        dialect.Provider == DialectProvider.Postgres
            ? new { p_product_id = productId, p_new_price = newPrice }
            : dialect.Provider == DialectProvider.MariaDb
            ? new { p_ProductId = productId, p_NewPrice = newPrice }
            : new { ProductId = productId, NewPrice = newPrice };

    private static bool UsesInOutForOutput(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres;

    private static string OutputCategoryParamName(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres ? "p_category_id" :
        dialect.Provider == DialectProvider.MariaDb ? "p_CategoryId" : "CategoryId";

    private static string OutputCountParamName(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres ? "p_product_count" :
        dialect.Provider == DialectProvider.MariaDb ? "p_ProductCount" : "ProductCount";

    #endregion

    #region ExecuteStoredProcedureAsync

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureAsync_WithResults_ReturnsEntities(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var products = await connection.ExecuteStoredProcedureAsync<Product>(SpName("GetAllProducts", dialect));

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var products = await connection.ExecuteStoredProcedureAsync<Product>(
            SpName("GetProductsByCategory", dialect),
            CategoryParam(dialect, 1));

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var transaction = connection.BeginTransaction();
        try
        {
            var products = await connection.ExecuteStoredProcedureAsync(SpName("GetProductsByCategory", dialect),
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

    #region ExecuteStoredProcedureFirstAsync

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var product = await connection.ExecuteStoredProcedureFirstAsync<Product>(
            SpName("GetProductById", dialect),
            ProductParam(dialect, 1));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var transaction = connection.BeginTransaction();
        try
        {
            var product = await connection.ExecuteStoredProcedureFirstAsync<Product>(
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
    public async Task ExecuteStoredProcedureFirstAsync_NoResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await connection.ExecuteStoredProcedureFirstAsync<Product>(SpName("GetNoResults", dialect)));
    }

    #endregion

    #region ExecuteStoredProcedureFirstOrDefaultAsync

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var product = await connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>(
            SpName("GetProductById", dialect),
            ProductParam(dialect, 1));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var product = await connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>(SpName("GetNoResults", dialect));

        Assert.Null(product);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var transaction = connection.BeginTransaction();
        try
        {
            var product = await connection.ExecuteStoredProcedureFirstOrDefaultAsync<Product>(
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

    #region ExecuteStoredProcedureScalarAsync

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureScalarAsync_ReturnsScalarValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var count = await connection.ExecuteStoredProcedureScalarAsync<int>(SpName("GetProductCount", dialect));

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var count = await connection.ExecuteStoredProcedureScalarAsync<int>(
            SpName("GetProductCountByCategory", dialect),
            CategoryParam(dialect, 1));

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var transaction = connection.BeginTransaction();
        try
        {
            var count = await connection.ExecuteStoredProcedureScalarAsync<int>(
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

    #region ExecuteStoredProcedureNonQueryAsync

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteStoredProcedureNonQueryAsync(SpName("UpdateProductPrice", dialect),
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
    public async Task ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var parameters = new SpParameters()
            .AddInput(OutputCategoryParamName(dialect), 1);

        if (UsesInOutForOutput(dialect))
            parameters.AddInputOutput(OutputCountParamName(dialect), 0, DbType.Int32);
        else
            parameters.AddOutput(OutputCountParamName(dialect), DbType.Int32);

        await connection.ExecuteStoredProcedureNonQueryAsync(SpName("GetProductCountWithOutput", dialect), parameters);

        var count = parameters.Get<int>(OutputCountParamName(dialect));
        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task SpParameters_HasValue_ReturnsTrueForOutputWithValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var parameters = new SpParameters()
            .AddInput(OutputCategoryParamName(dialect), 1);

        if (UsesInOutForOutput(dialect))
            parameters.AddInputOutput(OutputCountParamName(dialect), 0, DbType.Int32);
        else
            parameters.AddOutput(OutputCountParamName(dialect), DbType.Int32);

        await connection.ExecuteStoredProcedureNonQueryAsync(SpName("GetProductCountWithOutput", dialect), parameters);

        Assert.True(parameters.HasValue(OutputCountParamName(dialect)));
    }

    [Theory]
    [SqlServer]
    [MariaDB]
    public async Task ExecuteStoredProcedureScalarAsync_SpParametersOverload_NoResultSet_ReturnsDefaultInsteadOfThrowing(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var parameters = new SpParameters()
            .AddInput(OutputCategoryParamName(dialect), 1)
            .AddOutput(OutputCountParamName(dialect), DbType.Int32);

        // GetProductCountWithOutput returns its count via the OUTPUT parameter only (no SELECT
        // result set), so ExecuteScalar() sees no rows here - this exercises the SpParameters
        // overload of ExecuteStoredProcedureScalarAsync<T>, which used to throw for a non-nullable
        // T in this scenario while the object-parameters overload silently returned default(T)
        // for the identical case (AUD-R11 consistency fix).
        var result = await connection.ExecuteStoredProcedureScalarAsync<int>(SpName("GetProductCountWithOutput", dialect), parameters);

        Assert.Equal(0, result);
    }

    #endregion
}