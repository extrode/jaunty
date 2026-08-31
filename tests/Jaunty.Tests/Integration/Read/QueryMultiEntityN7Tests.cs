using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>Integration tests for Query&lt;T1,T2,T3,T4,T5,T6,T7&gt; — arity-7 multi-entity mapping.</summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class QueryMultiEntityN7Tests : IClassFixture<DialectFixture>
{
    internal sealed class E1 { [Column("id1")] public long Id { get; set; } }
    internal sealed class E2 { [Column("id2")] public long Id { get; set; } }
    internal sealed class E3 { [Column("id3")] public long Id { get; set; } }
    internal sealed class E4 { [Column("id4")] public long Id { get; set; } }
    internal sealed class E5 { [Column("id5")] public long Id { get; set; } }
    internal sealed class E6 { [Column("id6")] public long Id { get; set; } }
    internal sealed class E7 { [Column("id7")] public long Id { get; set; } }

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        for (int i = 1; i <= 7; i++)
        {
            Execute(connection, $@"
                CREATE TABLE multimap7_t{i} (id INTEGER PRIMARY KEY);
                INSERT INTO multimap7_t{i} VALUES ({i});");
        }
        return connection;
    }

    private static void Execute(IDbConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    [Theory]
    [SystemSqlite]
    public void Query_SevenEntities_MapsAllSevenTypes(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        const string sql = @"
            SELECT t1.id id1, t2.id id2, t3.id id3, t4.id id4, t5.id id5, t6.id id6, t7.id id7
            FROM multimap7_t1 t1
            CROSS JOIN multimap7_t2 t2
            CROSS JOIN multimap7_t3 t3
            CROSS JOIN multimap7_t4 t4
            CROSS JOIN multimap7_t5 t5
            CROSS JOIN multimap7_t6 t6
            CROSS JOIN multimap7_t7 t7";

        var results = connection.Query<E1, E2, E3, E4, E5, E6, E7>(sql);
        Assert.Single(results);
        var (e1, e2, e3, e4, e5, e6, e7) = results[0];
        Assert.Equal(1, e1.Id);
        Assert.Equal(2, e2.Id);
        Assert.Equal(3, e3.Id);
        Assert.Equal(4, e4.Id);
        Assert.Equal(5, e5.Id);
        Assert.Equal(6, e6.Id);
        Assert.Equal(7, e7.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_SevenEntities_ReturnsFirstOrNull(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        const string sql = @"
            SELECT t1.id id1, t2.id id2, t3.id id3, t4.id id4, t5.id id5, t6.id id6, t7.id id7
            FROM multimap7_t1 t1
            CROSS JOIN multimap7_t2 t2
            CROSS JOIN multimap7_t3 t3
            CROSS JOIN multimap7_t4 t4
            CROSS JOIN multimap7_t5 t5
            CROSS JOIN multimap7_t6 t6
            CROSS JOIN multimap7_t7 t7";

        var result = connection.QueryFirstOrDefault<E1, E2, E3, E4, E5, E6, E7>(sql);
        Assert.NotNull(result);
        var (e1, e2, e3, e4, e5, e6, e7) = result.Value;
        Assert.Equal(7, e7.Id);
    }

    // ---------------------------------------------------------------------------
    // CommandOptions<(T1..T7)> / MultiEntityCommandOptions<T1..T7> overload coverage, plus
    // QueryFirst/QuerySingle/QuerySingleOrDefault/QueryStream at every overload, and the
    // remaining QueryFirstOrDefault overloads (AUD-R11 batch-01: previously untested at arity 7).
    // ---------------------------------------------------------------------------

    private const string JoinSql = @"
            SELECT t1.id id1, t2.id id2, t3.id id3, t4.id id4, t5.id id5, t6.id id6, t7.id id7
            FROM multimap7_t1 t1
            CROSS JOIN multimap7_t2 t2
            CROSS JOIN multimap7_t3 t3
            CROSS JOIN multimap7_t4 t4
            CROSS JOIN multimap7_t5 t5
            CROSS JOIN multimap7_t6 t6
            CROSS JOIN multimap7_t7 t7";

    [Theory]
    [SystemSqlite]
    public void Query_SevenEntities_WithCommandOptions_UsesTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE multimap7_t1 SET id = 100 WHERE id = 1";
            cmd.ExecuteNonQuery();
        }

        var options = new CommandOptions<(E1, E2, E3, E4, E5, E6, E7)>(transaction: txn);
        var results = connection.Query<E1, E2, E3, E4, E5, E6, E7>(JoinSql, options);

        Assert.Single(results);
        Assert.Equal(100, results[0].Item1.Id);

        txn.Rollback();
    }

    // AUD-R32: bare (sql, parameters) overload — no CommandOptions/MultiEntityCommandOptions — was never called by any test.
    [Theory]
    [SystemSqlite]
    public void Query_SevenEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = connection.Query<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = 1 });

        Assert.Single(results);
        Assert.Equal(1, results[0].Item1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void Query_SevenEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(E1, E2, E3, E4, E5, E6, E7)>();

        var results = connection.Query<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = 1 }, options);

        Assert.Single(results);
        Assert.Equal(1, results[0].Item1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void Query_SevenEntities_WithMultiEntityCommandOptions_UsesTransaction(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        using var txn = connection.BeginTransaction();

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = txn;
            cmd.CommandText = "UPDATE multimap7_t1 SET id = 100 WHERE id = 1";
            cmd.ExecuteNonQuery();
        }

        var options = new MultiEntityCommandOptions<E1, E2, E3, E4, E5, E6, E7>(transaction: txn);
        var results = connection.Query<E1, E2, E3, E4, E5, E6, E7>(JoinSql, options);

        Assert.Single(results);
        Assert.Equal(100, results[0].Item1.Id);

        txn.Rollback();
    }

    [Theory]
    [SystemSqlite]
    public void Query_SevenEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<E1, E2, E3, E4, E5, E6, E7>();

        var results = connection.Query<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = 1 }, options);

        Assert.Single(results);
        Assert.Equal(1, results[0].Item1.Id);
    }

    // AUD-R13: QueryMultiEntityCore<T1..T7> hardcoded JauntyConfig.QueryResultCapacity for the
    // results list instead of honoring options.ExpectedRowCount (arity-2 equivalent already
    // fixed under AUD-R12).
    [Theory]
    [SystemSqlite]
    public void Query_SevenEntities_WithExpectedRowCount_PreSizesListCapacity(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = CommandOptions<(E1, E2, E3, E4, E5, E6, E7)>.WithExpectedRowCount(500);

        var results = connection.Query<E1, E2, E3, E4, E5, E6, E7>(JoinSql, options);

        Assert.Single(results);
        Assert.True(results.Capacity >= 500);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_SevenEntities_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (e1, _, _, _, _, _, e7) = connection.QueryFirst<E1, E2, E3, E4, E5, E6, E7>(JoinSql);

        Assert.Equal(1, e1.Id);
        Assert.Equal(7, e7.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_SevenEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (e1, _, _, _, _, _, _) = connection.QueryFirst<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = 1 });

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_SevenEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(E1, E2, E3, E4, E5, E6, E7)>();

        var (e1, _, _, _, _, _, _) = connection.QueryFirst<E1, E2, E3, E4, E5, E6, E7>(JoinSql, options);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_SevenEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(E1, E2, E3, E4, E5, E6, E7)>();

        var (e1, _, _, _, _, _, _) = connection.QueryFirst<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = 1 }, options);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_SevenEntities_WithMultiEntityCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<E1, E2, E3, E4, E5, E6, E7>();

        var (e1, _, _, _, _, _, _) = connection.QueryFirst<E1, E2, E3, E4, E5, E6, E7>(JoinSql, options);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirst_SevenEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<E1, E2, E3, E4, E5, E6, E7>();

        var (e1, _, _, _, _, _, _) = connection.QueryFirst<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = 1 }, options);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_SevenEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QueryFirstOrDefault<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_SevenEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(E1, E2, E3, E4, E5, E6, E7)>();

        var result = connection.QueryFirstOrDefault<E1, E2, E3, E4, E5, E6, E7>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Item1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_SevenEntities_WithParametersAndCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(E1, E2, E3, E4, E5, E6, E7)>();

        var result = connection.QueryFirstOrDefault<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_SevenEntities_WithMultiEntityCommandOptions_ReturnsFirstTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<E1, E2, E3, E4, E5, E6, E7>();

        var result = connection.QueryFirstOrDefault<E1, E2, E3, E4, E5, E6, E7>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Item1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QueryFirstOrDefault_SevenEntities_WithParametersAndMultiEntityCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<E1, E2, E3, E4, E5, E6, E7>();

        var result = connection.QueryFirstOrDefault<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_SevenEntities_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (e1, _, _, _, _, _, e7) = connection.QuerySingle<E1, E2, E3, E4, E5, E6, E7>(JoinSql);

        Assert.Equal(1, e1.Id);
        Assert.Equal(7, e7.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_SevenEntities_WithParameters_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var (e1, _, _, _, _, _, _) = connection.QuerySingle<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = 1 });

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_SevenEntities_WithCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(E1, E2, E3, E4, E5, E6, E7)>();

        var (e1, _, _, _, _, _, _) = connection.QuerySingle<E1, E2, E3, E4, E5, E6, E7>(JoinSql, options);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_SevenEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(E1, E2, E3, E4, E5, E6, E7)>();

        var (e1, _, _, _, _, _, _) = connection.QuerySingle<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = 1 }, options);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_SevenEntities_WithMultiEntityCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<E1, E2, E3, E4, E5, E6, E7>();

        var (e1, _, _, _, _, _, _) = connection.QuerySingle<E1, E2, E3, E4, E5, E6, E7>(JoinSql, options);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_SevenEntities_WithParametersAndMultiEntityCommandOptions_FiltersRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<E1, E2, E3, E4, E5, E6, E7>();

        var (e1, _, _, _, _, _, _) = connection.QuerySingle<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = 1 }, options);

        Assert.Equal(1, e1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_SevenEntities_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QuerySingleOrDefault<E1, E2, E3, E4, E5, E6, E7>(JoinSql);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Item1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_SevenEntities_WithParameters_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = connection.QuerySingleOrDefault<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = -999 });

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_SevenEntities_WithCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(E1, E2, E3, E4, E5, E6, E7)>();

        var result = connection.QuerySingleOrDefault<E1, E2, E3, E4, E5, E6, E7>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Item1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_SevenEntities_WithParametersAndCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(E1, E2, E3, E4, E5, E6, E7)>();

        var result = connection.QuerySingleOrDefault<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_SevenEntities_WithMultiEntityCommandOptions_ReturnsSingleTuple(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<E1, E2, E3, E4, E5, E6, E7>();

        var result = connection.QuerySingleOrDefault<E1, E2, E3, E4, E5, E6, E7>(JoinSql, options);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Item1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingleOrDefault_SevenEntities_WithParametersAndMultiEntityCommandOptions_ReturnsNullWhenEmpty(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<E1, E2, E3, E4, E5, E6, E7>();

        var result = connection.QuerySingleOrDefault<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = -999 }, options);

        Assert.Null(result);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_SevenEntities_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = connection.QueryStream<E1, E2, E3, E4, E5, E6, E7>(JoinSql).ToList();

        Assert.Single(results);
        Assert.Equal(1, results[0].Item1.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_SevenEntities_WithParameters_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = connection.QueryStream<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = 1 }).ToList();

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_SevenEntities_WithCommandOptions_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(E1, E2, E3, E4, E5, E6, E7)>();

        var results = connection.QueryStream<E1, E2, E3, E4, E5, E6, E7>(JoinSql, options).ToList();

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_SevenEntities_WithParametersAndCommandOptions_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new CommandOptions<(E1, E2, E3, E4, E5, E6, E7)>();

        var results = connection.QueryStream<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = 1 }, options).ToList();

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_SevenEntities_WithMultiEntityCommandOptions_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<E1, E2, E3, E4, E5, E6, E7>();

        var results = connection.QueryStream<E1, E2, E3, E4, E5, E6, E7>(JoinSql, options).ToList();

        Assert.Single(results);
    }

    [Theory]
    [SystemSqlite]
    public void QueryStream_SevenEntities_WithParametersAndMultiEntityCommandOptions_FiltersRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        var options = new MultiEntityCommandOptions<E1, E2, E3, E4, E5, E6, E7>();

        var results = connection.QueryStream<E1, E2, E3, E4, E5, E6, E7>(
            $"{JoinSql} WHERE t1.id = @Id", new { Id = 1 }, options).ToList();

        Assert.Single(results);
    }
}
