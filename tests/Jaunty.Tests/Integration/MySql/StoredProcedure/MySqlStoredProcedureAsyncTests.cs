using System.Data.Common;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Helpers.Dialects;
using MySql.Data.MySqlClient;

namespace Jaunty.Tests.Integration.MySql.StoredProcedure;

public class MySqlStoredProcedureAsyncTests : Integration.StoredProcedure.StoredProcedureAsyncTestBase
{
    protected override DbConnection CreateConnection() =>
        new MySqlConnection(TestConfiguration.MariaDbConnectionString);

    protected override object CategoryParam(int id) => new { p_CategoryId = id };
    protected override object ProductParam(int id) => new { p_ProductId = id };
    protected override object UpdatePriceParam(int productId, decimal newPrice) =>
        new { p_ProductId = productId, p_NewPrice = newPrice };

    protected override string OutputCategoryParamName => "p_CategoryId";
    protected override string OutputCountParamName => "p_ProductCount";

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureAsync_WithResults_ReturnsEntities(DialectInfo _) =>
        ExecuteStoredProcedureAsync_WithResults_ReturnsEntities_Core();

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults(DialectInfo _) =>
        ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults_Core();

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureAsync_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureAsync_WithParametersAndOptions_Works_Core();

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst(DialectInfo _) =>
        ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst_Core();

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works_Core();

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureFirstAsync_NoResults_Throws(DialectInfo _) =>
        ExecuteStoredProcedureFirstAsync_NoResults_Throws_Core();

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst_Core();

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull_Core();

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works_Core();

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureScalarAsync_ReturnsScalarValue(DialectInfo _) =>
        ExecuteStoredProcedureScalarAsync_ReturnsScalarValue_Core();

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue(DialectInfo _) =>
        ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue_Core();

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works_Core();

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully(DialectInfo _) =>
        ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully_Core();

    [Theory]
    [MariaDB]
    public Task ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue(DialectInfo _) =>
        ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue_Core();
}
