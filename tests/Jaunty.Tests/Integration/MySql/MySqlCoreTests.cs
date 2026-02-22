using System.Data.Common;

using Jaunty.Tests.Helpers;
using Jaunty.Tests.Integration.Core;

using MySql.Data.MySqlClient;

namespace Jaunty.Tests.Integration.MySql;

/// <summary>
/// Core integration tests against MySQL / MariaDB.
/// MySQL uses LIMIT syntax and returns long from COUNT(*).
/// </summary>
public class MySqlCoreTests : CoreIntegrationTestBase
{
    protected override DbConnection CreateConnection() =>
        new MySqlConnection(TestConfiguration.MySqlConnectionString);

    [SkipIfNoMySqlFact]
    public void QueryPartial_ReturnsResults() => QueryPartial_ReturnsResults_Core();

    [SkipIfNoMySqlFact]
    public void QueryPartialFirst_ReturnsFirst() => QueryPartialFirst_ReturnsFirst_Core();

    [SkipIfNoMySqlFact]
    public void QueryPartialFirstOrDefault_NoResults_ReturnsNull() => QueryPartialFirstOrDefault_NoResults_ReturnsNull_Core();

    [SkipIfNoMySqlFact]
    public void QueryPartialSingle_WithUniqueResult_ReturnsSingle() => QueryPartialSingle_WithUniqueResult_ReturnsSingle_Core();

    [SkipIfNoMySqlFact]
    public Task QueryPartialAsync_ReturnsResults() => QueryPartialAsync_ReturnsResults_Core();

    [SkipIfNoMySqlFact]
    public Task QueryPartialFirstAsync_ReturnsFirst() => QueryPartialFirstAsync_ReturnsFirst_Core();

    [SkipIfNoMySqlFact]
    public void QueryScalar_ReturnsCount() => QueryScalar_ReturnsCount_Core();

    [SkipIfNoMySqlFact]
    public void QueryScalar_WithParameters_ReturnsCount() => QueryScalar_WithParameters_ReturnsCount_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteScalar_ReturnsCount() => ExecuteScalar_ReturnsCount_Core();

    [SkipIfNoMySqlFact]
    public Task QueryScalarAsync_ReturnsCount() => QueryScalarAsync_ReturnsCount_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteScalarAsync_ReturnsCount() => ExecuteScalarAsync_ReturnsCount_Core();

    [SkipIfNoMySqlFact]
    public void Insert_And_Delete_WithTransaction() => Insert_And_Delete_WithTransaction_Core();

    [SkipIfNoMySqlFact]
    public void Update_WithTransaction() => Update_WithTransaction_Core();

    [SkipIfNoMySqlFact]
    public Task InsertAsync_And_DeleteAsync_WithTransaction() => InsertAsync_And_DeleteAsync_WithTransaction_Core();
}
