using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Helpers.Dialects;

using Microsoft.Data.SqlClient;

using MySql.Data.MySqlClient;

using Npgsql;

namespace Jaunty.Tests.Integration.StoredProcedure;

public sealed class StoredProcedureDialectTests
{
    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedure_WithResults_ReturnsEntities(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
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
        using var connection = CreateConnection(dialect);
        var products = connection.ExecuteStoredProcedure<Product>("GetProductsByCategory", CategoryParam(dialect, 1));

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedure_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureFirst_WithResults_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var product = connection.ExecuteStoredProcedureFirst<Product>("GetProductById", ProductParam(dialect, 1));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureFirst_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
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
        using var connection = CreateConnection(dialect);
        Assert.Throws<InvalidOperationException>(() => connection.ExecuteStoredProcedureFirst<Product>("GetNoResults"));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var product = connection.ExecuteStoredProcedureFirstOrDefault<Product>("GetProductById", ProductParam(dialect, 1));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var product = connection.ExecuteStoredProcedureFirstOrDefault<Product>("GetNoResults");

        Assert.Null(product);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureScalar_ReturnsScalarValue(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var count = connection.ExecuteStoredProcedureScalar<int>("GetProductCount");

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureScalar_WithParameters_ReturnsValue(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var count = connection.ExecuteStoredProcedureScalar<int>("GetProductCountByCategory", CategoryParam(dialect, 1));

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureScalar_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureNonQuery_ExecutesSuccessfully(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue(DialectInfo dialect)
    {
        using var connection = CreateConnection(dialect);
        var parameters = new SpParameters().AddInput(OutputCategoryParamName(dialect), 1);

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
        using var connection = CreateConnection(dialect);
        var parameters = new SpParameters().AddInput(OutputCategoryParamName(dialect), 1);

        if (UsesInOutForOutput(dialect))
            parameters.AddInputOutput(OutputCountParamName(dialect), 0, DbType.Int32);
        else
            parameters.AddOutput(OutputCountParamName(dialect), DbType.Int32);

        connection.ExecuteStoredProcedureNonQuery("GetProductCountWithOutput", parameters);
        Assert.True(parameters.HasValue(OutputCountParamName(dialect)));
    }

    private static IDbConnection CreateConnection(DialectInfo dialect)
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
