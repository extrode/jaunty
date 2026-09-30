using Extrode.Jaunty.Dialects;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Dialects;

public class UpsertMultiColumnTests
{
    private static string Upsert(ISqlDialect dialect)
        => dialect.GenerateUpsertSql("t", ["a", "b", "c"], ["@a", "@b", "@c"], ["b", "c"], ["@b", "@c"], ["k1", "k2"], ["@k1", "@k2"]);

    [Fact]
    public void SQLite_SeparatesEveryColumnParameterAndKey()
        => Assert.StartsWith("INSERT INTO t (a, b, c) VALUES (@a, @b, @c) ON CONFLICT (k1, k2) DO UPDATE SET ", Upsert(new SQLiteDialect()), StringComparison.Ordinal);

    [Fact]
    public void PostgreSql_SeparatesEveryColumnParameterAndKey()
        => Assert.StartsWith("INSERT INTO t (a, b, c) VALUES (@a, @b, @c) ON CONFLICT (k1, k2) DO UPDATE SET ", Upsert(new PostgreSqlDialect()), StringComparison.Ordinal);

    [Fact]
    public void MySql_SeparatesEveryColumnAndParameter()
        => Assert.StartsWith("INSERT INTO t (a, b, c) VALUES (@a, @b, @c) ON DUPLICATE KEY UPDATE ", Upsert(new MySqlDialect()), StringComparison.Ordinal);
}

public class OverClauseMultiColumnTests
{
    private static readonly (string column, bool descending)[] Order = [("a", false), ("b", true), ("c", false)];

    [Fact]
    public void MySql_SeparatesEveryOrderColumn()
        => Assert.Equal(" OVER (PARTITION BY p, q ORDER BY a, b DESC, c)", new MySqlDialect().GenerateOverClause(["p", "q"], Order));

    [Fact]
    public void PostgreSql_SeparatesEveryOrderColumn()
        => Assert.Equal(" OVER (PARTITION BY p, q ORDER BY a, b DESC, c)", new PostgreSqlDialect().GenerateOverClause(["p", "q"], Order));
}

public class SqliteTableNameTests
{
    [Fact]
    public void EscapeTableName_RejectsAnUnsafeName()
        => Assert.Throws<ArgumentException>(() => new SQLiteDialect().EscapeTableName(null, "a;b"));
}
