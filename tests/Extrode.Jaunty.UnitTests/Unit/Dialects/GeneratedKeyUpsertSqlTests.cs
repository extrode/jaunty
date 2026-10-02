using Extrode.Jaunty.Dialects;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Dialects;

public class GeneratedKeyUpsertSqlTests
{
    private static string Identity(ISqlDialect dialect)
        => dialect.GenerateUpsertSql("t", ["a", "b"], ["@a", "@b"], ["a", "b"], ["@a", "@b"], ["id"], ["@id"]);

    [Fact]
    public void SQLite_UpdatesByTheKeyThenInsertsWhenNoRowHasIt()
        => Assert.Equal(
            "UPDATE t SET a = @a, b = @b WHERE id = @id; INSERT INTO t (a, b) SELECT @a, @b WHERE NOT EXISTS (SELECT 1 FROM t WHERE id = @id)",
            Identity(new SQLiteDialect()));

    [Fact]
    public void PostgreSql_UpdatesByTheKeyThenInsertsWhenNoRowHasIt()
        => Assert.Equal(
            "UPDATE t SET a = @a, b = @b WHERE id = @id; INSERT INTO t (a, b) SELECT @a, @b WHERE NOT EXISTS (SELECT 1 FROM t WHERE id = @id)",
            Identity(new PostgreSqlDialect()));

    [Fact]
    public void MySql_SelectsFromDual()
        => Assert.Equal(
            "UPDATE t SET a = @a, b = @b WHERE id = @id; INSERT INTO t (a, b) SELECT @a, @b FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM t WHERE id = @id)",
            Identity(new MySqlDialect()));

    [Fact]
    public void NothingToUpdate_LeavesOutTheUpdate()
        => Assert.Equal(
            "INSERT INTO t (a) SELECT @a WHERE NOT EXISTS (SELECT 1 FROM t WHERE id = @id)",
            new SQLiteDialect().GenerateUpsertSql("t", ["a"], ["@a"], [], [], ["id"], ["@id"]));

    [Fact]
    public void ACompositeKeyWithOneGeneratedPart_MatchesOnEveryPart()
        => Assert.Equal(
            "UPDATE t SET a = @a WHERE k1 = @k1 AND k2 = @k2; INSERT INTO t (k2, a) SELECT @k2, @a WHERE NOT EXISTS (SELECT 1 FROM t WHERE k1 = @k1 AND k2 = @k2)",
            new PostgreSqlDialect().GenerateUpsertSql("t", ["k2", "a"], ["@k2", "@a"], ["a"], ["@a"], ["k1", "k2"], ["@k1", "@k2"]));

    [Fact]
    public void AnInsertedKey_KeepsTheNativeUpsert()
        => Assert.StartsWith(
            "INSERT INTO t (id, a) VALUES (@id, @a) ON CONFLICT (id)",
            new SQLiteDialect().GenerateUpsertSql("t", ["id", "a"], ["@id", "@a"], ["a"], ["@a"], ["id"], ["@id"]),
            StringComparison.Ordinal);

    [Fact]
    public void UpsertBatchIsAtomic_OnlyWhereTheServerRunsTheBatchAsOneTransaction()
    {
        Assert.True(new SqlServerDialect().UpsertBatchIsAtomic);
        Assert.True(new PostgreSqlDialect().UpsertBatchIsAtomic);
        Assert.False(new MySqlDialect().UpsertBatchIsAtomic);
        Assert.False(new SQLiteDialect().UpsertBatchIsAtomic);
    }

    [Theory]
    [InlineData(new[] { "id", "a" }, new[] { "id" }, false)]
    [InlineData(new[] { "a" }, new[] { "id" }, true)]
    [InlineData(new[] { "k1", "a" }, new[] { "k1", "k2" }, true)]
    public void Applies_WhenAKeyIsNotInserted(string[] insertColumns, string[] keyColumns, bool expected)
        => Assert.Equal(expected, GeneratedKeyUpsertSql.Applies(insertColumns, keyColumns));
}
