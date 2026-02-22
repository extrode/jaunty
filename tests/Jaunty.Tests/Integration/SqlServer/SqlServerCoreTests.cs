using System;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Integration.Core;

namespace Jaunty.Tests.Integration.SqlServer;

/// <summary>
/// Core integration tests against SQL Server.
/// SQL Server uses TOP N syntax and returns int from COUNT(*).
/// </summary>
public class SqlServerCoreTests : CoreIntegrationTestBase
{
    protected override DbConnection CreateConnection() =>
        new SqlConnection(TestConfiguration.SqlServerConnectionString);

    protected override string SelectTopProducts(int n) =>
        $"SELECT TOP {n} ProductID AS ProductId, ProductName AS ProductName, CategoryID AS CategoryId FROM products";

    protected override string SelectCountProductsByCategory =>
        "SELECT COUNT(*) FROM products WHERE CategoryID = @CategoryId";

    protected override string SelectCategoryByIdSql =>
        "SELECT CategoryID AS CategoryId, CategoryName AS CategoryName, Description FROM Categories WHERE CategoryID = 1";

    protected override string SelectCategoryNameOnlySql =>
        "SELECT CategoryID AS CategoryId, CategoryName AS CategoryName FROM Categories WHERE CategoryID = 1";

    protected override string SelectEmptyProductSql =>
        "SELECT ProductID AS ProductId, ProductName AS ProductName FROM products WHERE 1 = 0";

    protected override string SelectProductByIdSql =>
        "SELECT ProductID AS ProductId, ProductName AS ProductName FROM products WHERE ProductID = @ProductId";

    protected override Type CountReturnType => typeof(int);

    [SkipIfNoSqlServerFact]
    public void QueryPartial_ReturnsResults() => QueryPartial_ReturnsResults_Core();

    [SkipIfNoSqlServerFact]
    public void QueryPartialFirst_ReturnsFirst() => QueryPartialFirst_ReturnsFirst_Core();

    [SkipIfNoSqlServerFact]
    public void QueryPartialFirstOrDefault_NoResults_ReturnsNull() => QueryPartialFirstOrDefault_NoResults_ReturnsNull_Core();

    [SkipIfNoSqlServerFact]
    public void QueryPartialSingle_WithUniqueResult_ReturnsSingle() => QueryPartialSingle_WithUniqueResult_ReturnsSingle_Core();

    [SkipIfNoSqlServerFact]
    public Task QueryPartialAsync_ReturnsResults() => QueryPartialAsync_ReturnsResults_Core();

    [SkipIfNoSqlServerFact]
    public Task QueryPartialFirstAsync_ReturnsFirst() => QueryPartialFirstAsync_ReturnsFirst_Core();

    [SkipIfNoSqlServerFact]
    public void QueryScalar_ReturnsCount() => QueryScalar_ReturnsCount_Core();

    [SkipIfNoSqlServerFact]
    public void QueryScalar_WithParameters_ReturnsCount() => QueryScalar_WithParameters_ReturnsCount_Core();

    [SkipIfNoSqlServerFact]
    public void ExecuteScalar_ReturnsCount() => ExecuteScalar_ReturnsCount_Core();

    [SkipIfNoSqlServerFact]
    public Task QueryScalarAsync_ReturnsCount() => QueryScalarAsync_ReturnsCount_Core();

    [SkipIfNoSqlServerFact]
    public Task ExecuteScalarAsync_ReturnsCount() => ExecuteScalarAsync_ReturnsCount_Core();

    [SkipIfNoSqlServerFact]
    public void Insert_And_Delete_WithTransaction() => Insert_And_Delete_WithTransaction_SqlServer();

    [SkipIfNoSqlServerFact]
    public void Update_WithTransaction() => Update_WithTransaction_SqlServer();

    [SkipIfNoSqlServerFact]
    public Task InsertAsync_And_DeleteAsync_WithTransaction() => InsertAsync_And_DeleteAsync_WithTransaction_SqlServer();

    #region SQL Server-specific transaction tests

    private void Insert_And_Delete_WithTransaction_SqlServer()
    {
        Connection.Open();
        using var transaction = Connection.BeginTransaction();
        try
        {
            var category = new CategorySql
            {
                CategoryName = "TestCategory",
                Description = "Integration test"
            };

            var result = Connection.Insert(category,
                CommandOptions<CategorySql>.WithTransaction(transaction));

            Assert.True(result > 0);
            Assert.True(category.CategoryId > 0); // Verify ID was generated

            var deleteResult = Connection.Delete(category,
                CommandOptions<CategorySql>.WithTransaction(transaction));

            Assert.True(deleteResult > 0);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    private void Update_WithTransaction_SqlServer()
    {
        Connection.Open();
        using var transaction = Connection.BeginTransaction();
        try
        {
            var category = Connection.QueryPartialFirst<CategorySql>(
                "SELECT CategoryID AS CategoryId, CategoryName AS CategoryName, Description FROM Categories WHERE CategoryID = 1",
                options: CommandOptions<CategorySql>.WithTransaction(transaction));

            Assert.NotNull(category);

            var originalName = category.CategoryName;
            category.CategoryName = "UpdatedCategory";

            var result = Connection.Update(category,
                CommandOptions<CategorySql>.WithTransaction(transaction));

            Assert.True(result > 0);

            var updated = Connection.QueryPartialFirst<CategorySql>(
                "SELECT CategoryID AS CategoryId, CategoryName AS CategoryName FROM Categories WHERE CategoryID = 1",
                options: CommandOptions<CategorySql>.WithTransaction(transaction));

            Assert.Equal("UpdatedCategory", updated.CategoryName);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    private async Task InsertAsync_And_DeleteAsync_WithTransaction_SqlServer()
    {
        await Connection.OpenAsync();
        using var transaction = Connection.BeginTransaction();
        try
        {
            var category = new CategorySql
            {
                CategoryName = "TestCategoryAsync",
                Description = "Async integration test"
            };

            var result = await Connection.InsertAsync(category,
                CommandOptions<CategorySql>.WithTransaction(transaction));

            Assert.True(result > 0);
            Assert.True(category.CategoryId > 0); // Verify ID was generated

            var deleteResult = await Connection.DeleteAsync(category,
                CommandOptions<CategorySql>.WithTransaction(transaction));

            Assert.True(deleteResult > 0);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    #endregion
}
