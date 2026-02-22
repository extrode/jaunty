using System.Data;
using MySql.Data.MySqlClient;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Helpers.Dialects;

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

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedure_WithResults_ReturnsEntities(DialectInfo _) =>
        ExecuteStoredProcedure_WithResults_ReturnsEntities_Core();

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults(DialectInfo _) =>
        ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults_Core();

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedure_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedure_WithParametersAndOptions_Works_Core();

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedureFirst_WithResults_ReturnsFirst(DialectInfo _) =>
        ExecuteStoredProcedureFirst_WithResults_ReturnsFirst_Core();

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedureFirst_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureFirst_WithParametersAndOptions_Works_Core();

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedureFirst_NoResults_Throws(DialectInfo _) =>
        ExecuteStoredProcedureFirst_NoResults_Throws_Core();

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst_Core();

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull_Core();

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works_Core();

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedureScalar_ReturnsScalarValue(DialectInfo _) =>
        ExecuteStoredProcedureScalar_ReturnsScalarValue_Core();

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedureScalar_WithParameters_ReturnsValue(DialectInfo _) =>
        ExecuteStoredProcedureScalar_WithParameters_ReturnsValue_Core();

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedureScalar_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureScalar_WithParametersAndOptions_Works_Core();

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedureNonQuery_ExecutesSuccessfully(DialectInfo _) =>
        ExecuteStoredProcedureNonQuery_ExecutesSuccessfully_Core();

    [Theory]
    [MariaDB]
    public void ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue(DialectInfo _) =>
        ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue_Core();

    [Theory]
    [MariaDB]
    public void SpParameters_HasValue_ReturnsTrueForOutputWithValue(DialectInfo _) =>
        SpParameters_HasValue_ReturnsTrueForOutputWithValue_Core();
}

