using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
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
}
