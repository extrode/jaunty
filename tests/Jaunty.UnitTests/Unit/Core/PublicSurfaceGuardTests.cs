using System.Data;

using Jaunty.Core;
using Jaunty.Diagnostics;
using Jaunty.Interceptors;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Core;

/// <summary>
/// Round 35, batch 06c. Three public entry points that accepted input they cannot honour:
/// a negative command timeout, a null reader or connection, and a retained audit record handed to a
/// reader with every setter open.
/// </summary>
public class PublicSurfaceGuardTests
{
    // ------------------------------------------------------------------
    // AUD-R35-149: the negative timeout
    // ------------------------------------------------------------------

    [Fact]
    public void ANegativeTimeout_IsRejectedWhereItIsWritten()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => CommandOptions.WithTimeout(-1));

        Assert.Equal("commandTimeout", ex.ParamName);
    }

    [Fact]
    public void ANegativeTimeout_IsRejectedOnTheGenericOptionsToo()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CommandOptions<int>.WithTimeout(-5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandOptions<int>(commandTimeout: -5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandOptions(commandTimeout: -5));
    }

    [Fact]
    public void ANegativeTimeout_IsRejectedAtEveryMultiEntityArity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiEntityCommandOptions<int, int>(commandTimeout: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiEntityCommandOptions<int, int, int>(commandTimeout: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiEntityCommandOptions<int, int, int, int>(commandTimeout: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiEntityCommandOptions<int, int, int, int, int>(commandTimeout: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiEntityCommandOptions<int, int, int, int, int, int>(commandTimeout: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiEntityCommandOptions<int, int, int, int, int, int, int>(commandTimeout: -1));
    }

    /// <summary>Zero is ADO.NET's "no timeout" and stays valid; so does the unset default.</summary>
    [Fact]
    public void ZeroAndNull_AreStillAccepted()
    {
        Assert.Equal(0, CommandOptions.WithTimeout(0).CommandTimeout);
        Assert.Equal(90, CommandOptions.WithTimeout(90).CommandTimeout);
        Assert.Null(default(CommandOptions).CommandTimeout);
        Assert.Equal(0, new MultiEntityCommandOptions<int, int>(commandTimeout: 0).CommandTimeout);
    }

    // ------------------------------------------------------------------
    // AUD-R35-150: GridReader's constructor
    // ------------------------------------------------------------------

    [Fact]
    public void GridReader_RejectsANullReader()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");

        var ex = Assert.Throws<ArgumentNullException>(() => new GridReader(null!, connection, false));

        Assert.Equal("reader", ex.ParamName);
    }

    [Fact]
    public void GridReader_RejectsANullConnection()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        using IDataReader reader = command.ExecuteReader();

        var ex = Assert.Throws<ArgumentNullException>(() => new GridReader(reader, null!, false));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void GridReader_StillReadsWhatItIsGiven()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = "CREATE TABLE grid_rows (id INTEGER PRIMARY KEY); INSERT INTO grid_rows VALUES (1), (2);";
        seed.ExecuteNonQuery();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM grid_rows ORDER BY id;";
        using IDataReader reader = command.ExecuteReader();

        using var grid = new GridReader(reader, connection, closeConnection: false);

        Assert.Equal([1, 2], grid.Read<GridRow>().Select(r => r.Id));
    }

    public class GridRow
    {
        public int Id { get; set; }
    }

    // ------------------------------------------------------------------
    // AUD-R35-151: the audit trail is not writable by a reader
    // ------------------------------------------------------------------

    private static SqliteConnection _shared = new("Data Source=:memory:");

    private static CommandContext Context(string sql) =>
        new(sql, parameters: null, connection: _shared, commandType: CommandType.Text);

    private static AuditInterceptor RecordOne()
    {
        var interceptor = new AuditInterceptor();
        interceptor.OnCommandExecuting(Context("SELECT 1"));
        return interceptor;
    }

    [Fact]
    public void RewritingARetrievedRecord_DoesNotChangeTheTrail()
    {
        AuditInterceptor interceptor = RecordOne();

        AuditRecord retrieved = Assert.Single(interceptor.GetRecentRecords());
        long sequence = retrieved.Sequence;
        retrieved.Sequence = 999;
        retrieved.CommandText = "DROP TABLE audit";

        AuditRecord again = Assert.Single(interceptor.GetRecentRecords());
        Assert.Equal(sequence, again.Sequence);
        Assert.Equal("SELECT 1", again.CommandText);
    }

    [Fact]
    public void TheResultCannotBeCastBackToTheInternalArray()
    {
        AuditInterceptor interceptor = RecordOne();

        IEnumerable<AuditRecord> result = interceptor.GetRecentRecords();

        Assert.Null(result as AuditRecord[]);
    }

    [Fact]
    public void ANonPositiveCount_StillReturnsNothing()
    {
        AuditInterceptor interceptor = RecordOne();

        Assert.Empty(interceptor.GetRecentRecords(0));
        Assert.Empty(interceptor.GetRecentRecords(-1));
    }

    [Fact]
    public void TheMostRecentRecordsComeBackOldestFirst()
    {
        var interceptor = new AuditInterceptor();
        for (int i = 1; i <= 5; i++)
        {
            interceptor.OnCommandExecuting(Context("SELECT " + i));
        }

        List<AuditRecord> recent = [.. interceptor.GetRecentRecords(3)];

        Assert.Equal(["SELECT 3", "SELECT 4", "SELECT 5"], recent.Select(r => r.CommandText));
        Assert.Equal([3L, 4L, 5L], recent.Select(r => r.Sequence));
    }
}
