using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Unit.StoredProcedures;

/// <summary>
/// AUD-R13: <c>ExecuteStoredProcedureNonQuery(IDbConnection, string, object?, CommandOptions)</c>
/// was the only overload in <c>StoredProcedureNonQuery</c> that skipped the explicit
/// <see cref="ArgumentNullException"/>/<see cref="ArgumentException"/> connection/procedureName
/// guard every sibling method (and its own async counterpart) performs before delegating. It
/// still eventually threw via the inner <c>ExecuteNonQueryCore</c> call, but reported the wrong
/// parameter name ("sql" instead of "procedureName") for an empty/whitespace procedure name.
/// </summary>
/// <remarks>
/// Uses <see cref="ThrowingDbConnection"/> (not a raw <see cref="SQLiteConnection"/>) because
/// setting <c>CommandType.StoredProcedure</c> against the real System.Data.SQLite provider throws
/// <see cref="NotSupportedException"/> on net472; the wrapper's CommandType setter is a
/// documented no-op for exactly this reason.
/// </remarks>
public class StoredProcedureNonQueryValidationTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;
    private readonly ThrowingDbConnection _connection;

    public StoredProcedureNonQueryValidationTests()
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
    public void ExecuteStoredProcedureNonQuery_WithOptions_NullConnection_ThrowsArgumentNullException()
    {
        System.Data.IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.ExecuteStoredProcedureNonQuery("DeleteOldProducts", new { Id = 1 }, default(CommandOptions)));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void ExecuteStoredProcedureNonQuery_WithOptions_WhitespaceProcedureName_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _connection.ExecuteStoredProcedureNonQuery("   ", null, default(CommandOptions)));

        Assert.Equal("procedureName", ex.ParamName);
    }
}
