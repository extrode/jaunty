using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Builders;

/// <summary>
/// AUD-R35-190, 191 and 192 on <c>SetOperationBuilder</c>.
/// </summary>
public class SetOperationOperandGuardTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public SetOperationOperandGuardTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    private ISetOperationClause<Product> Union(IQueryTerminal<Product> operand) =>
        _fixture.Connection.From<Product>().Where(p => p.CategoryId == 1).Union(operand);

    // ------------------------------------------------------------------
    // AUD-R35-191 - a paging keyword inside a literal or comment is not paging
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("SELECT product_id, product_name FROM products WHERE product_name = 'Top Gun'")]
    [InlineData("SELECT product_id, product_name FROM products WHERE product_name LIKE '%order by%'")]
    [InlineData("SELECT product_id, product_name FROM products WHERE product_name = 'it''s a LIMIT'")]
    [InlineData("SELECT product_id, product_name FROM products -- no OFFSET here")]
    [InlineData("SELECT product_id, product_name FROM products /* FETCH is only mentioned */")]
    [InlineData("SELECT product_id, \"TOP\" FROM products")]
    [InlineData("SELECT product_id, [ORDER BY] FROM products")]
    public void AnOperandWithAKeywordOnlyInsideALiteralIsAccepted(string operandSql)
    {
        var builder = Union(new StubQueryTerminal<Product>(operandSql));

        Assert.Contains("UNION", builder.ToSql());
    }

    [Theory]
    [InlineData("SELECT product_id FROM products ORDER BY product_name")]
    [InlineData("SELECT product_id FROM products WHERE product_name = 'Top Gun' ORDER BY product_id")]
    [InlineData("SELECT product_id FROM products LIMIT 5")]
    [InlineData("SELECT product_id FROM products OFFSET 5 ROWS")]
    [InlineData("SELECT TOP 5 product_id FROM products")]
    [InlineData("SELECT product_id FROM products -- a comment\nORDER BY product_id")]
    public void AnOperandThatReallyOrdersOrPagesIsStillRejected(string operandSql)
    {
        var operand = new StubQueryTerminal<Product>(operandSql);

        Assert.Throws<NotSupportedException>(() => Union(operand));
    }

    [Fact]
    public void AColumnMerelyContainingAKeywordIsStillAccepted()
    {
        var builder = Union(new StubQueryTerminal<Product>("SELECT toplevel, limits FROM products"));

        Assert.Contains("UNION", builder.ToSql());
    }

    // ------------------------------------------------------------------
    // AUD-R35-192 - ISetOperationClause<T>.Skip, reached before any OrderBy
    // ------------------------------------------------------------------

    [Fact]
    public void SkipBeforeAnyOrderBy_IsApplied()
    {
        var all = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Select();

        var skipped = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Skip(2)
            .Select();

        Assert.True(all.Count > 2);
        Assert.Equal(all.Count - 2, skipped.Count);
    }

    [Fact]
    public void SkipBeforeAnyOrderBy_ComposesWithTake()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Skip(1)
            .Take(2)
            .Select();

        Assert.Equal(2, results.Count);
    }

    // ------------------------------------------------------------------
    // AUD-R35-190 - a throwing First/Single terminal must not clamp the builder
    // ------------------------------------------------------------------

    [Fact]
    public void AThrowingSelectFirst_LeavesTakeUnclamped()
    {
        var dialect = new ThrowOnDemandDialect();
        SqlDialectFactory.RegisterDialect(nameof(SetOpClampConnection), dialect);

        var connection = new SetOpClampConnection();
        var builder = connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(connection.From<Product>().Where(p => p.CategoryId == 2));

        dialect.Throwing = true;
        Assert.Throws<InvalidOperationException>(() => builder.SelectFirst());
        dialect.Throwing = false;

        Assert.DoesNotContain("FETCH NEXT 1 ROWS ONLY", builder.ToSql());
    }

    [Fact]
    public void AThrowingSelectSingle_LeavesAnExplicitTakeIntact()
    {
        var dialect = new ThrowOnDemandDialect();
        SqlDialectFactory.RegisterDialect(nameof(SetOpClampConnection), dialect);

        var connection = new SetOpClampConnection();
        var builder = connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(connection.From<Product>().Where(p => p.CategoryId == 2))
            .Take(4);

        dialect.Throwing = true;
        Assert.Throws<InvalidOperationException>(() => builder.SelectSingle());
        dialect.Throwing = false;

        Assert.Contains("FETCH NEXT 4 ROWS ONLY", builder.ToSql());
    }

    private sealed class ThrowOnDemandDialect : TestDialect
    {
        public bool Throwing { get; set; }

        public override string GetPagingSql(string baseSql, int offset, int fetchNext) =>
            Throwing
                ? throw new InvalidOperationException("ThrowOnDemandDialect is failing on purpose.")
                : $"{baseSql} OFFSET {offset} ROWS FETCH NEXT {fetchNext} ROWS ONLY";
    }

    private sealed class SetOpClampConnection : System.Data.IDbConnection
    {
        public string ConnectionString { get => ""; set { } }
        public int ConnectionTimeout => 0;
        public string Database => "";
        public System.Data.ConnectionState State => System.Data.ConnectionState.Closed;
        public System.Data.IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public System.Data.IDbTransaction BeginTransaction(System.Data.IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public System.Data.IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Open() { }
        public void Dispose() { }
    }
}
