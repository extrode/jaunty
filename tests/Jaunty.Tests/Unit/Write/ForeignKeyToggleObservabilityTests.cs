using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Interfaces;
using Jaunty.Internals.Write;
using Jaunty.Tests.Helpers;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Write;

/// <summary>
/// AUD-R35-131 and AUD-R35-132. The foreign-key toggle ran on the provider default timeout while
/// every other command in the same bulk operation honoured <c>options.CommandTimeout</c>, and it
/// never reached the logger - so the one statement in the sequence that suspends referential
/// integrity was the one an audit reader could not see.
/// </summary>
[Collection("Jaunty Config State")]
public class ForeignKeyToggleObservabilityTests : IDisposable
{
    private readonly Action<string, object?>? _originalLogger = JauntyConfig.Logger;
    private readonly List<string> _logged = [];
    private readonly RecordingDbConnection _connection;
    private readonly ISqlDialect _dialect;

    public ForeignKeyToggleObservabilityTests()
    {
        var inner = new SqliteConnection("Data Source=:memory:");
        inner.Open();
        _dialect = SqlDialectFactory.GetDialect(inner);
        _connection = new RecordingDbConnection(inner);

        JauntyConfig.Logger = (sql, _) => _logged.Add(sql);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.Logger = _originalLogger;
        _connection.Dispose();
    }

    [Fact]
    public void DisableSync_AppliesTheCallersTimeout()
    {
        ForeignKeyToggleCoordinator.DisableSync(_connection, _dialect, null, 77);

        Assert.Equal(77, Assert.Single(_connection.Executed).CommandTimeout);
    }

    [Fact]
    public void EnableSync_AppliesTheCallersTimeout()
    {
        ForeignKeyToggleCoordinator.EnableSync(_connection, _dialect, null, 77);

        Assert.Equal(77, Assert.Single(_connection.Executed).CommandTimeout);
    }

    [Fact]
    public async Task DisableAsync_AppliesTheCallersTimeout()
    {
        await ForeignKeyToggleCoordinator.DisableAsync(_connection, _dialect, null, 77, TestContext.Current.CancellationToken);

        Assert.Equal(77, Assert.Single(_connection.Executed).CommandTimeout);
    }

    [Fact]
    public async Task EnableAsync_AppliesTheCallersTimeout()
    {
        await ForeignKeyToggleCoordinator.EnableAsync(_connection, _dialect, null, 77, TestContext.Current.CancellationToken);

        Assert.Equal(77, Assert.Single(_connection.Executed).CommandTimeout);
    }

    /// <summary>
    /// The control: no timeout given means the provider default is left alone, not overwritten with
    /// a zero.
    /// </summary>
    [Fact]
    public void NoTimeoutGiven_LeavesTheProviderDefault()
    {
        ForeignKeyToggleCoordinator.DisableSync(_connection, _dialect, null, null);

        Assert.NotEqual(77, Assert.Single(_connection.Executed).CommandTimeout);
    }

    [Fact]
    public void TheToggleStatementsReachTheLogger()
    {
        ForeignKeyToggleCoordinator.DisableSync(_connection, _dialect, null, null);
        ForeignKeyToggleCoordinator.EnableSync(_connection, _dialect, null, null);

        Assert.Equal(2, _logged.Count);
        Assert.Contains("foreign_keys", _logged[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("foreign_keys", _logged[1], StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(_logged[0], _logged[1]);
    }

    [Fact]
    public async Task TheAsyncTogglesReachTheLoggerToo()
    {
        await ForeignKeyToggleCoordinator.DisableAsync(_connection, _dialect, null, null, TestContext.Current.CancellationToken);
        await ForeignKeyToggleCoordinator.EnableAsync(_connection, _dialect, null, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, _logged.Count);
    }

    /// <summary>
    /// End to end, through the thirty-six call sites: a bulk write that suspends enforcement must
    /// say so in the log, and the toggle must carry the same timeout as the write it brackets.
    /// </summary>
    [Fact]
    public void ABulkWriteThatSuspendsEnforcement_LogsTheToggleWithTheCallersTimeout()
    {
        using (var seed = _connection.CreateCommand())
        {
            seed.CommandText = "CREATE TABLE fk_toggle_rows (id INTEGER PRIMARY KEY, name TEXT)";
            seed.ExecuteNonQuery();
        }

        _logged.Clear();
        _connection.Executed.Clear();

        _connection.BulkInsertIgnoreConstraints(
            new List<ToggleRow> { new() { Id = 1, Name = "a" } },
            CommandOptions.WithTimeout(77));

        Assert.Contains(_logged, sql => sql.IndexOf("foreign_keys", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.All(
            _connection.Executed.Where(e => e.CommandText.IndexOf("foreign_keys", StringComparison.OrdinalIgnoreCase) >= 0),
            e => Assert.Equal(77, e.CommandTimeout));
    }

    [Table("fk_toggle_rows")]
    public class ToggleRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }
}
