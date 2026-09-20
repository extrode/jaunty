using System.Data;
using System.Data.SQLite;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Core;
using Extrode.Jaunty.Tests.Helpers;

namespace Extrode.Jaunty.Tests.Integration.Write;

/// <summary>
/// coverage-gaps-2026-09-20: the closed-connection auto-open/close lifecycle and the
/// <c>CommandTimeout</c>/<c>CommandType.StoredProcedure</c> pass-through were untested across
/// <c>Get</c>/<c>GetAll</c>/<c>Delete</c>/<c>Update</c>/<c>Upsert</c>/<c>Query</c>/<c>ExecuteBatch</c>,
/// sync and async. Reuses the shared <see cref="RecordingDbConnection"/>, which records what a
/// command actually had set on it without letting SQLite choke on
/// <see cref="CommandType.StoredProcedure"/>.
/// </summary>
public class CommandOptionsSweepTests
{
    [Table("sweep_widget")]
    public class SweepWidget
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    private static RecordingDbConnection OpenSeeded()
    {
        var inner = new SQLiteConnection("Data Source=:memory:");
        inner.Open();
        using (SQLiteCommand cmd = inner.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE sweep_widget (id INTEGER PRIMARY KEY, name TEXT); " +
                               "INSERT INTO sweep_widget (id, name) VALUES (1, 'a');";
            cmd.ExecuteNonQuery();
        }
        return new RecordingDbConnection(inner);
    }

    /// <summary>
    /// A closed <c>:memory:</c> connection loses its database entirely - SQLite tears the in-memory
    /// database down when the last connection to it closes. The auto-open/close lifecycle can only
    /// be observed against a real file, so this backs the connection with a temp file and deletes it
    /// on dispose.
    /// </summary>
    private sealed class ClosedConnectionFixture : IDisposable
    {
        private readonly string _dbPath;
        public RecordingDbConnection Connection { get; }

        public ClosedConnectionFixture()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"sweep_{Guid.NewGuid():N}.db");

            using (var seed = new SQLiteConnection($"Data Source={_dbPath}"))
            {
                seed.Open();
                using SQLiteCommand cmd = seed.CreateCommand();
                cmd.CommandText = "CREATE TABLE sweep_widget (id INTEGER PRIMARY KEY, name TEXT); " +
                                   "INSERT INTO sweep_widget (id, name) VALUES (1, 'a');";
                cmd.ExecuteNonQuery();
            }

            Connection = new RecordingDbConnection(new SQLiteConnection($"Data Source={_dbPath}"));
        }

        public void Dispose()
        {
            Connection.Dispose();
            File.Delete(_dbPath);
        }
    }

    // ------------------------------------------------------------------
    // CommandTimeout + CommandType.StoredProcedure pass-through
    // ------------------------------------------------------------------

    [Fact]
    public void Get_WithTimeoutAndCommandType_AppliesBoth()
    {
        using RecordingDbConnection connection = OpenSeeded();

        SweepWidget? widget = connection.Get<SweepWidget>(1, new CommandOptions<SweepWidget>(commandTimeout: 77, commandType: CommandType.StoredProcedure));

        Assert.NotNull(widget);
        Assert.Equal(77, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public async Task GetAsync_WithTimeoutAndCommandType_AppliesBoth()
    {
        using RecordingDbConnection connection = OpenSeeded();

        SweepWidget? widget = await connection.GetAsync<SweepWidget>(
            1, new CommandOptions<SweepWidget>(commandTimeout: 77, commandType: CommandType.StoredProcedure), TestContext.Current.CancellationToken);

        Assert.NotNull(widget);
        Assert.Equal(77, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public void GetAll_WithTimeoutAndCommandType_AppliesBoth()
    {
        using RecordingDbConnection connection = OpenSeeded();

        List<SweepWidget> widgets = connection.GetAll(new CommandOptions<SweepWidget>(commandTimeout: 77, commandType: CommandType.StoredProcedure));

        Assert.Single(widgets);
        Assert.Equal(77, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public async Task GetAllAsync_WithTimeoutAndCommandType_AppliesBoth()
    {
        using RecordingDbConnection connection = OpenSeeded();

        List<SweepWidget> widgets = await connection.GetAllAsync(
            new CommandOptions<SweepWidget>(commandTimeout: 77, commandType: CommandType.StoredProcedure), TestContext.Current.CancellationToken);

        Assert.Single(widgets);
        Assert.Equal(77, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public void Delete_WithTimeoutAndCommandType_AppliesBoth()
    {
        using RecordingDbConnection connection = OpenSeeded();

        int rows = connection.Delete<SweepWidget>(new SweepWidget { Id = 1 }, new CommandOptions(commandTimeout: 77, commandType: CommandType.StoredProcedure));

        Assert.Equal(1, rows);
        Assert.Equal(77, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public async Task DeleteAsync_WithTimeoutAndCommandType_AppliesBoth()
    {
        using RecordingDbConnection connection = OpenSeeded();

        int rows = await connection.DeleteAsync<SweepWidget>(
            new SweepWidget { Id = 1 }, new CommandOptions(commandTimeout: 77, commandType: CommandType.StoredProcedure), TestContext.Current.CancellationToken);

        Assert.Equal(1, rows);
        Assert.Equal(77, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public void Update_WithTimeoutAndCommandType_AppliesBoth()
    {
        using RecordingDbConnection connection = OpenSeeded();

        int rows = connection.Update(new SweepWidget { Id = 1, Name = "b" }, new CommandOptions(commandTimeout: 77, commandType: CommandType.StoredProcedure));

        Assert.Equal(1, rows);
        Assert.Equal(77, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public async Task UpdateAsync_WithTimeoutAndCommandType_AppliesBoth()
    {
        using RecordingDbConnection connection = OpenSeeded();

        int rows = await connection.UpdateAsync(
            new SweepWidget { Id = 1, Name = "b" }, new CommandOptions(commandTimeout: 77, commandType: CommandType.StoredProcedure), TestContext.Current.CancellationToken);

        Assert.Equal(1, rows);
        Assert.Equal(77, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public void Upsert_WithTimeoutAndCommandType_AppliesBoth()
    {
        using RecordingDbConnection connection = OpenSeeded();

        int rows = connection.Upsert(new SweepWidget { Id = 1, Name = "b" }, new CommandOptions(commandTimeout: 77, commandType: CommandType.StoredProcedure));

        Assert.Equal(1, rows);
        Assert.Equal(77, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public async Task UpsertAsync_WithTimeoutAndCommandType_AppliesBoth()
    {
        using RecordingDbConnection connection = OpenSeeded();

        int rows = await connection.UpsertAsync(
            new SweepWidget { Id = 1, Name = "b" }, new CommandOptions(commandTimeout: 77, commandType: CommandType.StoredProcedure), TestContext.Current.CancellationToken);

        Assert.Equal(1, rows);
        Assert.Equal(77, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public void ExecuteBatch_WithTimeoutAndCommandType_AppliesBoth()
    {
        using RecordingDbConnection connection = OpenSeeded();

        int rows = connection.ExecuteBatch(
            "UPDATE sweep_widget SET name = @name WHERE id = @id",
            [new { id = 1, name = "c" }],
            new CommandOptions(commandTimeout: 77, commandType: CommandType.StoredProcedure));

        Assert.Equal(1, rows);
        Assert.Equal(77, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public async Task ExecuteBatchAsync_WithTimeoutAndCommandType_AppliesBoth()
    {
        using RecordingDbConnection connection = OpenSeeded();

        int rows = await connection.ExecuteBatchAsync(
            "UPDATE sweep_widget SET name = @name WHERE id = @id",
            [new { id = 1, name = "c" }],
            new CommandOptions(commandTimeout: 77, commandType: CommandType.StoredProcedure),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, rows);
        Assert.Equal(77, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    [Fact]
    public void Query_WithTimeoutAndCommandType_AppliesBoth()
    {
        using RecordingDbConnection connection = OpenSeeded();

        List<SweepWidget> widgets = connection.Query<SweepWidget>(
            "SELECT id AS Id, name AS Name FROM sweep_widget",
            new CommandOptions<SweepWidget>(commandTimeout: 77, commandType: CommandType.StoredProcedure));

        Assert.Single(widgets);
        Assert.Equal(77, connection.ExecutedTimeout);
        Assert.Equal(CommandType.StoredProcedure, connection.ExecutedCommandType);
    }

    // ------------------------------------------------------------------
    // Closed-connection auto-open/close lifecycle
    // ------------------------------------------------------------------

    [Fact]
    public void Get_GivenAClosedConnection_OpensExecutesAndCloses()
    {
        using var fixture = new ClosedConnectionFixture();

        SweepWidget? widget = fixture.Connection.Get<SweepWidget>(1);

        Assert.NotNull(widget);
        Assert.Equal(ConnectionState.Closed, fixture.Connection.State);
    }

    [Fact]
    public async Task GetAsync_GivenAClosedConnection_OpensExecutesAndCloses()
    {
        using var fixture = new ClosedConnectionFixture();

        SweepWidget? widget = await fixture.Connection.GetAsync<SweepWidget>(1, TestContext.Current.CancellationToken);

        Assert.NotNull(widget);
        Assert.Equal(ConnectionState.Closed, fixture.Connection.State);
    }

    [Fact]
    public void GetAll_GivenAClosedConnection_OpensExecutesAndCloses()
    {
        using var fixture = new ClosedConnectionFixture();

        List<SweepWidget> widgets = fixture.Connection.GetAll<SweepWidget>();

        Assert.Single(widgets);
        Assert.Equal(ConnectionState.Closed, fixture.Connection.State);
    }

    [Fact]
    public void Delete_GivenAClosedConnection_OpensExecutesAndCloses()
    {
        using var fixture = new ClosedConnectionFixture();

        int rows = fixture.Connection.Delete<SweepWidget>(new SweepWidget { Id = 1 });

        Assert.Equal(1, rows);
        Assert.Equal(ConnectionState.Closed, fixture.Connection.State);
    }

    [Fact]
    public void Update_GivenAClosedConnection_OpensExecutesAndCloses()
    {
        using var fixture = new ClosedConnectionFixture();

        int rows = fixture.Connection.Update(new SweepWidget { Id = 1, Name = "b" });

        Assert.Equal(1, rows);
        Assert.Equal(ConnectionState.Closed, fixture.Connection.State);
    }

    [Fact]
    public void Upsert_GivenAClosedConnection_OpensExecutesAndCloses()
    {
        using var fixture = new ClosedConnectionFixture();

        int rows = fixture.Connection.Upsert(new SweepWidget { Id = 1, Name = "b" });

        Assert.Equal(1, rows);
        Assert.Equal(ConnectionState.Closed, fixture.Connection.State);
    }

    [Fact]
    public void ExecuteBatch_GivenAClosedConnection_OpensExecutesAndCloses()
    {
        using var fixture = new ClosedConnectionFixture();

        int rows = fixture.Connection.ExecuteBatch("UPDATE sweep_widget SET name = @name WHERE id = @id", [new { id = 1, name = "c" }]);

        Assert.Equal(1, rows);
        Assert.Equal(ConnectionState.Closed, fixture.Connection.State);
    }

    [Fact]
    public async Task ExecuteBatchAsync_GivenAClosedConnection_OpensExecutesAndCloses()
    {
        using var fixture = new ClosedConnectionFixture();

        int rows = await fixture.Connection.ExecuteBatchAsync(
            "UPDATE sweep_widget SET name = @name WHERE id = @id", [new { id = 1, name = "c" }], TestContext.Current.CancellationToken);

        Assert.Equal(1, rows);
        Assert.Equal(ConnectionState.Closed, fixture.Connection.State);
    }

    [Fact]
    public void Query_GivenAClosedConnection_OpensExecutesAndCloses()
    {
        using var fixture = new ClosedConnectionFixture();

        List<SweepWidget> widgets = fixture.Connection.Query<SweepWidget>("SELECT id AS Id, name AS Name FROM sweep_widget");

        Assert.Single(widgets);
        Assert.Equal(ConnectionState.Closed, fixture.Connection.State);
    }
}
