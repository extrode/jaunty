using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>Integration tests for QueryAsync&lt;T1,T2,T3,T4,T5,T6,T7&gt; — arity-7 async multi-entity mapping.</summary>
public class QueryMultiEntityAsyncN7Tests : IClassFixture<DialectFixture>
{
    internal sealed class Ae1 { [Column("id1")] public long Id { get; set; } }
    internal sealed class Ae2 { [Column("id2")] public long Id { get; set; } }
    internal sealed class Ae3 { [Column("id3")] public long Id { get; set; } }
    internal sealed class Ae4 { [Column("id4")] public long Id { get; set; } }
    internal sealed class Ae5 { [Column("id5")] public long Id { get; set; } }
    internal sealed class Ae6 { [Column("id6")] public long Id { get; set; } }
    internal sealed class Ae7 { [Column("id7")] public long Id { get; set; } }

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        for (int i = 1; i <= 7; i++)
        {
            Execute(connection, $@"
                CREATE TABLE amultimap7_t{i} (id INTEGER PRIMARY KEY);
                INSERT INTO amultimap7_t{i} VALUES ({i});");
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
        SELECT t1.id id1, t2.id id2, t3.id id3, t4.id id4, t5.id id5, t6.id id6, t7.id id7
        FROM amultimap7_t1 t1
        CROSS JOIN amultimap7_t2 t2
        CROSS JOIN amultimap7_t3 t3
        CROSS JOIN amultimap7_t4 t4
        CROSS JOIN amultimap7_t5 t5
        CROSS JOIN amultimap7_t6 t6
        CROSS JOIN amultimap7_t7 t7";

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_SevenEntities_MapsAllSevenTypes(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var results = await connection.QueryAsync<Ae1, Ae2, Ae3, Ae4, Ae5, Ae6, Ae7>(Sql);

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
    public async Task QueryFirstOrDefaultAsync_SevenEntities_ReturnsFirstOrNull(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var result = await connection.QueryFirstOrDefaultAsync<Ae1, Ae2, Ae3, Ae4, Ae5, Ae6, Ae7>(Sql);

        Assert.NotNull(result);
        var (_, _, _, _, _, _, e7) = result.Value;
        Assert.Equal(7, e7.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryStreamAsync_SevenEntities_StreamsAllRows(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        var seen = new List<(Ae1, Ae2, Ae3, Ae4, Ae5, Ae6, Ae7)>();
        await foreach (var row in connection.QueryStreamAsync<Ae1, Ae2, Ae3, Ae4, Ae5, Ae6, Ae7>(Sql))
        {
            seen.Add(row);
        }

        Assert.Single(seen);
        Assert.Equal(7, seen[0].Item7.Id);
    }

    [Theory]
    [SystemSqlite]
    public async Task QueryAsync_SevenEntities_NullSql_ThrowsArgumentNullException(DialectInfo _)
    {
        using var connection = CreateAndSeed();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => connection.QueryAsync<Ae1, Ae2, Ae3, Ae4, Ae5, Ae6, Ae7>(null!).AsTask());
    }
}
