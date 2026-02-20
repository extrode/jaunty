using System;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
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
        $"SELECT TOP {n} product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products";

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
    public void Insert_And_Delete_WithTransaction() => Insert_And_Delete_WithTransaction_Core();

    [SkipIfNoSqlServerFact]
    public void Update_WithTransaction() => Update_WithTransaction_Core();

    [SkipIfNoSqlServerFact]
    public Task InsertAsync_And_DeleteAsync_WithTransaction() => InsertAsync_And_DeleteAsync_WithTransaction_Core();
}
