using System.Data;

using Jaunty.Core;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Streaming;

/// <summary>
/// AUD-R35-153 and AUD-R35-154. The options-without-parameters overloads -
/// <c>QueryPartialStream&lt;T&gt;(IDbConnection, string, CommandOptions&lt;T&gt;)</c> and its async twin -
/// had no call site anywhere in <c>tests/</c>, so the transaction and timeout path was unverified on
/// both sides for a query that takes no parameters.
/// </summary>
public class PartialStreamOptionsOverloadTests
{
    private static SqliteConnection Seed()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText =
            "CREATE TABLE stream_rows (id INTEGER PRIMARY KEY, name TEXT); " +
            "INSERT INTO stream_rows VALUES (1, 'one'), (2, 'two'), (3, 'three');";
        seed.ExecuteNonQuery();

        return connection;
    }

    private const string Sql = "SELECT id AS Id, name AS Name FROM stream_rows ORDER BY id;";

    public class StreamRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void TheOptionsOverload_Streams()
    {
        using SqliteConnection connection = Seed();

        List<StreamRow> rows = [.. connection.QueryPartialStream<StreamRow>(Sql, CommandOptions<StreamRow>.WithTimeout(30))];

        Assert.Equal([1, 2, 3], rows.Select(r => r.Id));
        Assert.Equal(["one", "two", "three"], rows.Select(r => r.Name));
    }

    /// <summary>
    /// The assertion that the options are actually plumbed through: SQLite refuses to execute a
    /// command on a connection with a pending local transaction unless the command carries that
    /// transaction, so dropping <c>options</c> on the floor turns this into a throw.
    /// </summary>
    [Fact]
    public void TheOptionsOverload_UsesTheTransactionItIsGiven()
    {
        using SqliteConnection connection = Seed();
        using SqliteTransaction transaction = connection.BeginTransaction();

        using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO stream_rows VALUES (4, 'four');";
            insert.ExecuteNonQuery();
        }

        List<StreamRow> rows =
            [.. connection.QueryPartialStream<StreamRow>(Sql, CommandOptions<StreamRow>.WithTransaction(transaction))];

        Assert.Equal([1, 2, 3, 4], rows.Select(r => r.Id));

        transaction.Rollback();
    }

    [Fact]
    public void TheOptionsOverload_ValidatesItsArguments()
    {
        using SqliteConnection connection = Seed();
        CommandOptions<StreamRow> options = CommandOptions<StreamRow>.WithTimeout(30);

        Assert.Throws<ArgumentNullException>("connection",
            () => ((IDbConnection)null!).QueryPartialStream<StreamRow>(Sql, options));
        Assert.Throws<ArgumentNullException>("sql",
            () => connection.QueryPartialStream<StreamRow>(null!, options));
        Assert.Throws<ArgumentException>("sql",
            () => connection.QueryPartialStream<StreamRow>("   ", options));
    }

    [Fact]
    public async Task TheAsyncOptionsOverload_Streams()
    {
        using SqliteConnection connection = Seed();

        var rows = new List<StreamRow>();
        await foreach (StreamRow row in connection.QueryPartialStreamAsync<StreamRow>(Sql, CommandOptions<StreamRow>.WithTimeout(30)))
            rows.Add(row);

        Assert.Equal([1, 2, 3], rows.Select(r => r.Id));
        Assert.Equal(["one", "two", "three"], rows.Select(r => r.Name));
    }

    [Fact]
    public async Task TheAsyncOptionsOverload_UsesTheTransactionItIsGiven()
    {
        using SqliteConnection connection = Seed();
        using SqliteTransaction transaction = connection.BeginTransaction();

        using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO stream_rows VALUES (4, 'four');";
            insert.ExecuteNonQuery();
        }

        var rows = new List<StreamRow>();
        await foreach (StreamRow row in connection.QueryPartialStreamAsync<StreamRow>(
            Sql, CommandOptions<StreamRow>.WithTransaction(transaction)))
        {
            rows.Add(row);
        }

        Assert.Equal([1, 2, 3, 4], rows.Select(r => r.Id));

        transaction.Rollback();
    }

    [Fact]
    public async Task TheAsyncOptionsOverload_HonoursCancellation()
    {
        using SqliteConnection connection = Seed();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (StreamRow row in connection.QueryPartialStreamAsync<StreamRow>(
                Sql, CommandOptions<StreamRow>.WithTimeout(30), cancelled.Token))
            {
                _ = row;
            }
        });
    }

    [Fact]
    public void TheAsyncOptionsOverload_ValidatesItsArguments()
    {
        using SqliteConnection connection = Seed();
        CommandOptions<StreamRow> options = CommandOptions<StreamRow>.WithTimeout(30);

        Assert.Throws<ArgumentNullException>("connection",
            () => ((IDbConnection)null!).QueryPartialStreamAsync<StreamRow>(Sql, options));
        Assert.Throws<ArgumentNullException>("sql",
            () => connection.QueryPartialStreamAsync<StreamRow>(null!, options));
        Assert.Throws<ArgumentException>("sql",
            () => connection.QueryPartialStreamAsync<StreamRow>("   ", options));
    }
}
