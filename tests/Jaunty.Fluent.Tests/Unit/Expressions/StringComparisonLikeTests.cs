using System.Data;

using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// AUD-R25: <c>WhereExpressionVisitor</c>'s <c>Contains</c>/<c>StartsWith</c>/<c>EndsWith</c> cases
/// read only <c>Arguments[0]</c> and unconditionally emitted
/// <c>ISqlDialect.GenerateCaseSensitiveLike</c>, silently discarding the <see cref="StringComparison"/>
/// argument of the two-argument overloads.
///
/// <para>
/// This was worse than "falls back to the database's collation". SQL Server's
/// GenerateCaseSensitiveLike appends <c>COLLATE Latin1_General_CS_AS</c> and MySQL's appends
/// <c>COLLATE utf8mb4_bin</c>, so an explicit OrdinalIgnoreCase request actively overrode a
/// case-insensitive column collation and returned the exact opposite of what the caller asked for.
/// The sibling <c>Equals</c> case in the same switch already handled the argument correctly, and
/// <c>GenerateCaseInsensitiveLike</c> existed on all four dialects with no caller.
/// </para>
/// </summary>
public class StringComparisonLikeTests
{
    public StringComparisonLikeTests()
    {
        SqlDialectFactory.RegisterDialect(nameof(SqlServerStubConnection), new SqlServerDialect());
        SqlDialectFactory.RegisterDialect(nameof(PostgresStubConnection), new PostgreSqlDialect());
    }

    // ------------------------------------------------------------------
    // SQL Server: the collation is flipped, so the divergence is visible in the SQL
    // ------------------------------------------------------------------

    [Fact]
    public void Contains_WithOrdinalIgnoreCase_EmitsTheCaseInsensitiveCollation()
    {
        var sql = new SqlServerStubConnection().From<Product>()
            .Where(p => p.ProductName.Contains("abc", StringComparison.OrdinalIgnoreCase))
            .ToSql();

        Assert.Contains("COLLATE Latin1_General_CI_AS", sql);
        Assert.DoesNotContain("COLLATE Latin1_General_CS_AS", sql);
    }

    [Fact]
    public void StartsWith_WithOrdinalIgnoreCase_EmitsTheCaseInsensitiveCollation()
    {
        var sql = new SqlServerStubConnection().From<Product>()
            .Where(p => p.ProductName.StartsWith("abc", StringComparison.OrdinalIgnoreCase))
            .ToSql();

        Assert.Contains("COLLATE Latin1_General_CI_AS", sql);
    }

    [Fact]
    public void EndsWith_WithOrdinalIgnoreCase_EmitsTheCaseInsensitiveCollation()
    {
        var sql = new SqlServerStubConnection().From<Product>()
            .Where(p => p.ProductName.EndsWith("abc", StringComparison.OrdinalIgnoreCase))
            .ToSql();

        Assert.Contains("COLLATE Latin1_General_CI_AS", sql);
    }

    [Theory]
    [InlineData(StringComparison.InvariantCultureIgnoreCase)]
    [InlineData(StringComparison.CurrentCultureIgnoreCase)]
    public void Contains_WithAnyIgnoreCaseComparison_IsCaseInsensitive(StringComparison comparison)
    {
        // All three IgnoreCase values are treated alike; the culture distinction between them is not
        // expressible in SQL, but the case sensitivity is what changes which rows come back.
        var sql = new SqlServerStubConnection().From<Product>()
            .Where(p => p.ProductName.Contains("abc", comparison))
            .ToSql();

        Assert.Contains("COLLATE Latin1_General_CI_AS", sql);
    }

    [Theory]
    [InlineData(StringComparison.Ordinal)]
    [InlineData(StringComparison.InvariantCulture)]
    [InlineData(StringComparison.CurrentCulture)]
    public void Contains_WithACaseSensitiveComparison_StaysCaseSensitive(StringComparison comparison)
    {
        var sql = new SqlServerStubConnection().From<Product>()
            .Where(p => p.ProductName.Contains("abc", comparison))
            .ToSql();

        Assert.Contains("COLLATE Latin1_General_CS_AS", sql);
    }

    [Fact]
    public void Contains_SingleArgumentOverload_IsUnchanged()
    {
        // The one-argument overload has no comparison to honour and must keep its previous behaviour.
        var sql = new SqlServerStubConnection().From<Product>()
            .Where(p => p.ProductName.Contains("abc"))
            .ToSql();

        Assert.Contains("COLLATE Latin1_General_CS_AS", sql);
    }

    // ------------------------------------------------------------------
    // PostgreSQL: LIKE vs ILIKE
    // ------------------------------------------------------------------

    [Fact]
    public void Contains_WithOrdinalIgnoreCase_UsesIlikeOnPostgres()
    {
        var sql = new PostgresStubConnection().From<Product>()
            .Where(p => p.ProductName.Contains("abc", StringComparison.OrdinalIgnoreCase))
            .ToSql();

        Assert.Contains("ILIKE", sql);
    }

    [Fact]
    public void Contains_WithoutAComparison_UsesLikeOnPostgres()
    {
        var sql = new PostgresStubConnection().From<Product>()
            .Where(p => p.ProductName.Contains("abc"))
            .ToSql();

        Assert.DoesNotContain("ILIKE", sql);
        Assert.Contains("LIKE", sql);
    }

    [Fact]
    public void StartsWith_WithOrdinalIgnoreCase_StillBindsThePrefixPattern()
    {
        // Honouring the comparison must not disturb the pattern the parameter is bound to.
        var builder = Assert.IsType<QueryBuilder<Product>>(
            new PostgresStubConnection().From<Product>()
                .Where(p => p.ProductName.StartsWith("abc", StringComparison.OrdinalIgnoreCase)));

        Assert.Contains(builder.GetParameters().GetAll(), p => Equals(p.Value, "abc%"));
    }

    [Fact]
    public void Equals_WithOrdinalIgnoreCase_IsStillHandled()
    {
        // The case that already worked, kept covered through the shared helper it now routes to.
        var sql = new PostgresStubConnection().From<Product>()
            .Where(p => p.ProductName.Equals("abc", StringComparison.OrdinalIgnoreCase))
            .ToSql();

        Assert.Contains("LOWER", sql, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Stub connections - nothing is executed, only ToSql()/GetParameters() are read
    // ------------------------------------------------------------------

    private class StubConnection : IDbConnection
    {
        public string ConnectionString { get => ""; set { } }
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Open;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Open() { }
        public void Dispose() { }
    }

    private sealed class SqlServerStubConnection : StubConnection;

    private sealed class PostgresStubConnection : StubConnection;
}
