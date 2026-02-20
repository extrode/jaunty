using System.Data.Common;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.MySql.StoredProcedure;

/// <summary>
/// Async stored procedure tests against MySQL / MariaDB.
/// Delegates all test logic to <see cref="StoredProcedureAsyncTestBase"/>.
/// MySQL uses p_ parameter prefix for SP params and OUT for output parameters.
/// </summary>
public class MySqlStoredProcedureAsyncTests : Integration.StoredProcedure.StoredProcedureAsyncTestBase
{
    protected override DbConnection CreateConnection() =>
        new MySqlConnection(TestConfiguration.MySqlConnectionString);

    protected override object CategoryParam(int id) => new { p_CategoryId = id };
    protected override object ProductParam(int id) => new { p_ProductId = id };
    protected override object UpdatePriceParam(int productId, decimal newPrice) =>
        new { p_ProductId = productId, p_NewPrice = newPrice };

    protected override string OutputCategoryParamName => "p_CategoryId";
    protected override string OutputCountParamName => "p_ProductCount";

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureAsync_WithResults_ReturnsEntities() =>
        ExecuteStoredProcedureAsync_WithResults_ReturnsEntities_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults() =>
        ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureAsync_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureAsync_WithParametersAndOptions_Works_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst() =>
        ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureFirstAsync_NoResults_Throws() =>
        ExecuteStoredProcedureFirstAsync_NoResults_Throws_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst() =>
        ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull() =>
        ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureScalarAsync_ReturnsScalarValue() =>
        ExecuteStoredProcedureScalarAsync_ReturnsScalarValue_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue() =>
        ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully() =>
        ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully_Core();

    [SkipIfNoMySqlFact]
    public Task ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue() =>
        ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue_Core();
}
