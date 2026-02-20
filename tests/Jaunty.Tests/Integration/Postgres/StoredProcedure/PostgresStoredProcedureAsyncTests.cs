using System.Data.Common;
using System.Threading.Tasks;
using Npgsql;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Postgres.StoredProcedure;

/// <summary>
/// Async stored procedure tests against PostgreSQL.
/// Delegates all test logic to <see cref="StoredProcedureAsyncTestBase"/>.
/// PostgreSQL uses p_ parameter prefix and INOUT for output parameters.
/// </summary>
public class PostgresStoredProcedureAsyncTests : Integration.StoredProcedure.StoredProcedureAsyncTestBase
{
    protected override DbConnection CreateConnection() =>
        new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString);

    protected override object CategoryParam(int id) => new { p_category_id = id };
    protected override object ProductParam(int id) => new { p_product_id = id };
    protected override object UpdatePriceParam(int productId, decimal newPrice) =>
        new { p_product_id = productId, p_new_price = newPrice };

    protected override bool UsesInOutForOutput => true;
    protected override string OutputCategoryParamName => "p_category_id";
    protected override string OutputCountParamName => "p_product_count";

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureAsync_WithResults_ReturnsEntities() =>
        ExecuteStoredProcedureAsync_WithResults_ReturnsEntities_Core();

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults() =>
        ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults_Core();

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureAsync_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureAsync_WithParametersAndOptions_Works_Core();

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst() =>
        ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst_Core();

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works_Core();

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureFirstAsync_NoResults_Throws() =>
        ExecuteStoredProcedureFirstAsync_NoResults_Throws_Core();

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst() =>
        ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst_Core();

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull() =>
        ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull_Core();

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works_Core();

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureScalarAsync_ReturnsScalarValue() =>
        ExecuteStoredProcedureScalarAsync_ReturnsScalarValue_Core();

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue() =>
        ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue_Core();

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works_Core();

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully() =>
        ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully_Core();

    [SkipIfNoPostgresFact]
    public Task ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue() =>
        ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue_Core();
}
