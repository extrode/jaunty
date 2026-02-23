using System.Data;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Helpers.Dialects;
using Microsoft.Data.SqlClient;

namespace Jaunty.Tests.Integration.SqlServer.StoredProcedure;

public class SqlServerStoredProcedureTests : Integration.StoredProcedure.StoredProcedureTestBase
{
    protected override IDbConnection CreateConnection() =>
        new SqlConnection(TestConfiguration.SqlServerConnectionString);

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedure_WithResults_ReturnsEntities(DialectInfo _) =>
        ExecuteStoredProcedure_WithResults_ReturnsEntities_Core();

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults(DialectInfo _) =>
        ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults_Core();

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedure_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedure_WithParametersAndOptions_Works_Core();

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedureFirst_WithResults_ReturnsFirst(DialectInfo _) =>
        ExecuteStoredProcedureFirst_WithResults_ReturnsFirst_Core();

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedureFirst_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureFirst_WithParametersAndOptions_Works_Core();

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedureFirst_NoResults_Throws(DialectInfo _) =>
        ExecuteStoredProcedureFirst_NoResults_Throws_Core();

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst_Core();

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull_Core();

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works_Core();

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedureScalar_ReturnsScalarValue(DialectInfo _) =>
        ExecuteStoredProcedureScalar_ReturnsScalarValue_Core();

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedureScalar_WithParameters_ReturnsValue(DialectInfo _) =>
        ExecuteStoredProcedureScalar_WithParameters_ReturnsValue_Core();

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedureScalar_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureScalar_WithParametersAndOptions_Works_Core();

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedureNonQuery_ExecutesSuccessfully(DialectInfo _) =>
        ExecuteStoredProcedureNonQuery_ExecutesSuccessfully_Core();

    [Theory]
    [SqlServer]
    public void ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue(DialectInfo _) =>
        ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue_Core();

    [Theory]
    [SqlServer]
    public void SpParameters_HasValue_ReturnsTrueForOutputWithValue(DialectInfo _) =>
        SpParameters_HasValue_ReturnsTrueForOutputWithValue_Core();
}
