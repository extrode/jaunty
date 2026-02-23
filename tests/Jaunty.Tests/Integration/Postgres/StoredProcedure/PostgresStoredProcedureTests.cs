using System.Data;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Helpers.Dialects;
using Npgsql;

namespace Jaunty.Tests.Integration.Postgres.StoredProcedure;

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

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedure_WithResults_ReturnsEntities(DialectInfo _) =>
        ExecuteStoredProcedure_WithResults_ReturnsEntities_Core();

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults(DialectInfo _) =>
        ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults_Core();

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedure_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedure_WithParametersAndOptions_Works_Core();

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedureFirst_WithResults_ReturnsFirst(DialectInfo _) =>
        ExecuteStoredProcedureFirst_WithResults_ReturnsFirst_Core();

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedureFirst_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureFirst_WithParametersAndOptions_Works_Core();

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedureFirst_NoResults_Throws(DialectInfo _) =>
        ExecuteStoredProcedureFirst_NoResults_Throws_Core();

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst_Core();

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull_Core();

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefault_WithParametersAndOptions_Works_Core();

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedureScalar_ReturnsScalarValue(DialectInfo _) =>
        ExecuteStoredProcedureScalar_ReturnsScalarValue_Core();

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedureScalar_WithParameters_ReturnsValue(DialectInfo _) =>
        ExecuteStoredProcedureScalar_WithParameters_ReturnsValue_Core();

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedureScalar_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureScalar_WithParametersAndOptions_Works_Core();

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedureNonQuery_ExecutesSuccessfully(DialectInfo _) =>
        ExecuteStoredProcedureNonQuery_ExecutesSuccessfully_Core();

    [Theory]
    [Postgres]
    public void ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue(DialectInfo _) =>
        ExecuteStoredProcedureNonQuery_WithOutputParameter_ReturnsOutputValue_Core();
}
