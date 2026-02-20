using System.Data;
using Npgsql;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Postgres.StoredProcedure;

/// <summary>
/// Sync stored procedure tests against PostgreSQL.
/// Delegates all test logic to <see cref="StoredProcedureTestBase"/>.
/// PostgreSQL uses p_ parameter prefix and INOUT for output parameters.
///
/// Configure via:
///   - Environment variable: JAUNTY_TEST_POSTGRESQL
///   - Or appsettings.json: ConnectionStrings:PostgreSql
/// </summary>
public class PostgresStoredProcedureTests : Integration.StoredProcedure.StoredProcedureTestBase
{
    protected override IDbConnection CreateConnection() =>
        new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString);

    protected override object CategoryParam(int id) => new { p_category_id = id };
    protected override object ProductParam(int id) => new { p_product_id = id };
    protected override object UpdatePriceParam(int productId, decimal newPrice) =>
        new { p_product_id = productId, p_new_price = newPrice };

    protected override bool UsesInOutForOutput => true;
    protected override string OutputCategoryParamName => "p_category_id";
    protected override string OutputCountParamName => "p_product_count";

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedure_WithResults_ReturnsEntities() =>
        ExecuteStoredProcedure_WithResults_ReturnsEntities_Core();

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults() =>
        ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults_Core();

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedure_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedure_WithParametersAndOptions_Works_Core();

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureFirst_WithResults_ReturnsFirst() =>
        ExecuteStoredProcedureFirst_WithResults_ReturnsFirst_Core();

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureFirst_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureFirst_WithParametersAndOptions_Works_Core();

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureFirst_NoResults_Throws() =>
        ExecuteStoredProcedureFirst_NoResults_Throws_Core();

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst() =>
        ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst_Core();

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull() =>
        ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull_Core();

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works_Core();

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureScalar_ReturnsScalarValue() =>
        ExecuteStoredProcedureScalar_ReturnsScalarValue_Core();

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureScalar_WithParameters_ReturnsValue() =>
        ExecuteStoredProcedureScalar_WithParameters_ReturnsValue_Core();

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureScalar_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureScalar_WithParametersAndOptions_Works_Core();

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureNonQuery_ExecutesSuccessfully() =>
        ExecuteStoredProcedureNonQuery_ExecutesSuccessfully_Core();

    [SkipIfNoPostgresFact]
    public void ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue() =>
        ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue_Core();
}
