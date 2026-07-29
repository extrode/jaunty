using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Interceptors;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Write;

/// <summary>
/// AUD-R26 (batch 2, high/security). Eleven public write APIs executed their commands without ever
/// consulting <see cref="JauntyConfig.InterceptorPipeline"/> or <see cref="JauntyConfig.Logger"/>:
/// <c>Upsert</c>, <c>UpsertAsync</c>, the six <c>Bulk*</c> families, and <c>ExecuteBatch</c> /
/// <c>ExecuteBatchAsync</c>. Measured by grep across the whole write surface - zero occurrences of
/// either hook in any of the nine files.
///
/// <para>
/// The contract promised otherwise in writing. <see cref="ICommandInterceptor"/>'s <c>remarks</c>
/// enumerate the one known exemption - the streaming read APIs, which cannot report a completed
/// command without materialising every row - and end "<i>as do all write operations</i>". Someone
/// registering <c>AuditInterceptor</c> for a compliance requirement got every single-row
/// <c>Insert</c> and none of the <c>BulkInsert</c> calls that wrote the overwhelming majority of the
/// rows. The streaming rationale does not transfer: all eleven are fully buffered - they execute,
/// complete, and return a row count.
/// </para>
///
/// <para>
/// This is the direct counterpart to <c>SpParametersObservabilityTests</c>, which pinned the same
/// class of gap for stored procedures in round 25.
/// </para>
/// </summary>
[Collection("Jaunty Config State")]
public class WriteObservabilityTests : IDisposable
{
    [Table("write_obs_widgets")]
    public class Widget
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    private readonly Action<string, object?>? _originalLogger = JauntyConfig.Logger;
    private readonly List<(string Sql, object? Parameters)> _logged = [];
    private readonly RecordingInterceptor _interceptor = new();
    private readonly SqliteConnection _connection;

    public WriteObservabilityTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using (SqliteCommand cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE write_obs_widgets (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Quantity INTEGER NOT NULL)
                """;
            cmd.ExecuteNonQuery();
        }

        JauntyConfig.ClearInterceptors();
        JauntyConfig.Logger = (sql, parameters) => _logged.Add((sql, parameters));
        JauntyConfig.AddInterceptor(_interceptor);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.ClearInterceptors();
        JauntyConfig.Logger = _originalLogger;
        _connection.Dispose();
    }

    private static List<Widget> Widgets(int n) =>
        [.. Enumerable.Range(0, n).Select(i => new Widget { Name = $"w{i}", Quantity = i })];

    private List<Widget> Seed(int n)
    {
        List<Widget> widgets = Widgets(n);
        _connection.BulkInsert(widgets);
        _interceptor.Reset();
        _logged.Clear();
        return widgets;
    }

    // ---------------------------------------------------------------------------
    // Interception - one event per operation
    // ---------------------------------------------------------------------------

    [Fact]
    public void Upsert_IsIntercepted()
    {
        _connection.Upsert(new Widget { Name = "a", Quantity = 1 });

        Assert.Single(_interceptor.Executing);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public async Task UpsertAsync_IsIntercepted()
    {
        await _connection.UpsertAsync(new Widget { Name = "a", Quantity = 1 }, TestContext.Current.CancellationToken);

        Assert.Single(_interceptor.Executing);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public void BulkInsert_IsIntercepted()
    {
        _connection.BulkInsert(Widgets(5));

        Assert.Single(_interceptor.Executing);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public async Task BulkInsertAsync_IsIntercepted()
    {
        await _connection.BulkInsertAsync(Widgets(5), TestContext.Current.CancellationToken);

        Assert.Single(_interceptor.Executing);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public void BulkUpdate_IsIntercepted()
    {
        List<Widget> widgets = Seed(3);
        foreach (Widget w in widgets) w.Quantity += 100;

        _connection.BulkUpdate(widgets);

        Assert.Single(_interceptor.Executing);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public async Task BulkUpdateAsync_IsIntercepted()
    {
        List<Widget> widgets = Seed(3);
        foreach (Widget w in widgets) w.Quantity += 100;

        await _connection.BulkUpdateAsync(widgets, TestContext.Current.CancellationToken);

        Assert.Single(_interceptor.Executing);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public void BulkDelete_IsIntercepted()
    {
        List<Widget> widgets = Seed(3);

        _connection.BulkDelete(widgets);

        Assert.Single(_interceptor.Executing);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public async Task BulkDeleteAsync_IsIntercepted()
    {
        List<Widget> widgets = Seed(3);

        await _connection.BulkDeleteAsync(widgets, TestContext.Current.CancellationToken);

        Assert.Single(_interceptor.Executing);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public void ExecuteBatch_IsIntercepted()
    {
        _connection.ExecuteBatch(
            "INSERT INTO write_obs_widgets (Name, Quantity) VALUES (@Name, @Quantity)",
            [new { Name = "a", Quantity = 1 }, new { Name = "b", Quantity = 2 }]);

        Assert.Single(_interceptor.Executing);
        Assert.Single(_interceptor.Executed);
    }

    [Fact]
    public async Task ExecuteBatchAsync_IsIntercepted()
    {
        await _connection.ExecuteBatchAsync(
            "INSERT INTO write_obs_widgets (Name, Quantity) VALUES (@Name, @Quantity)",
            [new { Name = "a", Quantity = 1 }, new { Name = "b", Quantity = 2 }],
            TestContext.Current.CancellationToken);

        Assert.Single(_interceptor.Executing);
        Assert.Single(_interceptor.Executed);
    }

    // ---------------------------------------------------------------------------
    // What gets reported
    // ---------------------------------------------------------------------------

    /// <summary>
    /// A 100,000-row BulkInsert is one logical write. Firing the pipeline per row would swamp an
    /// auditor and cost more than the bulk path saves, so the operation is reported once - and that
    /// is asserted rather than left as an implementation detail, because "once" is the whole design.
    /// </summary>
    [Fact]
    public void ABulkOperation_IsReportedOnce_NotOncePerRow()
    {
        _connection.BulkInsert(Widgets(50));

        Assert.Single(_interceptor.Executing);
        Assert.Single(_interceptor.Executed);
        Assert.Single(_logged);
    }

    /// <summary>
    /// The entity list is deliberately not handed over as the parameter object.
    /// <c>LoggingInterceptor.FormatParameters</c> reflects over the object's own public properties,
    /// so a <c>List&lt;T&gt;</c> would log <c>Capacity=…, Count=…</c> - the same meaningless output
    /// batch 1 recorded against the mis-documented "positional parameters" path. The operation
    /// describes itself instead.
    /// </summary>
    [Fact]
    public void ABulkOperation_ReportsOperationTypeAndRowCount()
    {
        _connection.BulkInsert(Widgets(7));

        object? reported = Assert.Single(_interceptor.Executing).Parameters;
        Assert.NotNull(reported);

        string description = reported!.ToString()!;
        Assert.Contains("BulkInsert", description, StringComparison.Ordinal);
        Assert.Contains(nameof(Widget), description, StringComparison.Ordinal);
        Assert.Contains("7", description, StringComparison.Ordinal);

        // Not the raw list: that is what produces Capacity=/Count= in a log.
        Assert.IsNotAssignableFrom<System.Collections.IEnumerable>(reported);
    }

    /// <summary>
    /// The SQL reported is the statement the operation runs, not a placeholder - an interceptor
    /// filtering or redacting by command text has to have something real to match on.
    /// </summary>
    [Fact]
    public void ABulkOperation_ReportsTheStatementItRuns()
    {
        _connection.BulkInsert(Widgets(3));

        string sql = Assert.Single(_interceptor.Executing).CommandText;

        Assert.Contains("INSERT INTO", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("write_obs_widgets", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ExecuteBatch takes a lazy IEnumerable, so the count is reported only when the caller's
    /// sequence already knows it. Draining it to fill in a log line would change the method's
    /// memory behaviour, which is a worse trade than an absent number.
    /// </summary>
    [Fact]
    public void ExecuteBatch_ReportsNoRowCount_ForALazySequence()
    {
        IEnumerable<object> Lazy()
        {
            yield return new { Name = "a", Quantity = 1 };
            yield return new { Name = "b", Quantity = 2 };
        }

        _connection.ExecuteBatch("INSERT INTO write_obs_widgets (Name, Quantity) VALUES (@Name, @Quantity)", Lazy());

        string description = Assert.Single(_interceptor.Executing).Parameters!.ToString()!;

        // The operation and nothing else - no "xN" suffix, because the count is genuinely unknown.
        Assert.Equal("ExecuteBatch", description);
    }

    [Fact]
    public void ExecuteBatch_ReportsTheCount_WhenTheSequenceKnowsIt()
    {
        _connection.ExecuteBatch(
            "INSERT INTO write_obs_widgets (Name, Quantity) VALUES (@Name, @Quantity)",
            [new { Name = "a", Quantity = 1 }, new { Name = "b", Quantity = 2 }]);

        string description = Assert.Single(_interceptor.Executing).Parameters!.ToString()!;

        Assert.Contains("x2", description, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------
    // Logging
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("Upsert")]
    [InlineData("BulkInsert")]
    [InlineData("BulkUpdate")]
    [InlineData("BulkDelete")]
    [InlineData("ExecuteBatch")]
    public void EveryWritePath_InvokesTheLogger(string operation)
    {
        switch (operation)
        {
            case "Upsert":
                _connection.Upsert(new Widget { Name = "a", Quantity = 1 });
                break;
            case "BulkInsert":
                _connection.BulkInsert(Widgets(3));
                break;
            case "BulkUpdate":
                {
                    List<Widget> widgets = Seed(3);
                    _connection.BulkUpdate(widgets);
                    break;
                }
            case "BulkDelete":
                {
                    List<Widget> widgets = Seed(3);
                    _connection.BulkDelete(widgets);
                    break;
                }
            case "ExecuteBatch":
                _connection.ExecuteBatch(
                    "INSERT INTO write_obs_widgets (Name, Quantity) VALUES (@Name, @Quantity)",
                    [new { Name = "a", Quantity = 1 }]);
                break;
        }

        Assert.NotEmpty(_logged);
    }

    // ---------------------------------------------------------------------------
    // The single-row paths, which already worked
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Insert/Update/Delete/Execute spell the interception inline in their own *Core.cs files and
    /// predate <c>WriteInterception</c>. They were deliberately not rewritten to use it - they are
    /// correct and covered, and rewriting a working transactional write path to prove a point is a
    /// bad trade. Asserting them here instead means the whole write surface is pinned by one class,
    /// so the inline form and the helper cannot drift apart unnoticed.
    /// </summary>
    [Theory]
    [InlineData("Insert")]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("Execute")]
    public void TheSingleRowPaths_AreAlsoIntercepted(string operation)
    {
        switch (operation)
        {
            case "Insert":
                _connection.Insert(new Widget { Name = "a", Quantity = 1 });
                break;
            case "Update":
                {
                    Widget widget = Seed(1)[0];
                    widget.Quantity = 99;
                    _connection.Update(widget);
                    break;
                }
            case "Delete":
                {
                    Widget widget = Seed(1)[0];
                    _connection.Delete(widget);
                    break;
                }
            case "Execute":
                _connection.Execute(
                    "INSERT INTO write_obs_widgets (Name, Quantity) VALUES (@Name, @Quantity)",
                    new { Name = "a", Quantity = 1 });
                break;
        }

        Assert.NotEmpty(_interceptor.Executing);
        Assert.NotEmpty(_interceptor.Executed);
    }

    // ---------------------------------------------------------------------------
    // Failure reporting
    // ---------------------------------------------------------------------------

    /// <summary>
    /// A failed write has to reach <c>OnCommandFailed</c>, not just vanish - an audit trail that
    /// records only successes is the one an auditor cares least about.
    /// </summary>
    [Fact]
    public void AFailedBulkWrite_IsReportedAsFailed()
    {
        // Name is NOT NULL, so this violates the constraint inside the bulk path.
        List<Widget> widgets = [new Widget { Name = null!, Quantity = 1 }];

        Assert.ThrowsAny<Exception>(() => _connection.BulkInsert(widgets));

        Assert.Single(_interceptor.Failed);
        Assert.Empty(_interceptor.Executed);
    }

    // ---------------------------------------------------------------------------

    private sealed class RecordingInterceptor : ICommandInterceptor
    {
        public List<CommandContext> Executing { get; } = [];
        public List<CommandContext> Executed { get; } = [];
        public List<CommandContext> Failed { get; } = [];

        public void Reset()
        {
            Executing.Clear();
            Executed.Clear();
            Failed.Clear();
        }

        public void OnCommandExecuting(CommandContext context) => Executing.Add(context);
        public void OnCommandExecuted(CommandContext context) => Executed.Add(context);
        public void OnCommandFailed(CommandContext context) => Failed.Add(context);

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Executing.Add(context);
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Executed.Add(context);
            return default;
        }

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
        {
            Failed.Add(context);
            return default;
        }
    }
}
