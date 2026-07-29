using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.StoredProcedures;

/// <summary>
/// Regression guard for a defect AUD-R26-030 introduced and the local suite could not see.
///
/// <para>
/// That sweep added a null guard to every public overload declaring <c>object parameters</c>
/// (required, non-nullable). The guard is right. What it did not account for is that
/// <em>Jaunty itself</em> called those overloads with null: the stored-procedure family declares
/// <c>object? parameters</c>, its no-parameter overloads forward <see langword="null"/> to the
/// arity-4 overload, and that forwarded straight into <c>Query*(sql, parameters!, options)</c> —
/// with the null-forgiving operator suppressing the warning that was describing the bug. Every
/// no-parameter stored-procedure call then threw
/// <c>ArgumentNullException (Parameter 'parameters')</c> before reaching the database.
/// </para>
///
/// <para>
/// It went unnoticed because no local dialect exercises it. SQLite has no stored procedures, so
/// the only coverage is the PostgreSQL, MySQL, MariaDB and SQL Server integration theories — all
/// of which skip themselves when no connection string is configured. On this machine that was
/// 2,447 skipped tests, and 16 of them were failing. It surfaced the first time the Docker stack
/// was actually brought up.
/// </para>
///
/// <para>
/// These tests need no server. The exception was raised at Jaunty's own argument check, before
/// any command reached the provider, so an in-memory SQLite connection reproduces it exactly —
/// which is the whole point of pinning it here rather than only in the integration suite.
/// </para>
/// </summary>
public class NoParameterOverloadTests
{
    private sealed class Row
    {
        public int Id { get; set; }
    }

    private static SqliteConnection Connection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// SQLite cannot run a stored procedure, so every call here fails — that is expected and not
    /// what is being asserted. What must not happen is Jaunty rejecting its own forwarded null
    /// before the provider is ever consulted.
    /// </summary>
    private static void AssertReachesTheProvider(Action call)
    {
        try
        {
            call();
        }
        catch (ArgumentNullException ex) when (ex.ParamName == "parameters")
        {
            Assert.Fail(
                "The no-parameter overload forwarded null into an overload that rejects it; " +
                "the call never reached the provider.");
        }
        catch (Exception)
        {
            // Anything else means Jaunty passed the argument along and the provider objected,
            // which is all this test needs.
        }
    }

    private static async Task AssertReachesTheProviderAsync(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (ArgumentNullException ex) when (ex.ParamName == "parameters")
        {
            Assert.Fail(
                "The no-parameter overload forwarded null into an overload that rejects it; " +
                "the call never reached the provider.");
        }
        catch (Exception)
        {
        }
    }

    // ------------------------------------------------------------------
    // The four synchronous entry points
    // ------------------------------------------------------------------

    [Fact]
    public void ExecuteStoredProcedure_NoParameters_DoesNotRejectItsOwnNull()
    {
        using SqliteConnection connection = Connection();
        AssertReachesTheProvider(() => connection.ExecuteStoredProcedure<Row>("GetAllProducts"));
    }

    [Fact]
    public void ExecuteStoredProcedureFirst_NoParameters_DoesNotRejectItsOwnNull()
    {
        using SqliteConnection connection = Connection();
        AssertReachesTheProvider(() => connection.ExecuteStoredProcedureFirst<Row>("GetAllProducts"));
    }

    [Fact]
    public void ExecuteStoredProcedureFirstOrDefault_NoParameters_DoesNotRejectItsOwnNull()
    {
        using SqliteConnection connection = Connection();
        AssertReachesTheProvider(() => connection.ExecuteStoredProcedureFirstOrDefault<Row>("GetAllProducts"));
    }

    [Fact]
    public void ExecuteStoredProcedureScalar_NoParameters_DoesNotRejectItsOwnNull()
    {
        using SqliteConnection connection = Connection();
        AssertReachesTheProvider(() => connection.ExecuteStoredProcedureScalar<int>("GetProductCount"));
    }

    // ------------------------------------------------------------------
    // The four asynchronous entry points
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStoredProcedureAsync_NoParameters_DoesNotRejectItsOwnNull()
    {
        using SqliteConnection connection = Connection();
        await AssertReachesTheProviderAsync(async () =>
            await connection.ExecuteStoredProcedureAsync<Row>("GetAllProducts", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteStoredProcedureFirstAsync_NoParameters_DoesNotRejectItsOwnNull()
    {
        using SqliteConnection connection = Connection();
        await AssertReachesTheProviderAsync(async () =>
            await connection.ExecuteStoredProcedureFirstAsync<Row>("GetAllProducts", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteStoredProcedureFirstOrDefaultAsync_NoParameters_DoesNotRejectItsOwnNull()
    {
        using SqliteConnection connection = Connection();
        await AssertReachesTheProviderAsync(async () =>
            await connection.ExecuteStoredProcedureFirstOrDefaultAsync<Row>("GetAllProducts", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_NoParameters_DoesNotRejectItsOwnNull()
    {
        using SqliteConnection connection = Connection();
        await AssertReachesTheProviderAsync(async () =>
            await connection.ExecuteStoredProcedureScalarAsync<int>("GetProductCount", TestContext.Current.CancellationToken));
    }
}
