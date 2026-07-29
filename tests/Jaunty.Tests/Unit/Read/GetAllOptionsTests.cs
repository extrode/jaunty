using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Interceptors;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R26 (batch 3, low/performance). <c>GetAll</c> accepted a <c>CommandOptions&lt;T&gt;</c> and
/// then ignored two of the things it carried.
///
/// <para>
/// <strong>ExpectedRowCount was discarded.</strong> All three result lists in
/// <c>GetAllCore</c> were sized <c>new List&lt;T&gt;(JauntyConfig.QueryResultCapacity)</c> and
/// <c>options.ExpectedRowCount</c> was not referenced anywhere in the file — while every
/// <c>QueryCore</c> result list uses <c>options.ExpectedRowCount ?? QueryResultCapacity</c> across
/// fifteen sites. <c>GetAll</c> is the API where a caller is most likely to know the table size,
/// and <c>WithExpectedRowCount</c> documents itself as a "Hint for expected row count to optimize
/// list allocation", so the one call that most plausibly knows better was the one that could not
/// say so.
/// </para>
///
/// <para>
/// <strong>CommandType was reported but not applied.</strong> <c>GetAllCore</c> passed
/// <c>options.CommandType</c> to the interceptor pipeline while never assigning
/// <c>command.CommandType</c>, so an interceptor was told the command was a stored procedure while
/// it executed as text. Same "what interceptors are actually told" theme as AUD-R26-020.
/// </para>
/// </summary>
[Collection("Jaunty Config State")]
public class GetAllOptionsTests : IDisposable
{
    [Table("rows")]
    private sealed class Row
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = "";
    }

    private sealed class CapturingInterceptor : ICommandInterceptor
    {
        public CommandType? SeenCommandType { get; private set; }

        public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
        {
            SeenCommandType = context.CommandType;
            return default;
        }

        public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken) => default;

        public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken) => default;
    }

    public void Dispose()
    {
        JauntyConfig.ClearInterceptors();
        JauntyConfig.QueryResultCapacity = 16;
        GC.SuppressFinalize(this);
    }

    private static SqliteConnection Seed(int rows)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = "CREATE TABLE rows (id INTEGER, name TEXT);";
        seed.ExecuteNonQuery();

        for (int i = 0; i < rows; i++)
        {
            using SqliteCommand insert = connection.CreateCommand();
            insert.CommandText = $"INSERT INTO rows VALUES ({i}, 'n{i}');";
            insert.ExecuteNonQuery();
        }

        return connection;
    }

    // ------------------------------------------------------------------
    // ExpectedRowCount is honoured
    // ------------------------------------------------------------------

    /// <summary>
    /// The hint has to reach the list, and the only observable proof of a capacity hint is that the
    /// growth reallocations do not happen. Measured against the default capacity of 16 with 512
    /// rows: without the hint the list doubles from 16, copying 16+32+…+256 elements on the way.
    /// </summary>
    [Fact]
    public void ExpectedRowCount_IsHonouredByGetAll()
    {
        JauntyConfig.QueryResultCapacity = 16;
        using SqliteConnection connection = Seed(512);

        // Warm every cache: metadata, SQL, mapper. Otherwise first-call setup dwarfs the delta.
        _ = connection.GetAll<Row>();
        _ = connection.GetAll<Row>(CommandOptions<Row>.WithExpectedRowCount(512));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = connection.GetAll<Row>();
        long unhinted = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        _ = connection.GetAll<Row>(CommandOptions<Row>.WithExpectedRowCount(512));
        long hinted = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(hinted < unhinted,
            $"Expected the row-count hint to allocate less than the default capacity; " +
            $"hinted {hinted}, unhinted {unhinted}.");
    }

    [Fact]
    public void ExpectedRowCount_DoesNotChangeTheResult()
    {
        using SqliteConnection connection = Seed(40);

        List<Row> hinted = connection.GetAll<Row>(CommandOptions<Row>.WithExpectedRowCount(40));
        List<Row> unhinted = connection.GetAll<Row>();

        Assert.Equal(40, hinted.Count);
        Assert.Equal(unhinted.Count, hinted.Count);
        Assert.Equal(unhinted.Select(r => r.Id), hinted.Select(r => r.Id));
    }

    /// <summary>A hint far larger than the result set must not change what comes back.</summary>
    [Fact]
    public void AnOverstatedHint_StillReturnsTheRealRows()
    {
        using SqliteConnection connection = Seed(3);

        List<Row> rows = connection.GetAll<Row>(CommandOptions<Row>.WithExpectedRowCount(100_000));

        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task ExpectedRowCount_IsHonouredByGetAllAsync()
    {
        using SqliteConnection connection = Seed(40);

        List<Row> rows = await connection.GetAllAsync<Row>(
            CommandOptions<Row>.WithExpectedRowCount(40), TestContext.Current.CancellationToken);

        Assert.Equal(40, rows.Count);
    }

    // ------------------------------------------------------------------
    // CommandType is applied, not merely announced
    // ------------------------------------------------------------------

    /// <summary>
    /// GetAllCore handed options.CommandType to the interceptor pipeline but never assigned
    /// command.CommandType, so the interceptor's record and the executed command disagreed. Asking
    /// SQLite for CommandType.StoredProcedure is the cheapest way to observe it: the provider
    /// rejects it, which can only happen if the value actually reached the command.
    /// </summary>
    [Fact]
    public void CommandType_ReachesTheCommandAndNotOnlyTheInterceptor()
    {
        var interceptor = new CapturingInterceptor();
        JauntyConfig.ClearInterceptors();
        JauntyConfig.AddInterceptor(interceptor);

        using SqliteConnection connection = Seed(1);

        CommandOptions<Row> options = CommandOptions<Row>.AsStoredProcedure();

        // Microsoft.Data.Sqlite supports only CommandType.Text; anything else throws on assignment.
        Assert.ThrowsAny<Exception>(() => { _ = connection.GetAll<Row>(options); });

        // And the interceptor was told the same thing the command was given.
        Assert.Equal(CommandType.StoredProcedure, interceptor.SeenCommandType);
    }

    [Fact]
    public void TheDefaultCommandType_StillExecutesAsText()
    {
        using SqliteConnection connection = Seed(3);
        Assert.Equal(3, connection.GetAll<Row>(CommandOptions<Row>.WithExpectedRowCount(3)).Count);
    }

    [Fact]
    public void NoHint_StillWorks()
    {
        using SqliteConnection connection = Seed(5);
        Assert.Equal(5, connection.GetAll<Row>().Count);
    }
}
