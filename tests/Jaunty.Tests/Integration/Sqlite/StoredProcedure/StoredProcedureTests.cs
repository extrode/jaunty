using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Sqlite.StoredProcedure;

public class StoredProcedureTests
{
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedure_WithResults_ReturnsEntities(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedure_WithCommandOptions_Works(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedure_WithParametersAndOptions_Works(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureFirst_WithResults_ReturnsFirst(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureFirst_WithParameters_ReturnsFirst(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureFirst_NoResults_Throws(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureSingle_WithExactlyOneResult_ReturnsEntity(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureSingle_NoResults_Throws(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureSingle_MultipleResults_Throws(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureSingleOrDefault_WithExactlyOneResult_ReturnsEntity(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureSingleOrDefault_NoResults_ReturnsNull(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureScalar_ReturnsScalarValue(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureScalar_WithParameters_ReturnsValue(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureNonQuery_ExecutesSuccessfully(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureNonQuery_WithParameters_ExecutesSuccessfully(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureNonQuery_WithParametersAndOptions_ExecutesSuccessfully(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureAsync_WithResults_ReturnsEntities(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureAsync_WithParameters_ReturnsFilteredResults(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureAsync_WithParametersAndOptions_Works(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureFirstAsync_WithParameters_ReturnsFirst(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureFirstAsync_WithParametersAndOptions_Works(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureFirstOrDefaultAsync_WithParameters_ReturnsResult(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureFirstOrDefaultAsync_WithParametersAndOptions_Works(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureSingleAsync_WithExactlyOneResult_ReturnsEntity(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureSingleOrDefaultAsync_NoResults_ReturnsNull(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureScalarAsync_ReturnsScalarValue(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureScalarAsync_WithParameters_ReturnsValue(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureScalarAsync_WithParametersAndOptions_Works(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureNonQueryAsync_ExecutesSuccessfully(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureNonQueryAsync_WithParameters_ExecutesSuccessfully(DialectInfo _) { }
    [Theory] [SqlServer] [Postgres] [MariaDB] public void ExecuteStoredProcedureNonQueryAsync_WithParametersAndOptions_ExecutesSuccessfully(DialectInfo _) { }
}
