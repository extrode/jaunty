using System.Data;

using Jaunty.Diagnostics;
using Jaunty.Interceptors;

using Xunit;

namespace Jaunty.Tests.Unit.Interceptors;

/// <summary>
/// AUD-R26-055 (batch 4, low/bug). <c>GetRecentRecords</c> is documented to return records
/// <em>"in chronological order"</em> and did not, for any records sharing a timestamp.
///
/// <para>
/// It sorted with <c>OrderByDescending(r =&gt; r.Timestamp).Take(count).Reverse()</c>.
/// <c>OrderByDescending</c> is stable, so equal timestamps keep insertion order <em>within the
/// descending sequence</em> - and the trailing <c>Reverse()</c> then flips them, handing ties back
/// in reverse insertion order.
/// </para>
///
/// <para>
/// The <c>Take</c> is the worse half. On a run of equal timestamps the stable descending sort leaves
/// the queue in its original oldest-first order, so <c>Take(count)</c> selects the <b>oldest</b>
/// <c>count</c> records - from a method called <c>GetRecentRecords</c>. A caller asking for the last
/// 10 of a burst got the first 10, reversed.
/// </para>
///
/// <para>
/// Ties are the normal case rather than an edge case: <c>Timestamp</c> is
/// <see cref="DateTime.UtcNow"/>, whose resolution is about 15.6 ms on Windows, and this interceptor
/// writes two records per command - so a single command can produce a mis-ordered pair. These tests
/// force the tie by assigning equal timestamps directly, rather than depending on the host clock's
/// resolution to produce one.
/// </para>
/// </summary>
public class AuditRecordOrderingTests
{
    private static CommandContext Context(string sql) =>
        new(sql, null, new StubConnection(), CommandType.Text);

    private static AuditInterceptor WithCommands(int count, int maxRecords = 1000)
    {
        var interceptor = new AuditInterceptor(maxRecords);
        for (int i = 1; i <= count; i++)
            interceptor.OnCommandExecuting(Context($"SELECT {i}"));

        return interceptor;
    }

    /// <summary>
    /// Flattens the clock so every record ties, which is what the sort could not handle. The records
    /// returned are the live instances held by the interceptor, so writing to them changes what a
    /// later call sees.
    /// </summary>
    private static void FlattenTimestamps(AuditInterceptor interceptor)
    {
        var fixedInstant = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (AuditRecord record in interceptor.GetRecentRecords(int.MaxValue))
            record.Timestamp = fixedInstant;
    }

    // ------------------------------------------------------------------
    // Chronological means chronological
    // ------------------------------------------------------------------

    [Fact]
    public void GetRecentRecords_WithTiedTimestamps_ReturnsInsertionOrder()
    {
        AuditInterceptor interceptor = WithCommands(5);
        FlattenTimestamps(interceptor);

        string[] order = interceptor.GetRecentRecords().Select(r => r.CommandText).ToArray();

        Assert.Equal(["SELECT 1", "SELECT 2", "SELECT 3", "SELECT 4", "SELECT 5"], order);
    }

    /// <summary>
    /// The two records a single command writes - <c>Executing</c> then <c>Executed</c> - are the
    /// commonest tie of all, and came back the wrong way round.
    /// </summary>
    [Fact]
    public void GetRecentRecords_ExecutingThenExecuted_KeepsThatOrder()
    {
        var interceptor = new AuditInterceptor();
        CommandContext context = Context("SELECT 1");
        interceptor.OnCommandExecuting(context);
        interceptor.OnCommandExecuted(context);
        FlattenTimestamps(interceptor);

        AuditPhase[] phases = interceptor.GetRecentRecords().Select(r => r.Phase).ToArray();

        Assert.Equal([AuditPhase.Executing, AuditPhase.Executed], phases);
    }

    // ------------------------------------------------------------------
    // "Recent" means recent
    // ------------------------------------------------------------------

    /// <summary>
    /// The sharpest case: with every timestamp equal, the old implementation returned the
    /// <em>oldest</em> <c>count</c> records, reversed. This asserts both halves at once - which
    /// records come back, and in what order.
    /// </summary>
    [Fact]
    public void GetRecentRecords_WithCount_ReturnsTheNewestNotTheOldest()
    {
        AuditInterceptor interceptor = WithCommands(5);
        FlattenTimestamps(interceptor);

        string[] order = interceptor.GetRecentRecords(2).Select(r => r.CommandText).ToArray();

        Assert.Equal(["SELECT 4", "SELECT 5"], order);
    }

    [Fact]
    public void GetRecentRecords_CountAboveTotal_ReturnsEverythingInOrder()
    {
        AuditInterceptor interceptor = WithCommands(3);
        FlattenTimestamps(interceptor);

        string[] order = interceptor.GetRecentRecords(100).Select(r => r.CommandText).ToArray();

        Assert.Equal(["SELECT 1", "SELECT 2", "SELECT 3"], order);
    }

    [Fact]
    public void GetRecentRecords_NonPositiveCount_ReturnsEmpty()
    {
        AuditInterceptor interceptor = WithCommands(3);

        Assert.Empty(interceptor.GetRecentRecords(0));
        Assert.Empty(interceptor.GetRecentRecords(-1));
    }

    /// <summary>
    /// Trimming keeps the newest records, and what survives is still in order. The bound is on
    /// records rather than commands, so a cap of 4 retains the last four writes.
    /// </summary>
    [Fact]
    public void GetRecentRecords_AfterTrimming_ReturnsTheSurvivorsInOrder()
    {
        AuditInterceptor interceptor = WithCommands(6, maxRecords: 4);
        FlattenTimestamps(interceptor);

        string[] order = interceptor.GetRecentRecords().Select(r => r.CommandText).ToArray();

        Assert.Equal(["SELECT 3", "SELECT 4", "SELECT 5", "SELECT 6"], order);
    }

    // ------------------------------------------------------------------
    // The sequence number
    // ------------------------------------------------------------------

    /// <summary>
    /// <see cref="DateTime.UtcNow"/> cannot totally order an audit trail, so a consumer that
    /// persists these records and sorts them later cannot recover the order from
    /// <c>Timestamp</c> alone. <c>Sequence</c> is monotonic per interceptor and does.
    /// </summary>
    [Fact]
    public void Sequence_IsStrictlyIncreasingInInsertionOrder()
    {
        AuditInterceptor interceptor = WithCommands(5);
        FlattenTimestamps(interceptor);

        long[] sequences = interceptor.GetRecentRecords().Select(r => r.Sequence).ToArray();

        Assert.Equal(5, sequences.Length);
        for (int i = 1; i < sequences.Length; i++)
            Assert.True(sequences[i] > sequences[i - 1], $"Sequence went {sequences[i - 1]} -> {sequences[i]}.");
    }

    /// <summary>
    /// Trimming must not renumber: a gap at the start is how a consumer sees that records were
    /// dropped, which is the point of having the number at all.
    /// </summary>
    [Fact]
    public void Sequence_SurvivesTrimming_LeavingAGap()
    {
        AuditInterceptor interceptor = WithCommands(6, maxRecords: 4);

        long[] sequences = interceptor.GetRecentRecords().Select(r => r.Sequence).ToArray();

        Assert.Equal(4, sequences.Length);
        Assert.Equal(3, sequences[0]);
        Assert.Equal(6, sequences[sequences.Length - 1]);
    }

    /// <summary>
    /// Concurrent writers must not collide on a sequence number, or the trail is no longer totally
    /// ordered - which is the one thing this field exists to guarantee.
    /// </summary>
    [Fact]
    public async Task Sequence_UnderConcurrentWriters_HasNoDuplicates()
    {
        var interceptor = new AuditInterceptor(10_000);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
                interceptor.OnCommandExecuting(Context($"SELECT {t}-{i}"));
        })));

        long[] sequences = interceptor.GetRecentRecords(int.MaxValue).Select(r => r.Sequence).ToArray();

        Assert.Equal(800, sequences.Length);
        Assert.Equal(800, sequences.Distinct().Count());
    }

    private sealed class StubConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => "TestDb";
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Dispose() { }
        public void Open() { }
    }
}
