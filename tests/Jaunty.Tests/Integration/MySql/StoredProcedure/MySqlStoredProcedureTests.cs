using System.Data;
using MySql.Data.MySqlClient;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.MySql.StoredProcedure;

/// <summary>
/// Sync stored procedure tests against MySQL / MariaDB.
/// Delegates all test logic to <see cref="StoredProcedureTestBase"/>.
/// MySQL uses p_ parameter prefix for SP params and OUT for output parameters.
///
/// Configure via:
///   - Environment variable: JAUNTY_TEST_MYSQL
///   - Or appsettings.json: ConnectionStrings:MySql
/// </summary>
public class MySqlStoredProcedureTests : Integration.StoredProcedure.StoredProcedureTestBase
{
    protected override IDbConnection CreateConnection() =>
        new MySqlConnection(TestConfiguration.MySqlConnectionString);

    protected override object CategoryParam(int id) => new { p_CategoryId = id };
    protected override object ProductParam(int id) => new { p_ProductId = id };
    protected override object UpdatePriceParam(int productId, decimal newPrice) =>
        new { p_ProductId = productId, p_NewPrice = newPrice };

    protected override string OutputCategoryParamName => "p_CategoryId";
    protected override string OutputCountParamName => "p_ProductCount";

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedure_WithResults_ReturnsEntities() =>
        ExecuteStoredProcedure_WithResults_ReturnsEntities_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults() =>
        ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedure_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedure_WithParametersAndOptions_Works_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedureFirst_WithResults_ReturnsFirst() =>
        ExecuteStoredProcedureFirst_WithResults_ReturnsFirst_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedureFirst_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureFirst_WithParametersAndOptions_Works_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedureFirst_NoResults_Throws() =>
        ExecuteStoredProcedureFirst_NoResults_Throws_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst() =>
        ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull() =>
        ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedureScalar_ReturnsScalarValue() =>
        ExecuteStoredProcedureScalar_ReturnsScalarValue_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedureScalar_WithParameters_ReturnsValue() =>
        ExecuteStoredProcedureScalar_WithParameters_ReturnsValue_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedureScalar_WithParametersAndOptions_Works() =>
        ExecuteStoredProcedureScalar_WithParametersAndOptions_Works_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedureNonQuery_ExecutesSuccessfully() =>
        ExecuteStoredProcedureNonQuery_ExecutesSuccessfully_Core();

    [SkipIfNoMySqlFact]
    public void ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue() =>
        ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue_Core();

    [SkipIfNoMySqlFact]
    public void SpParameters_HasValue_ReturnsTrueForOutputWithValue() =>
        SpParameters_HasValue_ReturnsTrueForOutputWithValue_Core();
}
