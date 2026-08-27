using System.Data.SQLite;

using Jaunty.StoredProcedure;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Unit.StoredProcedures;

/// <summary>
/// AUD-R6: a null literal argument for the <c>SpParameters parameters</c> overloads binds to
/// them instead of the <c>object? parameters</c> overloads (SpParameters is a more specific
/// reference type, so the compiler always prefers it for a null literal per "better conversion
/// target" rules). Before the fix, that meant <c>connection.ExecuteStoredProcedureNonQuery("proc",
/// null)</c> unexpectedly threw <see cref="ArgumentNullException"/> instead of executing with no
/// parameters as the caller intended. These overloads now treat a null <see cref="SpParameters"/>
/// argument the same as an empty one.
/// </summary>
/// <remarks>
/// Uses <see cref="ThrowingDbConnection"/> (not a raw <see cref="SQLiteConnection"/>) because
/// setting <c>CommandType.StoredProcedure</c> against the real System.Data.SQLite provider throws
/// <see cref="NotSupportedException"/> on net472; the wrapper's CommandType setter is a
/// documented no-op for exactly this reason.
/// </remarks>
public class StoredProcedureNullParametersTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;
    private readonly ThrowingDbConnection _connection;

    public StoredProcedureNullParametersTests()
    {
        _realConnection = new SQLiteConnection("Data Source=:memory:");
        _connection = new ThrowingDbConnection(_realConnection);
        _connection.Open();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    [Fact]
    public void ExecuteStoredProcedureScalar_NullParameters_DoesNotThrow()
    {
        int result = _connection.ExecuteStoredProcedureScalar<int>("SELECT 1", null);

        Assert.Equal(1, result);
    }

    [Fact]
    public void ExecuteStoredProcedureNonQuery_NullParameters_DoesNotThrow()
    {
        int result = _connection.ExecuteStoredProcedureNonQuery("CREATE TABLE t (id INTEGER)", null);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_NullParameters_DoesNotThrow()
    {
        int result = await _connection.ExecuteStoredProcedureScalarAsync<int>("SELECT 1", null);

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteStoredProcedureNonQueryAsync_NullParameters_DoesNotThrow()
    {
        int result = await _connection.ExecuteStoredProcedureNonQueryAsync("CREATE TABLE t2 (id INTEGER)", null);

        Assert.Equal(0, result);
    }
}
