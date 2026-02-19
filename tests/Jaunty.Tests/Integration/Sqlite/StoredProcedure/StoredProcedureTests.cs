namespace Jaunty.Tests.Integration.Sqlite.StoredProcedure;

/// <summary>
/// StoredProcedure tests are skipped because SQLite does not support stored procedures.
/// These tests document the API surface and serve as placeholders for SQL Server/PostgreSQL testing.
/// </summary>
public class StoredProcedureTests
{
    #region ExecuteStoredProcedure (returns List<T>)

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedure_WithResults_ReturnsEntities() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedure_WithParameters_ReturnsFilteredResults() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedure_WithCommandOptions_Works() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedure_WithParametersAndOptions_Works() { }

    #endregion

    #region ExecuteStoredProcedureFirst

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureFirst_WithResults_ReturnsFirst() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureFirst_WithParameters_ReturnsFirst() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureFirst_NoResults_Throws() { }

    #endregion

    #region ExecuteStoredProcedureFirstOrDefault

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureFirstOrDefault_WithResults_ReturnsFirst() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureFirstOrDefault_NoResults_ReturnsNull() { }

    #endregion

    #region ExecuteStoredProcedureSingle

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureSingle_WithExactlyOneResult_ReturnsEntity() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureSingle_NoResults_Throws() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureSingle_MultipleResults_Throws() { }

    #endregion

    #region ExecuteStoredProcedureSingleOrDefault

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureSingleOrDefault_WithExactlyOneResult_ReturnsEntity() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureSingleOrDefault_NoResults_ReturnsNull() { }

    #endregion

    #region ExecuteStoredProcedureScalar

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureScalar_ReturnsScalarValue() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureScalar_WithParameters_ReturnsValue() { }

    #endregion

    #region Async Variants

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureAsync_WithResults_ReturnsEntities() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureFirstAsync_WithResults_ReturnsFirst() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureFirstOrDefaultAsync_NoResults_ReturnsNull() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureSingleAsync_WithExactlyOneResult_ReturnsEntity() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureSingleOrDefaultAsync_NoResults_ReturnsNull() { }

    [Fact(Skip = "Requires SQL Server or PostgreSQL - SQLite does not support stored procedures")]
    public void ExecuteStoredProcedureScalarAsync_ReturnsScalarValue() { }

    #endregion
}
