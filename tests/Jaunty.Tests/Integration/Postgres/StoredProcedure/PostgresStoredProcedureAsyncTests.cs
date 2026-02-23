using System.Data.Common;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Helpers.Dialects;
using Npgsql;

namespace Jaunty.Tests.Integration.Postgres.StoredProcedure;

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

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureAsync_WithResults_ReturnsEntities(DialectInfo _) =>
        ExecuteStoredProcedureAsync_WithResults_ReturnsEntities_Core();

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults(DialectInfo _) =>
        ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults_Core();

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureAsync_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureAsync_WithParametersAndOptions_Works_Core();

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst(DialectInfo _) =>
        ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst_Core();

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works_Core();

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureFirstAsync_NoResults_Throws(DialectInfo _) =>
        ExecuteStoredProcedureFirstAsync_NoResults_Throws_Core();

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefaultAsync_WithResults_ReturnsFirst_Core();

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull_Core();

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works_Core();

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureScalarAsync_ReturnsScalarValue(DialectInfo _) =>
        ExecuteStoredProcedureScalarAsync_ReturnsScalarValue_Core();

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue(DialectInfo _) =>
        ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue_Core();

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works(DialectInfo _) =>
        ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works_Core();

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully(DialectInfo _) =>
        ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully_Core();

    [Theory]
    [Postgres]
    public Task ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue(DialectInfo _) =>
        ExecuteStoredProcedureNonQueryAsync_WithOutputParameter_ReturnsOutputValue_Core();
}
