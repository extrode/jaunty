using System.Data.SQLite;

using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Unit.StoredProcedures;

/// <summary>
/// AUD-R12: the three <c>ExecuteStoredProcedureScalar&lt;T&gt;</c> (sync) and three
/// <c>ExecuteStoredProcedureScalarAsync&lt;T&gt;</c> overloads were the only stored-procedure
/// methods in their respective files that skipped the explicit
/// <see cref="ArgumentNullException"/>/<see cref="ArgumentException"/> connection/procedureName
/// guard every sibling method performs before delegating. They still eventually threw via the
/// inner QueryScalar/QueryScalarAsync call, but reported the wrong parameter name ("sql" instead
/// of "procedureName") for an empty/whitespace procedure name.
/// </summary>
/// <remarks>
/// Uses <see cref="ThrowingDbConnection"/> (not a raw <see cref="SQLiteConnection"/>) because
/// setting <c>CommandType.StoredProcedure</c> against the real System.Data.SQLite provider throws
/// <see cref="NotSupportedException"/> on net472; the wrapper's CommandType setter is a
/// documented no-op for exactly this reason.
/// </remarks>
public class StoredProcedureScalarValidationTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;
    private readonly ThrowingDbConnection _connection;

    public StoredProcedureScalarValidationTests()
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
    public void ExecuteStoredProcedureScalar_NullConnection_ThrowsArgumentNullException()
    {
        System.Data.IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.ExecuteStoredProcedureScalar<int>("GetCount"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void ExecuteStoredProcedureScalar_WithParameters_NullConnection_ThrowsArgumentNullException()
    {
        System.Data.IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.ExecuteStoredProcedureScalar<int>("GetCount", new { Id = 1 }));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void ExecuteStoredProcedureScalar_WithOptions_NullConnection_ThrowsArgumentNullException()
    {
        System.Data.IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.ExecuteStoredProcedureScalar<int>("GetCount", new { Id = 1 }, default));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void ExecuteStoredProcedureScalar_WhitespaceProcedureName_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _connection.ExecuteStoredProcedureScalar<int>("   "));

        Assert.Equal("procedureName", ex.ParamName);
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_NullConnection_ThrowsArgumentNullException()
    {
        System.Data.IDbConnection? connection = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.ExecuteStoredProcedureScalarAsync<int>("GetCount"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_WithParameters_NullConnection_ThrowsArgumentNullException()
    {
        System.Data.IDbConnection? connection = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.ExecuteStoredProcedureScalarAsync<int>("GetCount", new { Id = 1 }));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_WithOptions_NullConnection_ThrowsArgumentNullException()
    {
        System.Data.IDbConnection? connection = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.ExecuteStoredProcedureScalarAsync<int>("GetCount", new { Id = 1 }, default));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_WhitespaceProcedureName_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _connection.ExecuteStoredProcedureScalarAsync<int>("   "));

        Assert.Equal("procedureName", ex.ParamName);
    }
}
