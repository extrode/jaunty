using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Integration tests for QueryAsync&lt;T1,T2,T3,T4,T5,T6&gt; — arity-6 async multi-entity mapping.
/// AUD-R11 batch-01: this arity previously had no async test file at all.
/// </summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class QueryMultiEntityAsyncN6Tests : IClassFixture<DialectFixture>
{
    internal sealed class Af1 { [Column("id1")] public long Id { get; set; } }
    internal sealed class Af2 { [Column("id2")] public long Id { get; set; } }
    internal sealed class Af3 { [Column("id3")] public long Id { get; set; } }
    internal sealed class Af4 { [Column("id4")] public long Id { get; set; } }
    internal sealed class Af5 { [Column("id5")] public long Id { get; set; } }
    internal sealed class Af6 { [Column("id6")] public long Id { get; set; } }

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        for (int i = 1; i <= 6; i++)
        {
            Execute(connection, $@"
                CREATE TABLE amultimap6_t{i} (id INTEGER PRIMARY KEY);
                INSERT INTO amultimap6_t{i} VALUES ({i});");
        }
        return connection;
    }

    private static void Execute(IDbConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private const string Sql = @"
        SELECT t1.id id1, t2.id id2, t3.id id3, t4.id id4, t5.id id5, t6.id id6
        FROM amultimap6_t1 t1
        CROSS JOIN amultimap6_t2 t2
        CROSS JOIN amultimap6_t3 t3
        CROSS JOIN amultimap6_t4 t4
        CROSS JOIN amultimap6_t5 t5
        CROSS JOIN amultimap6_t6 t6";

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_SixEntities_MapsAllSixTypes(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = await connection.QueryAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql);

        Assert.Single(results);
        var (e1, e2, e3, e4, e5, e6) = results[0];
        Assert.Equal(1, e1.Id);
        Assert.Equal(6, e6.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_SixEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = await connection.QueryAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 });

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_SixEntities_WithCommandOptions_UsesTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE amultimap6_t1 SET id = 100 WHERE id = 1";
            cmd.ExecuteNonQuery();
        }

        var options = new CommandOptions<(Af1, Af2, Af3, Af4, Af5, Af6)>(transaction: txn);
        var results = await connection.QueryAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql, options, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(100, results[0].Item1.Id);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_SixEntities_WithMultiEntityCommandOptions_ReturnsMappedTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Af1, Af2, Af3, Af4, Af5, Af6>();

        var results = await connection.QueryAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql, options, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(1, results[0].Item1.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_SixEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Af1, Af2, Af3, Af4, Af5, Af6)>();

        var results = await connection.QueryAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 }, options, CancellationToken.None);

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_SixEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Af1, Af2, Af3, Af4, Af5, Af6>();

        var results = await connection.QueryAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 }, options, CancellationToken.None);

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_SixEntities_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (e1, _, _, _, _, e6) = await connection.QueryFirstAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql);

        Assert.Equal(1, e1.Id);
        Assert.Equal(6, e6.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_SixEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (e1, _, _, _, _, _) = await connection.QueryFirstAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 });

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_SixEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Af1, Af2, Af3, Af4, Af5, Af6)>();

        var (e1, _, _, _, _, _) = await connection.QueryFirstAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql, options, CancellationToken.None);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_SixEntities_WithMultiEntityCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Af1, Af2, Af3, Af4, Af5, Af6>();

        var (e1, _, _, _, _, _) = await connection.QueryFirstAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql, options, CancellationToken.None);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_SixEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Af1, Af2, Af3, Af4, Af5, Af6)>();

        var (e1, _, _, _, _, _) = await connection.QueryFirstAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 }, options, CancellationToken.None);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstAsync_SixEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Af1, Af2, Af3, Af4, Af5, Af6>();

        var (e1, _, _, _, _, _) = await connection.QueryFirstAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 }, options, CancellationToken.None);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_SixEntities_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QueryFirstOrDefaultAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Item1.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_SixEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QueryFirstOrDefaultAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_SixEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Af1, Af2, Af3, Af4, Af5, Af6)>();

        var result = await connection.QueryFirstOrDefaultAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_SixEntities_WithMultiEntityCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Af1, Af2, Af3, Af4, Af5, Af6>();

        var result = await connection.QueryFirstOrDefaultAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_SixEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Af1, Af2, Af3, Af4, Af5, Af6)>();

        var result = await connection.QueryFirstOrDefaultAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 }, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_SixEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Af1, Af2, Af3, Af4, Af5, Af6>();

        var result = await connection.QueryFirstOrDefaultAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 }, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_SixEntities_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (e1, _, _, _, _, e6) = await connection.QuerySingleAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql);

        Assert.Equal(1, e1.Id);
        Assert.Equal(6, e6.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_SixEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (e1, _, _, _, _, _) = await connection.QuerySingleAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 });

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_SixEntities_WithCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Af1, Af2, Af3, Af4, Af5, Af6)>();

        var (e1, _, _, _, _, _) = await connection.QuerySingleAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql, options, CancellationToken.None);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_SixEntities_WithMultiEntityCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Af1, Af2, Af3, Af4, Af5, Af6>();

        var (e1, _, _, _, _, _) = await connection.QuerySingleAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql, options, CancellationToken.None);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_SixEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Af1, Af2, Af3, Af4, Af5, Af6)>();

        var (e1, _, _, _, _, _) = await connection.QuerySingleAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 }, options, CancellationToken.None);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleAsync_SixEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Af1, Af2, Af3, Af4, Af5, Af6>();

        var (e1, _, _, _, _, _) = await connection.QuerySingleAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 }, options, CancellationToken.None);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_SixEntities_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QuerySingleOrDefaultAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Item1.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_SixEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QuerySingleOrDefaultAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_SixEntities_WithCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Af1, Af2, Af3, Af4, Af5, Af6)>();

        var result = await connection.QuerySingleOrDefaultAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_SixEntities_WithMultiEntityCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Af1, Af2, Af3, Af4, Af5, Af6>();

        var result = await connection.QuerySingleOrDefaultAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_SixEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Af1, Af2, Af3, Af4, Af5, Af6)>();

        var result = await connection.QuerySingleOrDefaultAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 }, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QuerySingleOrDefaultAsync_SixEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Af1, Af2, Af3, Af4, Af5, Af6>();

        var result = await connection.QuerySingleOrDefaultAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 }, options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_SixEntities_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var seen = new List<(Af1, Af2, Af3, Af4, Af5, Af6)>();
        await foreach (var row in connection.QueryStreamAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_SixEntities_WithParameters_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var seen = new List<(Af1, Af2, Af3, Af4, Af5, Af6)>();
        await foreach (var row in connection.QueryStreamAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 }))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_SixEntities_WithCommandOptions_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Af1, Af2, Af3, Af4, Af5, Af6)>();

        var seen = new List<(Af1, Af2, Af3, Af4, Af5, Af6)>();
        await foreach (var row in connection.QueryStreamAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql, options, CancellationToken.None))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_SixEntities_WithMultiEntityCommandOptions_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Af1, Af2, Af3, Af4, Af5, Af6>();

        var seen = new List<(Af1, Af2, Af3, Af4, Af5, Af6)>();
        await foreach (var row in connection.QueryStreamAsync<Af1, Af2, Af3, Af4, Af5, Af6>(Sql, options, CancellationToken.None))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_SixEntities_WithParametersAndCommandOptions_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(Af1, Af2, Af3, Af4, Af5, Af6)>();

        var seen = new List<(Af1, Af2, Af3, Af4, Af5, Af6)>();
        await foreach (var row in connection.QueryStreamAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 }, options, CancellationToken.None))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_SixEntities_WithParametersAndMultiEntityCommandOptions_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<Af1, Af2, Af3, Af4, Af5, Af6>();

        var seen = new List<(Af1, Af2, Af3, Af4, Af5, Af6)>();
        await foreach (var row in connection.QueryStreamAsync<Af1, Af2, Af3, Af4, Af5, Af6>(
            $"{Sql} WHERE t1.id = @Id", new { Id = 1 }, options, CancellationToken.None))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
    }
}
