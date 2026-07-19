using System.Data;
using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>Integration tests for Query&lt;T1,T2,T3,T4,T5,T6&gt; — arity-6 multi-entity mapping.</summary>
[Collection("Multi-Entity Mapper Reflection Resolver")]
public class QueryMultiEntityN6Tests : IClassFixture<DialectFixture>
{
    internal sealed class E1 { [Column("id1")] public long Id { get; set; } }
    internal sealed class E2 { [Column("id2")] public long Id { get; set; } }
    internal sealed class E3 { [Column("id3")] public long Id { get; set; } }
    internal sealed class E4 { [Column("id4")] public long Id { get; set; } }
    internal sealed class E5 { [Column("id5")] public long Id { get; set; } }
    internal sealed class E6 { [Column("id6")] public long Id { get; set; } }

    private static IDbConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        for (int i = 1; i <= 6; i++)
        {
            Execute(connection, $@"
                CREATE TABLE multimap6_t{i} (id INTEGER PRIMARY KEY);
                INSERT INTO multimap6_t{i} VALUES ({i});");
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
    public void Query_SixEntities_MapsAllSixTypes(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        const string sql = @"
            SELECT t1.id id1, t2.id id2, t3.id id3, t4.id id4, t5.id id5, t6.id id6
            FROM multimap6_t1 t1
            CROSS JOIN multimap6_t2 t2
            CROSS JOIN multimap6_t3 t3
            CROSS JOIN multimap6_t4 t4
            CROSS JOIN multimap6_t5 t5
            CROSS JOIN multimap6_t6 t6";

        var results = connection.Query<E1, E2, E3, E4, E5, E6>(sql);
        Assert.Single(results);
        var (e1, e2, e3, e4, e5, e6) = results[0];
        Assert.Equal(1, e1.Id);
        Assert.Equal(2, e2.Id);
        Assert.Equal(3, e3.Id);
        Assert.Equal(4, e4.Id);
        Assert.Equal(5, e5.Id);
        Assert.Equal(6, e6.Id);
    }

    [Theory]
    [SystemSqlite]
    public void QuerySingle_SixEntities_ReturnsSingleRow(DialectInfo _)
    {
        using var connection = CreateAndSeed();
        const string sql = @"
            SELECT t1.id id1, t2.id id2, t3.id id3, t4.id id4, t5.id id5, t6.id id6
            FROM multimap6_t1 t1
            CROSS JOIN multimap6_t2 t2
            CROSS JOIN multimap6_t3 t3
            CROSS JOIN multimap6_t4 t4
            CROSS JOIN multimap6_t5 t5
            CROSS JOIN multimap6_t6 t6";

        var (e1, e2, e3, e4, e5, e6) = connection.QuerySingle<E1, E2, E3, E4, E5, E6>(sql);
        Assert.Equal(1, e1.Id);
        Assert.Equal(6, e6.Id);
    }
}
