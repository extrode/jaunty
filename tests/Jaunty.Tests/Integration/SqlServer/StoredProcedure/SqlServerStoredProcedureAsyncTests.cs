using System.Data.Common;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Helpers.Dialects;
using Microsoft.Data.SqlClient;

namespace Jaunty.Tests.Integration.SqlServer.StoredProcedure;

public class SqlServerStoredProcedureAsyncTests : Integration.StoredProcedure.StoredProcedureAsyncTestBase
{
    protected override DbConnection CreateConnection() =>
        new SqlConnection(TestConfiguration.SqlServerConnectionString);

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureAsync_WithResults_ReturnsEntities(DialectInfo _) =>
        ExecuteStoredProcedureAsync_WithResults_ReturnsEntities_Core();

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults(DialectInfo _) =>
        ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults_Core();

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureAsync_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureAsync_WithParametersAndOptions_Works_Core();

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst(DialectInfo _) =>
        ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst_Core();

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works_Core();

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureFirstAsync_NoResults_Throws(DialectInfo _) =>
        ExecuteStoredProcedureFirstAsync_NoResults_Throws_Core();

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst_Core();

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull_Core();

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works_Core();

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureScalarAsync_ReturnsScalarValue(DialectInfo _) =>
        ExecuteStoredProcedureScalarAsync_ReturnsScalarValue_Core();

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue(DialectInfo _) =>
        ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue_Core();

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works_Core();

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully(DialectInfo _) =>
        ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully_Core();

    [Theory]
    [SqlServer]
    public Task ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue(DialectInfo _) =>
        ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue_Core();
}
