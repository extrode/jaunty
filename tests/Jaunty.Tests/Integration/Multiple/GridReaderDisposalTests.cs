using System.Data;
using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Multiple;

/// <summary>
/// Regression tests proving <c>ExecuteQueryMultiple</c> hands its <see cref="IDbCommand"/> off to
/// <see cref="Jaunty.Core.GridReader"/> for disposal instead of leaking it, and that a failure while
/// building/executing the command disposes the command and closes a self-opened connection.
/// </summary>
public class GridReaderDisposalTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    public GridReaderDisposalTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
        SeedSchema();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    private void SeedSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE categories (
                category_id INTEGER PRIMARY KEY AUTOINCREMENT,
                category_name TEXT NOT NULL,
                description TEXT
            );
            INSERT INTO categories (category_name, description) VALUES ('Beverages', 'Soft drinks');";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void QueryMultiple_DisposesUnderlyingCommand_WhenGridReaderIsDisposed()
    {
        using var wrapper = new IDbConnectionWrapper(_connection);

        var gridReader = wrapper.QueryMultiple(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        Assert.NotNull(wrapper.LastCommand);
        Assert.False(wrapper.LastCommand!.Disposed);

        gridReader.Dispose();

        Assert.True(wrapper.LastCommand!.Disposed);
    }

    [Fact]
    public void QueryMultiple_ThrowingDuringExecute_DisposesCommandAndClosesSelfOpenedConnection()
    {
        using var realConnection = new SQLiteConnection("Data Source=:memory:");
        using var wrapper = new IDbConnectionWrapper(realConnection);

        Assert.Throws<SQLiteException>(() => wrapper.QueryMultiple("SELECT * FROM no_such_table"));

        Assert.NotNull(wrapper.LastCommand);
        Assert.True(wrapper.LastCommand!.Disposed);
        Assert.Equal(ConnectionState.Closed, realConnection.State);
    }

    [Fact]
    public void Read_ThrowingDuringRowMapping_StillDisposesUnderlyingCommand()
    {
        // Regression test (round 10): GridReader.ReadCore (and its ReadStream/async siblings)
        // called Advance() unconditionally after the read loop instead of in a finally block.
        // A row-mapping failure mid-loop would propagate past Advance() entirely, leaving the
        // underlying command/reader undisposed - a resource leak on top of the mapping error.
        using var wrapper = new IDbConnectionWrapper(_connection);

        GridReader gridReader = wrapper.QueryMultiple(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        var options = CommandOptions<Category>.WithMapper(_ => throw new InvalidOperationException("boom"));

        Assert.Throws<InvalidOperationException>(() => gridReader.Read(options));

        Assert.True(wrapper.LastCommand!.Disposed);
    }
}
